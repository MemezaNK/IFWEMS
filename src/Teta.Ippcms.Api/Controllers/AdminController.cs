using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Admin;
using Teta.Ippcms.Application.Audit;
using Teta.Ippcms.Application.Security;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Configuration administration: workflows, delegations, substitutions, SoD, reference data, calendar, settings, templates, retention (SRS §5.11).</summary>
[Route("api/v1/admin")]
public sealed class AdminController : TetaControllerBase
{
    private readonly IAdminService _admin;
    private readonly IImportService _import;

    public AdminController(IAdminService admin, IImportService import)
    {
        _admin = admin;
        _import = import;
    }

    public sealed record ActivateRequest(bool Approve, string? Comment);

    [HttpGet("workflows"), HasPermission(Permissions.AdminWorkflow, Permissions.AdminConfigApprove)]
    public Task<IReadOnlyList<WorkflowDefinitionDto>> Workflows(CancellationToken ct) => _admin.ListWorkflowsAsync(ct);

    [HttpPost("workflows"), HasPermission(Permissions.AdminWorkflow)]
    public Task<WorkflowDefinitionDto> CreateWorkflow(SaveWorkflowDefinitionRequest request, CancellationToken ct) => _admin.SaveWorkflowAsync(null, request, ct);

    [HttpPut("workflows/{id:guid}"), HasPermission(Permissions.AdminWorkflow)]
    public Task<WorkflowDefinitionDto> UpdateWorkflow(Guid id, SaveWorkflowDefinitionRequest request, CancellationToken ct) => _admin.SaveWorkflowAsync(id, request, ct);

    [HttpPost("workflows/{id:guid}/submit"), HasPermission(Permissions.AdminWorkflow)]
    public Task<WorkflowDefinitionDto> SubmitWorkflow(Guid id, CancellationToken ct) => _admin.SubmitWorkflowAsync(id, ct);

    [HttpPost("workflows/{id:guid}/activate"), HasPermission(Permissions.AdminConfigApprove)]
    public Task<WorkflowDefinitionDto> ActivateWorkflow(Guid id, ActivateRequest request, CancellationToken ct) => _admin.ActivateWorkflowAsync(id, request.Approve, request.Comment, ct);

    [HttpGet("delegations")]
    public Task<IReadOnlyList<DelegationDto>> Delegations(CancellationToken ct) => _admin.ListDelegationsAsync(ct);

    [HttpPost("delegations"), HasPermission(Permissions.AdminDelegations)]
    public Task<DelegationDto> CreateDelegation(SaveDelegationRequest request, CancellationToken ct) => _admin.SaveDelegationAsync(null, request, ct);

    [HttpPut("delegations/{id:guid}"), HasPermission(Permissions.AdminDelegations)]
    public Task<DelegationDto> UpdateDelegation(Guid id, SaveDelegationRequest request, CancellationToken ct) => _admin.SaveDelegationAsync(id, request, ct);

    [HttpGet("delegations/my-limit")]
    public async Task<object> MyLimit([FromQuery] string authorityType, CancellationToken ct) => new { limit = await _admin.MyLimitAsync(authorityType, ct) };

    [HttpGet("substitutions")]
    public Task<IReadOnlyList<SubstitutionDto>> Substitutions([FromQuery] bool mineOnly = true, CancellationToken ct = default) => _admin.ListSubstitutionsAsync(mineOnly, ct);

    [HttpPost("substitutions"), HasPermission(Permissions.WorkflowDecide, Permissions.AdminDelegations)]
    public Task<SubstitutionDto> CreateSubstitution(SaveSubstitutionRequest request, CancellationToken ct) => _admin.SaveSubstitutionAsync(request, ct);

    [HttpPost("substitutions/{id:guid}/revoke"), HasPermission(Permissions.WorkflowDecide, Permissions.AdminDelegations)]
    public Task<SubstitutionDto> RevokeSubstitution(Guid id, CancellationToken ct) => _admin.RevokeSubstitutionAsync(id, ct);

    [HttpGet("sod-rules"), HasPermission(Permissions.AdminConfig, Permissions.AuditRead)]
    public Task<IReadOnlyList<SodRuleDto>> SodRules(CancellationToken ct) => _admin.ListSodRulesAsync(ct);

    [HttpPost("sod-rules"), HasPermission(Permissions.AdminConfig)]
    public Task<SodRuleDto> CreateSodRule(SaveSodRuleRequest request, CancellationToken ct) => _admin.SaveSodRuleAsync(null, request, ct);

    [HttpPut("sod-rules/{id:guid}"), HasPermission(Permissions.AdminConfig)]
    public Task<SodRuleDto> UpdateSodRule(Guid id, SaveSodRuleRequest request, CancellationToken ct) => _admin.SaveSodRuleAsync(id, request, ct);

    /// <summary>Reference lists are readable by every signed-in user (drop-downs).</summary>
    [HttpGet("reference-data")]
    public Task<IReadOnlyList<ReferenceItemDto>> ReferenceData([FromQuery] string? category, [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        _admin.ListReferenceDataAsync(category, activeOnly, ct);

    [HttpPost("reference-data"), HasPermission(Permissions.AdminConfig)]
    public Task<ReferenceItemDto> CreateReferenceItem(SaveReferenceItemRequest request, CancellationToken ct) => _admin.SaveReferenceItemAsync(null, request, ct);

    [HttpPut("reference-data/{id:guid}"), HasPermission(Permissions.AdminConfig)]
    public Task<ReferenceItemDto> UpdateReferenceItem(Guid id, SaveReferenceItemRequest request, CancellationToken ct) => _admin.SaveReferenceItemAsync(id, request, ct);

    [HttpGet("holidays")]
    public Task<IReadOnlyList<HolidayDto>> Holidays([FromQuery] int? year, CancellationToken ct) => _admin.ListHolidaysAsync(year, ct);

    [HttpPost("holidays"), HasPermission(Permissions.AdminConfig)]
    public Task<HolidayDto> CreateHoliday(SaveHolidayRequest request, CancellationToken ct) => _admin.SaveHolidayAsync(null, request, ct);

    [HttpPut("holidays/{id:guid}"), HasPermission(Permissions.AdminConfig)]
    public Task<HolidayDto> UpdateHoliday(Guid id, SaveHolidayRequest request, CancellationToken ct) => _admin.SaveHolidayAsync(id, request, ct);

    [HttpDelete("holidays/{id:guid}"), HasPermission(Permissions.AdminConfig)]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        await _admin.DeleteHolidayAsync(id, ct);
        return NoContent();
    }

    [HttpGet("settings"), HasPermission(Permissions.AdminConfig, Permissions.AuditRead)]
    public Task<IReadOnlyList<SettingDto>> Settings(CancellationToken ct) => _admin.ListSettingsAsync(ct);

    [HttpPut("settings/{key}"), HasPermission(Permissions.AdminConfig)]
    public Task<SettingDto> SaveSetting(string key, SaveSettingRequest request, CancellationToken ct) => _admin.SaveSettingAsync(key, request, ct);

    [HttpGet("notification-templates"), HasPermission(Permissions.AdminConfig)]
    public Task<IReadOnlyList<TemplateDto>> Templates(CancellationToken ct) => _admin.ListTemplatesAsync(ct);

    [HttpPut("notification-templates/{code}"), HasPermission(Permissions.AdminConfig)]
    public Task<TemplateDto> SaveTemplate(string code, SaveTemplateRequest request, CancellationToken ct) => _admin.SaveTemplateAsync(code, request, ct);

    [HttpGet("retention"), HasPermission(Permissions.AdminConfig, Permissions.AuditRead)]
    public Task<IReadOnlyList<RetentionDto>> Retention(CancellationToken ct) => _admin.ListRetentionAsync(ct);

    [HttpPost("retention"), HasPermission(Permissions.AdminConfig)]
    public Task<RetentionDto> CreateRetention(SaveRetentionRequest request, CancellationToken ct) => _admin.SaveRetentionAsync(null, request, ct);

    [HttpPut("retention/{id:guid}"), HasPermission(Permissions.AdminConfig)]
    public Task<RetentionDto> UpdateRetention(Guid id, SaveRetentionRequest request, CancellationToken ct) => _admin.SaveRetentionAsync(id, request, ct);

    // ----- Bulk import (FR-ADM-009) -----
    [HttpGet("imports/templates"), HasPermission(Permissions.AdminImport)]
    public IReadOnlyList<ImportTemplateDto> ImportTemplates() => _import.Templates();

    [HttpGet("imports"), HasPermission(Permissions.AdminImport)]
    public Task<IReadOnlyList<ImportJobDto>> Imports(CancellationToken ct) => _import.ListJobsAsync(ct);

    [HttpGet("imports/{id:guid}"), HasPermission(Permissions.AdminImport)]
    public Task<ImportResultDto> Import(Guid id, CancellationToken ct) => _import.GetJobAsync(id, ct);

    [HttpPost("imports/{importType}"), HasPermission(Permissions.AdminImport), RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ImportResultDto> RunImport(string importType, IFormFile file, [FromQuery] bool validateOnly = true, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        return await _import.ImportAsync(importType, file.FileName, stream, validateOnly, ct);
    }
}

/// <summary>Identity and access administration with dual control (SRS §10, SEC-003/004/009).</summary>
[Route("api/v1/security")]
public sealed class SecurityController : TetaControllerBase
{
    private readonly IUserAdminService _users;
    private readonly IMemoryCache _cache;

    public SecurityController(IUserAdminService users, IMemoryCache cache)
    {
        _users = users;
        _cache = cache;
    }

    public sealed record SetActiveRequest(bool Active, string Reason);

    [HttpGet("users"), HasPermission(Permissions.SecurityUsers, Permissions.AuditRead)]
    public Task<PagedResult<UserSummaryDto>> Users([FromQuery] string? search, [FromQuery] bool tetaOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) => _users.ListUsersAsync(search, tetaOnly, page, pageSize, ct);

    /// <summary>User picker for owners/approvers (display name + role only).</summary>
    [HttpGet("users/lookup")]
    public Task<IReadOnlyList<UserLookupDto>> Lookup([FromQuery] string? search, [FromQuery] string? roleCode, CancellationToken ct) => _users.LookupAsync(search, roleCode, ct);

    [HttpPost("users"), HasPermission(Permissions.SecurityUsers)]
    public Task<UserSummaryDto> CreateUser(CreateUserRequest request, CancellationToken ct) => _users.CreateUserAsync(request, ct);

    [HttpPost("users/{id:guid}/active"), HasPermission(Permissions.SecurityUsers)]
    public async Task<UserSummaryDto> SetActive(Guid id, SetActiveRequest request, CancellationToken ct)
    {
        var result = await _users.SetActiveAsync(id, request.Active, request.Reason, ct);
        TetaClaimsTransformation.Invalidate(_cache, id);
        return result;
    }

    [HttpPost("users/{id:guid}/unlock"), HasPermission(Permissions.SecurityUsers)]
    public Task<UserSummaryDto> Unlock(Guid id, CancellationToken ct) => _users.UnlockAsync(id, ct);

    [HttpPost("assignments"), HasPermission(Permissions.SecurityUsers)]
    public Task<AssignmentDto> RequestAssignment(RequestAssignmentRequest request, CancellationToken ct) => _users.RequestAssignmentAsync(request, ct);

    [HttpGet("assignments/pending"), HasPermission(Permissions.SecurityUsers, Permissions.SecurityRolesApprove)]
    public Task<IReadOnlyList<AssignmentDto>> Pending(CancellationToken ct) => _users.PendingAssignmentsAsync(ct);

    [HttpPost("assignments/{id:guid}/decision"), HasPermission(Permissions.SecurityRolesApprove)]
    public async Task<AssignmentDto> Decide(Guid id, DecideAssignmentRequest request, CancellationToken ct)
    {
        var result = await _users.DecideAssignmentAsync(id, request, ct);
        TetaClaimsTransformation.Invalidate(_cache, result.UserId);
        return result;
    }

    [HttpPost("assignments/{id:guid}/revoke"), HasPermission(Permissions.SecurityUsers)]
    public async Task<AssignmentDto> Revoke(Guid id, ReasonRequest request, CancellationToken ct)
    {
        var result = await _users.RevokeAssignmentAsync(id, request.Reason, ct);
        TetaClaimsTransformation.Invalidate(_cache, result.UserId);
        return result;
    }

    [HttpGet("roles"), HasPermission(Permissions.SecurityUsers, Permissions.SecurityRolesApprove, Permissions.AuditRead)]
    public Task<IReadOnlyList<RoleDto>> Roles(CancellationToken ct) => _users.ListRolesAsync(ct);

    [HttpGet("permissions"), HasPermission(Permissions.SecurityUsers, Permissions.SecurityRolesApprove, Permissions.AuditRead)]
    public IReadOnlyList<string> AllPermissions() => Permissions.All;

    [HttpPut("roles/{code}/permissions"), HasPermission(Permissions.SecurityRolesApprove)]
    public Task<RoleDto> SaveRolePermissions(string code, SaveRolePermissionsRequest request, CancellationToken ct) => _users.SaveRolePermissionsAsync(code, request, ct);

    [HttpGet("sessions"), HasPermission(Permissions.SecurityUsers)]
    public Task<IReadOnlyList<SessionDto>> Sessions([FromQuery] Guid? userId, CancellationToken ct) => _users.ListSessionsAsync(userId, ct);

    [HttpPost("sessions/{id:guid}/revoke"), HasPermission(Permissions.SecurityUsers)]
    public async Task<IActionResult> RevokeSession(Guid id, ReasonRequest request, CancellationToken ct)
    {
        await _users.RevokeSessionAsync(id, request.Reason, ct);
        return NoContent();
    }
}

/// <summary>Read-only audit trail, integrity verification and end-to-end transaction reconstruction (SRS §14, NFR-015).</summary>
[Route("api/v1/audit")]
[HasPermission(Permissions.AuditRead)]
public sealed class AuditController : TetaControllerBase
{
    private readonly IAuditQueryService _audit;

    public AuditController(IAuditQueryService audit) => _audit = audit;

    [HttpGet]
    public Task<PagedResult<AuditLogDto>> Search([FromQuery] AuditQuery query, CancellationToken ct) => _audit.SearchAsync(query, ct);

    [HttpPost("verify")]
    public Task<IntegrityReportDto> Verify([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct) => _audit.VerifyIntegrityAsync(fromUtc, toUtc, ct);

    [HttpGet("trail/{entityType}/{entityId:guid}")]
    public Task<EntityTrailDto> Trail(string entityType, Guid entityId, CancellationToken ct) => _audit.EntityTrailAsync(entityType, entityId, ct);
}
