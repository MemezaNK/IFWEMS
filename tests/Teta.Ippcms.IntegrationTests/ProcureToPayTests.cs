using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Teta.Ippcms.Application.Audit;
using Teta.Ippcms.Application.Budget;
using Teta.Ippcms.Application.ContractManagement;
using Teta.Ippcms.Application.Documents;
using Teta.Ippcms.Application.Finance;
using Teta.Ippcms.Application.Portal;
using Teta.Ippcms.Application.Reporting;
using Teta.Ippcms.Application.Sourcing;
using Teta.Ippcms.Application.Suppliers;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Suppliers;

namespace Teta.Ippcms.IntegrationTests;

/// <summary>
/// UAT: requisition → sourcing → evaluation → adjudication → award → contract → deliverable (supplier
/// portal) → invoice three-way check → certification → ERP payment, with BR-002/003/004/006/008 and
/// audit integrity asserted along the way.
/// </summary>
public sealed class ProcureToPayTests : IClassFixture<ScenarioFixture>
{
    private readonly TetaApiFactory _factory;
    private readonly Cast _cast;

    public ProcureToPayTests(ScenarioFixture fixture)
    {
        _factory = fixture.Factory;
        _cast = fixture.Cast;
    }

    private static Task<T> Post<T>(HttpClient c, string url, object? body) => TetaApiFactory.PostAsync<T>(c, url, body);
    private static Task<T> Get<T>(HttpClient c, string url) => TetaApiFactory.GetAsync<T>(c, url);

    [Fact]
    public async Task Requisition_to_payment_end_to_end()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ---- Approved project with a baselined budget ----
        var project = await _cast.ApprovedProjectAsync("Artisan development programme", 2_500_000m);
        var pm = await _cast.PmAsync();
        var line = await TetaApiFactory.PostAsync<BudgetLineDto>(pm, $"/api/v1/projects/{project.Id}/budget/lines",
            new SaveBudgetLineRequest("2026/27", "Professional services", "Discretionary grant", 2_500_000m, false, null));
        var summary = await Post<BudgetSummaryDto>(pm, $"/api/v1/projects/{project.Id}/budget/baseline", null);
        Assert.True(summary.Reconciled);

        // ---- Requisition (FR-SCM-001) and approval workflow ----
        var requisition = await Post<RequisitionDto>(pm, "/api/v1/procurement/requisitions", new SaveRequisitionRequest(project.Id, null, line.Id,
            "Artisan training services", "Training of 50 artisans", 500_000m, today.AddMonths(2), null, null, null));
        var reqWf = await Post<WorkflowInstanceDto>(pm, $"/api/v1/procurement/requisitions/{requisition.Id}/submit", null);
        Assert.Contains(reqWf.Steps, s => s.Code == "SCM_APPROVE");
        Assert.DoesNotContain(reqWf.Steps, s => s.Code == "CFO_APPROVE"); // R500k is inside Head SCM's band
        Assert.Equal("Approved", (await _cast.ApproveAllAsync(reqWf.Id)).State);

        var scm = await _cast.AsAsync(Roles.ScmOfficer);
        var headScm = await _cast.AsAsync(Roles.HeadScm);
        var procurement = await Post<ProcurementDetailDto>(scm, $"/api/v1/procurement/requisitions/{requisition.Id}/convert", null);
        Assert.Equal("RFQ", procurement.Summary.Method);
        var pid = procurement.Summary.Id;

        // ---- Specification: author cannot approve own spec (SoD) ----
        var spec = await Post<SpecificationDto>(scm, $"/api/v1/procurement/{pid}/specifications", new SaveSpecificationRequest("TOR", "Scope of services"));
        await Post<SpecificationDto>(scm, $"/api/v1/procurement/{pid}/specifications/{spec.Id}/submit", null);
        var selfApprove = await scm.PostAsync($"/api/v1/procurement/{pid}/specifications/{spec.Id}/approve", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, selfApprove.StatusCode);
        await Post<SpecificationDto>(headScm, $"/api/v1/procurement/{pid}/specifications/{spec.Id}/approve", null);

        // ---- Evaluation committee with a time-bound evaluator ----
        var evaluatorId = _factory.CreateUser("p2p.evaluator", Roles.Evaluator);
        var evaluator = await _factory.ClientForAsync("p2p.evaluator");
        var committee = await Post<CommitteeDto>(scm, $"/api/v1/procurement/{pid}/committees", new SaveCommitteeRequest(CommitteeType.BidEvaluation, "BEC", 1));
        await Post<CommitteeDto>(scm, $"/api/v1/procurement/committees/{committee.Id}/members",
            new AddMemberRequest(evaluatorId, CommitteeRole.Chairperson, today.AddDays(-1), today.AddDays(30)));

        // ---- Publication (minimum advert days enforced) ----
        var tooShort = await scm.PostAsJsonAsync($"/api/v1/procurement/{pid}/publications",
            new SavePublicationRequest("eTender", "RFQ-001", today, DateTime.UtcNow.AddDays(2), null, null, null), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooShort.StatusCode);
        await Post<PublicationDto>(scm, $"/api/v1/procurement/{pid}/publications",
            new SavePublicationRequest("eTender", "RFQ-001", today.AddDays(-8), DateTime.UtcNow.AddDays(1), "https://etenders.example/rfq-001", null, null));

        // ---- Suppliers and bids ----
        var s1 = await Post<SupplierDto>(scm, "/api/v1/suppliers", new SaveSupplierRequest("Alpha Artisan Academy (Pty) Ltd", null, "2010/000111/07", "MAAA0000111",
            "9000000111", null, 1, "alpha@test.local", null, null, "Gauteng", true, true, today.AddYears(1), "V111", SupplierStatus.Active));
        var s2 = await Post<SupplierDto>(scm, "/api/v1/suppliers", new SaveSupplierRequest("Beta Skills Training CC", null, "2011/000222/23", "MAAA0000222",
            "9000000222", null, 2, "beta@test.local", null, null, "Gauteng", false, true, today.AddYears(1), "V222", SupplierStatus.Active));
        var dup = await scm.PostAsJsonAsync("/api/v1/suppliers", new SaveSupplierRequest("ALPHA ARTISAN ACADEMY PTY LTD", null, null, null, null, null, null, null,
            null, null, null, false, false, null, null, null), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode); // FR-SCM-018 duplicate control

        var bid1 = await Post<BidDto>(scm, $"/api/v1/procurement/{pid}/bids", new RegisterBidRequest(s1.Id, "B-001", 450_000m, 18m, null));
        var bid2 = await Post<BidDto>(scm, $"/api/v1/procurement/{pid}/bids", new RegisterBidRequest(s2.Id, "B-002", 420_000m, 10m, null));

        // Closing date passes; bids are opened.
        using (var db = _factory.CreateContext())
        {
            var p = await db.Procurements.SingleAsync(x => x.Id == pid);
            p.ClosingDateUtc = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }
        await Post<List<BidDto>>(scm, $"/api/v1/procurement/{pid}/bids/open", null);

        // ---- BR-003: no bid access before a declaration ----
        var blocked = await evaluator.GetAsync($"/api/v1/procurement/{pid}/bids");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        await Post<DeclarationDto>(evaluator, $"/api/v1/procurement/{pid}/declarations", new DeclareRequest(false, null, true));
        await Post<DeclarationDto>(scm, $"/api/v1/procurement/{pid}/declarations", new DeclareRequest(false, null, true));
        Assert.Equal(2, (await Get<List<BidDto>>(evaluator, $"/api/v1/procurement/{pid}/bids")).Count);

        // ---- Criteria, evaluation and consolidation (80/20) ----
        var compliance = await Post<CriterionDto>(scm, $"/api/v1/procurement/{pid}/criteria",
            new SaveCriterionRequest(EvaluationStage.Compliance, "Valid tax compliance status", null, 0, 1, true, 1));
        var methodology = await Post<CriterionDto>(scm, $"/api/v1/procurement/{pid}/criteria",
            new SaveCriterionRequest(EvaluationStage.Technical, "Methodology", null, 60, 5, true, 2));
        var experience = await Post<CriterionDto>(scm, $"/api/v1/procurement/{pid}/criteria",
            new SaveCriterionRequest(EvaluationStage.Technical, "Experience", null, 40, 5, true, 3));
        var setup = await scm.PutAsJsonAsync($"/api/v1/procurement/{pid}/evaluation-setup", new EvaluationSetupRequest(60m, "80/20"), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, setup.StatusCode);

        foreach (var (bid, m, e) in new[] { (bid1, 5m, 4m), (bid2, 3m, 3m) })
        {
            await Post<ScoreDto>(evaluator, $"/api/v1/procurement/{pid}/scores", new SubmitScoreRequest(bid.Id, compliance.Id, null, true, "Compliant", null));
            await Post<ScoreDto>(evaluator, $"/api/v1/procurement/{pid}/scores", new SubmitScoreRequest(bid.Id, methodology.Id, m, null, "Scored", null));
            await Post<ScoreDto>(evaluator, $"/api/v1/procurement/{pid}/scores", new SubmitScoreRequest(bid.Id, experience.Id, e, null, "Scored", null));
        }
        var report = await Post<EvaluationReportDto>(scm, $"/api/v1/procurement/{pid}/consolidate", null);
        var first = report.Bids.Single(b => b.Id == bid1.Id);
        var second = report.Bids.Single(b => b.Id == bid2.Id);
        Assert.Equal(92m, first.TechnicalScore);      // 5/5*60 + 4/5*40
        Assert.Equal(60m, second.TechnicalScore);     // exactly on the 60-point threshold -> still responsive
        Assert.Equal(80m, second.PricePoints);         // lowest price
        Assert.Equal(74.29m, first.PricePoints);       // 80 x (1 - (450k - 420k) / 420k)
        Assert.Equal(1, first.Rank);                   // 74.29 + 18 > 80 + 10
        Assert.Equal("Recommended", first.Status);

        // ---- Due diligence and adjudication (BR-004) ----
        var dd = await Post<DueDiligenceDto>(scm, $"/api/v1/procurement/{pid}/due-diligence",
            new SaveDueDiligenceRequest(bid1.Id, true, true, true, true, true, true, "All checks passed"));
        Assert.Equal("Passed", dd.Outcome);
        var adjWf = await Post<WorkflowInstanceDto>(scm, $"/api/v1/procurement/{pid}/adjudication",
            new SubmitAdjudicationRequest(bid1.Id, "Award to the highest-ranked responsive bidder", null));
        Assert.Equal(new[] { "BAC_RECOMMEND", "CFO_AWARD" }, adjWf.Steps.Select(s => s.Code));
        Assert.Equal("Approved", (await _cast.ApproveAllAsync(adjWf.Id)).State);

        var detail = await Get<ProcurementDetailDto>(scm, $"/api/v1/procurement/{pid}");
        Assert.Equal("Awarded", detail.Summary.Status);
        var award = detail.Award!;
        Assert.Equal(s1.Id, award.SupplierId);
        Assert.Equal(450_000m, award.Amount);

        // ---- Contract from award, signed agreement, commitment (FR-CON-001/005) ----
        var cm = await _cast.AsAsync(Roles.ContractManager);
        var contract = await Post<ContractDetailDto>(cm, "/api/v1/contracts/from-award",
            new CreateContractFromAwardRequest(award.Id, "Artisan training services", today, today.AddMonths(10), _cast.UserIds[Roles.ContractManager], "CM", "PO-7788"));
        var cid = contract.Summary.Id;
        var signed = await UploadAsync(cm, "/api/v1/documents", "Contract", cid, "Contract", "signed-contract.pdf", "%PDF-1.4 signed agreement");
        var active = await Post<ContractDetailDto>(cm, $"/api/v1/contracts/{cid}/sign", new SignContractRequest(today, signed.Id));
        Assert.Equal("Active", active.Summary.Status);
        Assert.Equal(450_000m, active.Summary.Committed);

        // ---- Deliverable submitted by the supplier through the portal ----
        var deliverable = await Post<DeliverableDto>(cm, "/api/v1/contracts/deliverables",
            new SaveDeliverableRequest(project.Id, cid, null, "Phase 1 training delivered", null, today.AddMonths(2), 200_000m, "Attendance register", true));
        var supplierUserId = _factory.CreateUser("p2p.supplier", Roles.Supplier);
        var linkUser = await scm.PostAsJsonAsync($"/api/v1/suppliers/{s1.Id}/portal-users", new { userId = supplierUserId }, TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, linkUser.StatusCode);
        var supplier = await _factory.ClientForAsync("p2p.supplier");
        var myContracts = await Get<List<PortalContractDto>>(supplier, "/api/v1/portal/contracts");
        Assert.Single(myContracts);
        var early = await supplier.PostAsJsonAsync($"/api/v1/portal/deliverables/{deliverable.Id}/submit", new SubmitDeliverableRequest(null), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, early.StatusCode); // evidence required first
        await UploadAsync(supplier, "/api/v1/documents/evidence", "Deliverable", deliverable.Id, "Attendance register", "register.pdf", "%PDF-1.4 register",
            evidenceType: "Attendance register");
        await Post<PortalDeliverableDto>(supplier, $"/api/v1/portal/deliverables/{deliverable.Id}/submit", new SubmitDeliverableRequest("Phase 1 complete"));
        var accepted = await Post<DeliverableDto>(cm, $"/api/v1/contracts/deliverables/{deliverable.Id}/decision", new DeliverableDecisionRequest(true, null));
        Assert.Equal("Accepted", accepted.AcceptanceStatus);

        // ---- Invoice via the portal; three-way check; certification workflow ----
        var overBilled = await supplier.PostAsJsonAsync("/api/v1/portal/invoices",
            new RegisterInvoiceRequest(cid, deliverable.Id, "INV-A-1", "PO-7788", today, null, 250_000m, 37_500m), TetaApiFactory.Json);
        var overBilledInvoice = await TetaApiFactory.ReadAsync<PortalInvoiceDto>(overBilled);
        Assert.Equal("ValidationFailed", overBilledInvoice.Status); // exceeds accepted payable amount

        supplier.DefaultRequestHeaders.Add("Idempotency-Key", "portal-invoice-1");
        var invoice = await Post<PortalInvoiceDto>(supplier, "/api/v1/portal/invoices",
            new RegisterInvoiceRequest(cid, deliverable.Id, "INV-A-2", "PO-7788", today, null, 200_000m, 30_000m));
        var replay = await Post<PortalInvoiceDto>(supplier, "/api/v1/portal/invoices",
            new RegisterInvoiceRequest(cid, deliverable.Id, "INV-A-2", "PO-7788", today, null, 200_000m, 30_000m));
        supplier.DefaultRequestHeaders.Remove("Idempotency-Key");
        Assert.Equal(invoice.Id, replay.Id); // idempotent retry
        Assert.Equal("PendingCertification", invoice.Status);

        // A finance clerk submits; a different finance officer certifies (the submitter never approves their own transaction).
        var finance = await _cast.AsAsync(Roles.FinanceOfficer);
        _factory.CreateUser("p2p.finclerk", Roles.FinanceOfficer);
        var clerk = await _factory.ClientForAsync("p2p.finclerk");
        var certWf = await Post<WorkflowInstanceDto>(clerk, $"/api/v1/finance/invoices/{invoice.Id}/submit", null);
        Assert.Equal(new[] { "PM_CONFIRM", "FIN_CERTIFY" }, certWf.Steps.Select(s => s.Code));
        Assert.Equal("Approved", (await _cast.ApproveAllAsync(certWf.Id)).State);
        var certified = await Get<InvoiceDto>(finance, $"/api/v1/finance/invoices/{invoice.Id}");
        Assert.Contains(certified.Status, new[] { "Certified", "SubmittedToErp" });

        // ---- ERP payment batch (API key, control totals) ----
        var erp = _factory.CreateClient();
        erp.DefaultRequestHeaders.Add("X-Api-Key", "integration-test-erp-api-key-0123456789abcdef");
        var batch = await Post<ErpBatchResult>(erp, "/api/v1/integration/erp/batches", new ErpBatchRequest(Guid.NewGuid(), ErpMessageTypes.Payment, 1, 230_000m,
            new[] { new ErpBatchItem("PAY-0001", certified.Number, null, "PO-7788", null, 230_000m, today, "Paid", null, "EFT-1", "Payment") }));
        Assert.True(batch.Balanced, string.Join("; ", batch.ErrorMessages));
        var badKey = _factory.CreateClient();
        badKey.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key-wrong-key-wrong-key-wrong-key-00");
        Assert.Equal(HttpStatusCode.Unauthorized, (await badKey.PostAsJsonAsync("/api/v1/integration/erp/batches", new { }, TetaApiFactory.Json)).StatusCode);

        var paidInvoices = await Get<List<PortalInvoiceDto>>(supplier, "/api/v1/portal/invoices");
        Assert.Equal("Paid", paidInvoices.Single(i => i.Id == invoice.Id).Status);

        // ---- Another supplier's portal user sees nothing of this contract ----
        var otherUser = _factory.CreateUser("p2p.supplier2", Roles.Supplier);
        using (var db = _factory.CreateContext())
        {
            db.SupplierUsers.Add(new SupplierUser { SupplierId = s2.Id, UserId = otherUser, LinkedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var other = await _factory.ClientForAsync("p2p.supplier2");
        Assert.Empty(await Get<List<PortalContractDto>>(other, "/api/v1/portal/contracts"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/documents/{signed.Id}/content")).StatusCode);
        var otherBids = await Get<List<PortalBidDto>>(other, "/api/v1/portal/bids");
        Assert.Equal("Unsuccessful", otherBids.Single().Outcome);

        // ---- Reports reflect the transaction; export is audited ----
        var exec = await _cast.ExecAsync();
        var register = await Get<ReportTable>(exec, "/api/v1/reports/run/RPT-005");
        Assert.Contains(register.Rows, r => Equals(r[0]?.ToString(), active.Summary.ContractNumber));
        var export = await exec.GetAsync("/api/v1/reports/run/RPT-008/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);

        // ---- Audit trail is complete and intact (NFR-015, SEC-010) ----
        var auditor = await _cast.AsAsync(Roles.InternalAudit);
        var trail = await Get<EntityTrailDto>(auditor, $"/api/v1/audit/trail/Invoice/{invoice.Id}");
        Assert.Contains(trail.Steps, s => s.Source == "Workflow" && s.Description.Contains("FIN_CERTIFY") == false && s.Description.Contains("Finance certification"));
        var integrity = await Post<IntegrityReportDto>(auditor, "/api/v1/audit/verify", null);
        Assert.True(integrity.Intact, $"Invalid audit seals: {string.Join(",", integrity.InvalidIds)}");
        var exports = await Get<Platform.Core.PagedResult<AuditLogDto>>(auditor, "/api/v1/audit?action=Export&entityType=Report");
        Assert.Contains(exports.Items, a => a.EntityId == "RPT-008");
    }

    private static async Task<DocumentDto> UploadAsync(HttpClient client, string url, string parentType, Guid parentId, string documentType, string fileName,
        string content, string? evidenceType = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "File", fileName);
        form.Add(new StringContent(parentType), "ParentType");
        form.Add(new StringContent(parentId.ToString()), "ParentId");
        form.Add(new StringContent(documentType), "DocumentType");
        form.Add(new StringContent("Internal"), "Classification");
        if (evidenceType is not null) form.Add(new StringContent(evidenceType), "EvidenceType");
        var response = await client.PostAsync(url, form);
        if (evidenceType is null) return await TetaApiFactory.ReadAsync<DocumentDto>(response);
        var evidence = await TetaApiFactory.ReadAsync<EvidenceDto>(response);
        return new DocumentDto(evidence.DocumentId, parentType, parentId, null, documentType, fileName, fileName, "application/pdf", content.Length, 1, null, true,
            "Internal", evidence.Sha256, null, "NotRequired", null, null, DateTime.UtcNow);
    }
}
