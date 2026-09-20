using System.Text.Json;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Application.Compliance;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Infrastructure.Compliance;

/// <summary>
/// Evaluates a transaction against the active, approved, effective-dated ComplianceRule
/// catalogue and returns a risk score/rating/recommended action. FR-010/FR-011/FR-012.
/// Rule thresholds are configuration-driven (ComplianceRule.ParametersJson); the minimum
/// rule catalogue (retrospective PO, expired contract, ceiling exceeded, splitting,
/// duplicate invoice, repeat emergency procurement) is extensible without further code
/// changes to this evaluator loop, only to the individual rule-code handlers below.
/// </summary>
public class ComplianceRuleEngine : IComplianceRuleEngine
{
    private const int AmberThreshold = 30;
    private const int RedThreshold = 60;

    private readonly IfwemsDbContext _dbContext;

    public ComplianceRuleEngine(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TransactionCheckResult> EvaluateAsync(TransactionCheckRequest request, CancellationToken cancellationToken = default)
    {
        var activeRules = await _dbContext.ComplianceRules
            .Where(r => r.IsActive && r.IsApproved
                && r.EffectiveFromUtc <= DateTime.UtcNow
                && (r.EffectiveToUtc == null || r.EffectiveToUtc > DateTime.UtcNow))
            .ToListAsync(cancellationToken);

        var failedRuleCodes = new List<string>();
        var riskScore = 0;

        foreach (var rule in activeRules)
        {
            var (failed, weight) = await EvaluateRuleAsync(rule, request, cancellationToken);
            if (failed)
            {
                failedRuleCodes.Add(rule.Code);
                riskScore += weight;
            }
        }

        riskScore = Math.Min(riskScore, 100);

        var rating = riskScore switch
        {
            >= 60 => "RED",
            >= 30 => "AMBER",
            _ => "GREEN"
        };

        var action = rating switch
        {
            "RED" => "BLOCK",
            "AMBER" => "REVIEW",
            _ => "PASS"
        };

        // Persist every screened transaction (FR-010; Section 6.3 financial_transaction),
        // not only emergency overrides. Without this record, (a) there is no register of
        // what was screened and why, and (b) the duplicate-invoice/repeat-emergency/
        // splitting rule handlers below -- which query _dbContext.Transactions for recent
        // history -- would never see a routine (non-override) transaction, so those checks
        // could never actually fire against real procurement-splitting or duplicate-invoice
        // patterns in normal traffic.
        _dbContext.Transactions.Add(new Transaction
        {
            TransactionReference = request.TransactionReference,
            OrgUnitId = request.OrgUnitId,
            SupplierId = request.SupplierId,
            Amount = request.Amount,
            RiskScore = riskScore,
            RiskRating = rating,
            RecommendedAction = Enum.Parse<ComplianceAction>(action, ignoreCase: true),
            FailedRuleCodes = failedRuleCodes.Count > 0 ? string.Join(',', failedRuleCodes) : null,
            IsEmergencyOverride = false
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TransactionCheckResult(riskScore, rating, failedRuleCodes, action);
    }

    private async Task<(bool Failed, int Weight)> EvaluateRuleAsync(ComplianceRule rule, TransactionCheckRequest request, CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rule.ParametersJson) ?? new();
        var weight = parameters.TryGetValue("weight", out var w) ? w.GetInt32() : 20;

        var failed = rule.Code switch
        {
            "CEILING_EXCEEDED" => await IsCeilingExceededAsync(request, parameters),
            "EXPIRED_CONTRACT" => await IsContractExpiredAsync(request, cancellationToken),
            "DUPLICATE_INVOICE" => await IsDuplicateInvoiceAsync(request, cancellationToken),
            "RETROSPECTIVE_PO" => IsRetrospectivePurchaseOrder(request),
            "REPEAT_EMERGENCY_PROCUREMENT" => await IsRepeatEmergencyProcurementAsync(request, parameters, cancellationToken),
            "SPLITTING" => await IsPotentialSplittingAsync(request, parameters, cancellationToken),
            _ => false
        };

        return (failed, weight);
    }

    private static Task<bool> IsCeilingExceededAsync(TransactionCheckRequest request, Dictionary<string, JsonElement> parameters)
    {
        var maxAmount = parameters.TryGetValue("maxAmount", out var m) ? m.GetDecimal() : decimal.MaxValue;
        return Task.FromResult(request.Amount > maxAmount);
    }

    private async Task<bool> IsContractExpiredAsync(TransactionCheckRequest request, CancellationToken cancellationToken)
    {
        if (request.ContractId is null) return false;
        var contract = await _dbContext.Contracts.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken);
        return contract is not null && contract.ExpiryDateUtc < DateTime.UtcNow;
    }

    private async Task<bool> IsDuplicateInvoiceAsync(TransactionCheckRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvoiceReference) || request.SupplierId is null) return false;
        return await _dbContext.Transactions.AsNoTracking().AnyAsync(
            t => t.SupplierId == request.SupplierId && t.TransactionReference == request.InvoiceReference,
            cancellationToken);
    }

    private static bool IsRetrospectivePurchaseOrder(TransactionCheckRequest request) =>
        request.PurchaseOrderDateUtc.HasValue && request.PurchaseOrderDateUtc.Value > DateTime.UtcNow;

    private async Task<bool> IsRepeatEmergencyProcurementAsync(TransactionCheckRequest request, Dictionary<string, JsonElement> parameters, CancellationToken cancellationToken)
    {
        if (request.SupplierId is null) return false;
        var windowDays = parameters.TryGetValue("windowDays", out var d) ? d.GetInt32() : 90;
        var since = DateTime.UtcNow.AddDays(-windowDays);

        return await _dbContext.Transactions.AsNoTracking().AnyAsync(
            t => t.SupplierId == request.SupplierId
                && t.OrgUnitId == request.OrgUnitId
                && t.IsEmergencyOverride
                && t.CreatedAtUtc >= since,
            cancellationToken);
    }

    private async Task<bool> IsPotentialSplittingAsync(TransactionCheckRequest request, Dictionary<string, JsonElement> parameters, CancellationToken cancellationToken)
    {
        if (request.SupplierId is null) return false;
        var windowDays = parameters.TryGetValue("windowDays", out var d) ? d.GetInt32() : 7;
        var maxAmount = parameters.TryGetValue("maxAmount", out var m) ? m.GetDecimal() : decimal.MaxValue;
        var since = DateTime.UtcNow.AddDays(-windowDays);

        var recentTotal = await _dbContext.Transactions.AsNoTracking()
            .Where(t => t.SupplierId == request.SupplierId && t.OrgUnitId == request.OrgUnitId && t.CreatedAtUtc >= since)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        return recentTotal + request.Amount > maxAmount && request.Amount <= maxAmount;
    }
}
