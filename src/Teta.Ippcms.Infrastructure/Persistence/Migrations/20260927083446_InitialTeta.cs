using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teta.Ippcms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "teta");

            migrationBuilder.CreateTable(
                name: "AssuranceCoverage",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Line = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Covered = table.Column<bool>(type: "bit", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceCoverage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditFindings",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Process = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AuditReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ManagementResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionOwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionOwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditFindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowStep = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Seal = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoardReportPacks",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PackVersion = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DatasetJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardReportPacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeMeetings",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Agenda = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Minutes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttendeesCount = table.Column<int>(type: "int", nullable: false),
                    QuorumMet = table.Column<bool>(type: "bit", nullable: false),
                    Resolutions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMeetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Committees",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quorum = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Committees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceObligations",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceObligations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClosureNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    LastEscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataQualityIssues",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataQualityIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Delegations",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PolicyReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DocumentVersion = table.Column<int>(type: "int", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsLatest = table.Column<bool>(type: "bit", nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RepositoryUri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SignatureStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetentionClass = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErpInterfaceMessages",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ControlAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpInterfaceMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErpReconciliations",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    ExpectedTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProcessedTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balanced = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpReconciliations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationEvents",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    EscalatedToRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportJobs",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    SucceededRows = table.Column<int>(type: "int", nullable: false),
                    FailedRows = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidateOnly = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringTemplates",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceEntityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    NextValue = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricePreferenceSystems",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PricePoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreferencePoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApplicableFromValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApplicableToValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PolicyReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricePreferenceSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrioritisationCriteria",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxScore = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrioritisationCriteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementExceptions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Motivation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Authority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementExceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementMethodRules",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    RuleVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequiresPublication = table.Column<bool>(type: "bit", nullable: false),
                    MinimumQuotations = table.Column<int>(type: "int", nullable: false),
                    MinimumAdvertDays = table.Column<int>(type: "int", nullable: false),
                    ApprovalAuthority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementMethodRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHealthSnapshots",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Overall = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleScore = table.Column<int>(type: "int", nullable: false),
                    CostScore = table.Column<int>(type: "int", nullable: false),
                    RiskScore = table.Column<int>(type: "int", nullable: false),
                    DeliveryScore = table.Column<int>(type: "int", nullable: false),
                    ScheduleVarianceDays = table.Column<int>(type: "int", nullable: false),
                    CostVariancePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CriticalOpenRisks = table.Column<int>(type: "int", nullable: false),
                    HighOpenRisks = table.Column<int>(type: "int", nullable: false),
                    OverdueMilestones = table.Column<int>(type: "int", nullable: false),
                    OverdueCriticalMilestones = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceData",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recipients = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionPolicies",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordClass = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RetentionYears = table.Column<int>(type: "int", nullable: false),
                    DisposalAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LegalReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskRatingBands",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinScore = table.Column<int>(type: "int", nullable: false),
                    MaxScore = table.Column<int>(type: "int", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskRatingBands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Risks",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cause = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Event = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Consequence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InherentLikelihood = table.Column<int>(type: "int", nullable: false),
                    InherentImpact = table.Column<int>(type: "int", nullable: false),
                    InherentScore = table.Column<int>(type: "int", nullable: false),
                    InherentRating = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResidualLikelihood = table.Column<int>(type: "int", nullable: false),
                    ResidualImpact = table.Column<int>(type: "int", nullable: false),
                    ResidualScore = table.Column<int>(type: "int", nullable: false),
                    ResidualRating = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Risks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrivileged = table.Column<bool>(type: "bit", nullable: false),
                    HasApprovalAuthority = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SodRules",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SodRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StageGateCriteria",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CheckCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageGateCriteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategicPlans",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Substitutions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubstituteUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubstituteName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Substitutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TradingName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CsdNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VatNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BbbeeLevel = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Province = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsImplementingPartner = table.Column<bool>(type: "bit", nullable: false),
                    CsdVerified = table.Column<bool>(type: "bit", nullable: false),
                    TaxClearanceExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    ErpVendorCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierUsers",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionActions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSecurityProfiles",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MfaSecretProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MfaEnrolledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSecurityProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MfaSatisfied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeMembers",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccessFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    AccessTo = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_Committees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalSchema: "teta",
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAttestations",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttestedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAttestations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceAttestations_ComplianceObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalSchema: "teta",
                        principalTable: "ComplianceObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Evidence",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerificationComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evidence_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "teta",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Programmes",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programmes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Programmes_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalSchema: "teta",
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskControls",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Effectiveness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastAssessedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskControls_Risks_RiskId",
                        column: x => x.RiskId,
                        principalSchema: "teta",
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskTreatments",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    LastEscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTreatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskTreatments_Risks_RiskId",
                        column: x => x.RiskId,
                        principalSchema: "teta",
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StrategicOutcomes",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicOutcomes_StrategicPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "teta",
                        principalTable: "StrategicPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StrategicPlanVersions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicPlanVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicPlanVersions_StrategicPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "teta",
                        principalTable: "StrategicPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentStepOrder = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StepsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalSchema: "teta",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepDefinitions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinimumValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SlaHours = table.Column<int>(type: "int", nullable: false),
                    EscalationRole = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepDefinitions_WorkflowDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalSchema: "teta",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProgrammeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SponsorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SponsorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrgUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjectType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Province = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    District = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Municipality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedBudget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Health = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HealthExplanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HealthCalculatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriorityScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Programmes_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalSchema: "teta",
                        principalTable: "Programmes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlAssessments",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Effectiveness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssessedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlAssessments_RiskControls_ControlId",
                        column: x => x.ControlId,
                        principalSchema: "teta",
                        principalTable: "RiskControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StrategicObjectives",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProgrammeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicObjectives_StrategicOutcomes_OutcomeId",
                        column: x => x.OutcomeId,
                        principalSchema: "teta",
                        principalTable: "StrategicOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StrategicObjectives_StrategicPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "teta",
                        principalTable: "StrategicPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTasks",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    StepCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedRole = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OnBehalfOfUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OnBehalfOfName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedToRole = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTasks_WorkflowInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalSchema: "teta",
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Accruals",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accruals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accruals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Beneficiaries",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentifierEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentifierHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdentifierMasked = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentifierType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BirthYear = table.Column<int>(type: "int", nullable: true),
                    Province = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    District = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Intervention = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FundingSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PotentialDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    DuplicateNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsentObtained = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Beneficiaries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BenefitReviews",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedBenefit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualBenefit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActualValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenefitReviews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLines",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CostCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FundingSource = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RevisedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ForecastAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCarryOver = table.Column<bool>(type: "bit", nullable: false),
                    CarryOverFromYear = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBaselined = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessCases",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Problem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Objectives = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Options = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Benefits = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Risks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryModel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedBenefitMeasure = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedBenefitValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessCases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CostImpact = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScheduleImpactDays = table.Column<int>(type: "int", nullable: false),
                    ProposedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BenefitImpact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContractImpact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultingBaselineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultingVariationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Commitments",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PoReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinancialYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommitmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsReleased = table.Column<bool>(type: "bit", nullable: false),
                    ErpReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Commitments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostForecasts",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BudgetAtCompletion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimateAtCompletion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VariancePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Commentary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostForecasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostForecasts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expenditures",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinancialYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GlAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErpReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenditures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenditures_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RootCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    LastEscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MePlans",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Methods = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Indicators = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsibleOfficerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsibleOfficerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextVisitDue = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MePlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringVisits",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateVersion = table.Column<int>(type: "int", nullable: true),
                    TemplateFieldsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Officials = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    ResponsesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringVisits_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPlanItems",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PlannedMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlannedRequisitionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedAdvertDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedAwardDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualRequisitionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualAdvertDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualAwardDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VarianceReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrgUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPlanItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCharters",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GovernanceStructure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyMilestones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Assumptions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCharters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCharters_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectClosures",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonsLearned = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HandoverNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinancialReconciliationConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    DocumentationComplete = table.Column<bool>(type: "bit", nullable: false),
                    ChecklistJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedExceptionReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectClosures_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDependencies",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DependsOn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Impact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NeededBy = table.Column<DateOnly>(type: "date", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDependencies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPriorityScores",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScoredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScoredBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPriorityScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPriorityScores_PrioritisationCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalSchema: "teta",
                        principalTable: "PrioritisationCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPriorityScores_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStakeholders",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Organisation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Responsibility = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStakeholders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStakeholders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatusHistory",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ToStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStatusHistory_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleBaselines",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaselineNumber = table.Column<int>(type: "int", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    BudgetAtBaseline = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleBaselines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StageGateReviews",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageGateReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageGateReviews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WbsElements",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    BaselineStart = table.Column<DateOnly>(type: "date", nullable: true),
                    BaselineEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ForecastEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    PercentComplete = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenceRequired = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WbsElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WbsElements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WbsElements_WbsElements_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "teta",
                        principalTable: "WbsElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Workstreams",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lead = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workstreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workstreams_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppIndicators",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Measure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceRule = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredEvidenceTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponsibleExecutive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsCumulative = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppIndicators_StrategicObjectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalSchema: "teta",
                        principalTable: "StrategicObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BeneficiaryStatusHistory",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficiaryStatusHistory_Beneficiaries_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalSchema: "teta",
                        principalTable: "Beneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetRevisions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousRevised = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewRevised = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevisedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevisedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetRevisions_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalSchema: "teta",
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Requisitions",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiredByDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecommendedMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SelectedMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MethodJustification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MethodRuleVersion = table.Column<int>(type: "int", nullable: true),
                    BudgetAvailable = table.Column<bool>(type: "bit", nullable: false),
                    BudgetCheckResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requisitions_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalSchema: "teta",
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requisitions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StageGateChecks",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriterionDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsMet = table.Column<bool>(type: "bit", nullable: false),
                    AutoEvaluated = table.Column<bool>(type: "bit", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageGateChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageGateChecks_StageGateReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalSchema: "teta",
                        principalTable: "StageGateReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgressUpdates",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WbsElementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PercentComplete = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ForecastEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressUpdates_WbsElements_WbsElementId",
                        column: x => x.WbsElementId,
                        principalSchema: "teta",
                        principalTable: "WbsElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceAssignments",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WbsElementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResourceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsExternal = table.Column<bool>(type: "bit", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllocationPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceAssignments_WbsElements_WbsElementId",
                        column: x => x.WbsElementId,
                        principalSchema: "teta",
                        principalTable: "WbsElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WbsDependencies",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredecessorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuccessorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LagDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WbsDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WbsDependencies_WbsElements_PredecessorId",
                        column: x => x.PredecessorId,
                        principalSchema: "teta",
                        principalTable: "WbsElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WbsDependencies_WbsElements_SuccessorId",
                        column: x => x.SuccessorId,
                        principalSchema: "teta",
                        principalTable: "WbsElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppTargets",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quarter = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ForecastValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ForecastCommentary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTargets_AppIndicators_IndicatorId",
                        column: x => x.IndicatorId,
                        principalSchema: "teta",
                        principalTable: "AppIndicators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceResults",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quarter = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerificationComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceResults_AppIndicators_IndicatorId",
                        column: x => x.IndicatorId,
                        principalSchema: "teta",
                        principalTable: "AppIndicators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceResults_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIndicatorLinks",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PlannedContribution = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIndicatorLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectIndicatorLinks_AppIndicators_IndicatorId",
                        column: x => x.IndicatorId,
                        principalSchema: "teta",
                        principalTable: "AppIndicators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectIndicatorLinks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Procurements",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MethodRuleVersion = table.Column<int>(type: "int", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClosingDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BidOpeningAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicalThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceSystemCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReAdvertisedFromId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReAdvertisedAsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlannedAwardDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualAwardDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Procurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Procurements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Procurements_Requisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalSchema: "teta",
                        principalTable: "Requisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Adjudications",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecommendedBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDiligenceCheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Conditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reasons = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjudications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adjudications_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BidderCommunications",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidderCommunications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidderCommunications_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    BidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvalidReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplianceMet = table.Column<bool>(type: "bit", nullable: true),
                    TechnicalScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PricePoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PreferencePoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SpecificGoalsClaimed = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalPoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    SubmissionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bids_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bids_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "teta",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Declarations",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeclarantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasConflict = table.Column<bool>(type: "bit", nullable: false),
                    ConflictDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfidentialityAccepted = table.Column<bool>(type: "bit", nullable: false),
                    DeclaredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Declarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Declarations_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationCriteria",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationCriteria_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosingDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BriefingSession = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publications_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Specifications",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Specifications_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Awards",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AwardDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Conditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConditionsSatisfied = table.Column<bool>(type: "bit", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Awards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Awards_Bids_BidId",
                        column: x => x.BidId,
                        principalSchema: "teta",
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Awards_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalSchema: "teta",
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Awards_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "teta",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DueDiligenceChecks",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CsdVerified = table.Column<bool>(type: "bit", nullable: false),
                    TaxCompliant = table.Column<bool>(type: "bit", nullable: false),
                    NotRestricted = table.Column<bool>(type: "bit", nullable: false),
                    NotOnDefaultersList = table.Column<bool>(type: "bit", nullable: false),
                    ReferencesChecked = table.Column<bool>(type: "bit", nullable: false),
                    CapacityConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DueDiligenceChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DueDiligenceChecks_Bids_BidId",
                        column: x => x.BidId,
                        principalSchema: "teta",
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationScores",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Passed = table.Column<bool>(type: "bit", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScoredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationScores_Bids_BidId",
                        column: x => x.BidId,
                        principalSchema: "teta",
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationScores_EvaluationCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalSchema: "teta",
                        principalTable: "EvaluationCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AwardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NonBidAuthority = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedVariations = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContractManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractManagerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignatureStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SignedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PoReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformanceRating = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosureNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosureExceptionReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Awards_AwardId",
                        column: x => x.AwardId,
                        principalSchema: "teta",
                        principalTable: "Awards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "teta",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractBreaches",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IdentifiedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    NoticeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NoticeReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remedy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RemedyDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractBreaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractBreaches_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "teta",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractObligations",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EvidenceRequirement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KpiTarget = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractObligations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "teta",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractPerformanceReviews",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    QualityScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TimelinessScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ComplianceScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rating = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractPerformanceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractPerformanceReviews_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "teta",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractVariations",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsExtension = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    RevisedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValueBefore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EndDateBefore = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDateAfter = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractVariations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractVariations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "teta",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Deliverables",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenceRequired = table.Column<bool>(type: "bit", nullable: false),
                    AcceptanceStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliverables_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "teta",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deliverables_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentScheduleItems",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentScheduleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentScheduleItems_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "teta",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupplierInvoiceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PoReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PotentialDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    ValidationMessages = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "teta",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Deliverables_DeliverableId",
                        column: x => x.DeliverableId,
                        principalSchema: "teta",
                        principalTable: "Deliverables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "teta",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "teta",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErpReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "teta",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accruals_ProjectId",
                schema: "teta",
                table: "Accruals",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjudications_ProcurementId",
                schema: "teta",
                table: "Adjudications",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_AppIndicators_ObjectiveId_Code",
                schema: "teta",
                table: "AppIndicators",
                columns: new[] { "ObjectiveId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTargets_IndicatorId_FinancialYear_Quarter",
                schema: "teta",
                table: "AppTargets",
                columns: new[] { "IndicatorId", "FinancialYear", "Quarter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_Number",
                schema: "teta",
                table: "AuditFindings",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_OccurredAtUtc",
                schema: "teta",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                schema: "teta",
                table: "AuditLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                schema: "teta",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Awards_BidId",
                schema: "teta",
                table: "Awards",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_Awards_Number",
                schema: "teta",
                table: "Awards",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Awards_ProcurementId",
                schema: "teta",
                table: "Awards",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_Awards_SupplierId",
                schema: "teta",
                table: "Awards",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_IdentifierHash",
                schema: "teta",
                table: "Beneficiaries",
                column: "IdentifierHash");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_Number",
                schema: "teta",
                table: "Beneficiaries",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_ProjectId",
                schema: "teta",
                table: "Beneficiaries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryStatusHistory_BeneficiaryId",
                schema: "teta",
                table: "BeneficiaryStatusHistory",
                column: "BeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitReviews_ProjectId",
                schema: "teta",
                table: "BenefitReviews",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BidderCommunications_ProcurementId",
                schema: "teta",
                table: "BidderCommunications",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_ProcurementId_SupplierId",
                schema: "teta",
                table: "Bids",
                columns: new[] { "ProcurementId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_SupplierId",
                schema: "teta",
                table: "Bids",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardReportPacks_Number",
                schema: "teta",
                table: "BoardReportPacks",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_ProjectId_FinancialYear",
                schema: "teta",
                table: "BudgetLines",
                columns: new[] { "ProjectId", "FinancialYear" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRevisions_BudgetLineId",
                schema: "teta",
                table: "BudgetRevisions",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCases_ProjectId",
                schema: "teta",
                table: "BusinessCases",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_Number",
                schema: "teta",
                table: "ChangeRequests",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_ProjectId",
                schema: "teta",
                table: "ChangeRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentType_ParentId",
                schema: "teta",
                table: "Comments",
                columns: new[] { "ParentType", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Commitments_ProjectId",
                schema: "teta",
                table: "Commitments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_CommitteeId_UserId",
                schema: "teta",
                table: "CommitteeMembers",
                columns: new[] { "CommitteeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAttestations_ObligationId",
                schema: "teta",
                table: "ComplianceAttestations",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceObligations_Code",
                schema: "teta",
                table: "ComplianceObligations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractBreaches_ContractId",
                schema: "teta",
                table: "ContractBreaches",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractObligations_ContractId",
                schema: "teta",
                table: "ContractObligations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractPerformanceReviews_ContractId",
                schema: "teta",
                table: "ContractPerformanceReviews",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_AwardId",
                schema: "teta",
                table: "Contracts",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNumber",
                schema: "teta",
                table: "Contracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProjectId",
                schema: "teta",
                table: "Contracts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status_CurrentEndDate",
                schema: "teta",
                table: "Contracts",
                columns: new[] { "Status", "CurrentEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_SupplierId",
                schema: "teta",
                table: "Contracts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractVariations_ContractId",
                schema: "teta",
                table: "ContractVariations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractVariations_Number",
                schema: "teta",
                table: "ContractVariations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlAssessments_ControlId",
                schema: "teta",
                table: "ControlAssessments",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_Number",
                schema: "teta",
                table: "CorrectiveActions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_ParentType_ParentId",
                schema: "teta",
                table: "CorrectiveActions",
                columns: new[] { "ParentType", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostForecasts_ProjectId",
                schema: "teta",
                table: "CostForecasts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DataQualityIssues_RuleCode_EntityId",
                schema: "teta",
                table: "DataQualityIssues",
                columns: new[] { "RuleCode", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_ProcurementId_UserId",
                schema: "teta",
                table: "Declarations",
                columns: new[] { "ProcurementId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ContractId",
                schema: "teta",
                table: "Deliverables",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_Number",
                schema: "teta",
                table: "Deliverables",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ProjectId",
                schema: "teta",
                table: "Deliverables",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ParentType_ParentId",
                schema: "teta",
                table: "Documents",
                columns: new[] { "ParentType", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DueDiligenceChecks_BidId",
                schema: "teta",
                table: "DueDiligenceChecks",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpInterfaceMessages_MessageType_ExternalReference",
                schema: "teta",
                table: "ErpInterfaceMessages",
                columns: new[] { "MessageType", "ExternalReference" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpInterfaceMessages_Status",
                schema: "teta",
                table: "ErpInterfaceMessages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCriteria_ProcurementId",
                schema: "teta",
                table: "EvaluationCriteria",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScores_BidId_CriterionId_EvaluatorUserId",
                schema: "teta",
                table: "EvaluationScores",
                columns: new[] { "BidId", "CriterionId", "EvaluatorUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScores_CriterionId",
                schema: "teta",
                table: "EvaluationScores",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_DocumentId",
                schema: "teta",
                table: "Evidence",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_ParentType_ParentId",
                schema: "teta",
                table: "Evidence",
                columns: new[] { "ParentType", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenditures_ErpReference",
                schema: "teta",
                table: "Expenditures",
                column: "ErpReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenditures_ProjectId",
                schema: "teta",
                table: "Expenditures",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_Number",
                schema: "teta",
                table: "Findings",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ProjectId",
                schema: "teta",
                table: "Findings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Key",
                schema: "teta",
                table: "IdempotencyRecords",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId",
                schema: "teta",
                table: "Invoices",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DeliverableId",
                schema: "teta",
                table: "Invoices",
                column: "DeliverableId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IdempotencyKey",
                schema: "teta",
                table: "Invoices",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                schema: "teta",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ProjectId",
                schema: "teta",
                table: "Invoices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SupplierId_SupplierInvoiceNumber",
                schema: "teta",
                table: "Invoices",
                columns: new[] { "SupplierId", "SupplierInvoiceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Number",
                schema: "teta",
                table: "Issues",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ProjectId",
                schema: "teta",
                table: "Issues",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MePlans_ProjectId",
                schema: "teta",
                table: "MePlans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringTemplates_Code_TemplateVersion",
                schema: "teta",
                table: "MonitoringTemplates",
                columns: new[] { "Code", "TemplateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringVisits_Number",
                schema: "teta",
                table: "MonitoringVisits",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringVisits_ProjectId",
                schema: "teta",
                table: "MonitoringVisits",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                schema: "teta",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Code",
                schema: "teta",
                table: "NotificationTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_Prefix_Year",
                schema: "teta",
                table: "NumberSequences",
                columns: new[] { "Prefix", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc",
                schema: "teta",
                table: "OutboxMessages",
                column: "ProcessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ErpReference",
                schema: "teta",
                table: "Payments",
                column: "ErpReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                schema: "teta",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentScheduleItems_ContractId",
                schema: "teta",
                table: "PaymentScheduleItems",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceResults_IndicatorId_FinancialYear_Quarter",
                schema: "teta",
                table: "PerformanceResults",
                columns: new[] { "IndicatorId", "FinancialYear", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceResults_ProjectId",
                schema: "teta",
                table: "PerformanceResults",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_Code",
                schema: "teta",
                table: "Portfolios",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricePreferenceSystems_Code_EffectiveFrom",
                schema: "teta",
                table: "PricePreferenceSystems",
                columns: new[] { "Code", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrioritisationCriteria_Code",
                schema: "teta",
                table: "PrioritisationCriteria",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptions_Number",
                schema: "teta",
                table: "ProcurementExceptions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMethodRules_MethodCode_RuleVersion",
                schema: "teta",
                table: "ProcurementMethodRules",
                columns: new[] { "MethodCode", "RuleVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_FinancialYear_ProjectId",
                schema: "teta",
                table: "ProcurementPlanItems",
                columns: new[] { "FinancialYear", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlanItems_ProjectId",
                schema: "teta",
                table: "ProcurementPlanItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_Number",
                schema: "teta",
                table: "Procurements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_ProjectId",
                schema: "teta",
                table: "Procurements",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_RequisitionId",
                schema: "teta",
                table: "Procurements",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_Code",
                schema: "teta",
                table: "Programmes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_PortfolioId",
                schema: "teta",
                table: "Programmes",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressUpdates_WbsElementId",
                schema: "teta",
                table: "ProgressUpdates",
                column: "WbsElementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCharters_ProjectId",
                schema: "teta",
                table: "ProjectCharters",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClosures_ProjectId",
                schema: "teta",
                table: "ProjectClosures",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDependencies_ProjectId",
                schema: "teta",
                table: "ProjectDependencies",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHealthSnapshots_ProjectId_CalculatedAtUtc",
                schema: "teta",
                table: "ProjectHealthSnapshots",
                columns: new[] { "ProjectId", "CalculatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIndicatorLinks_IndicatorId",
                schema: "teta",
                table: "ProjectIndicatorLinks",
                column: "IndicatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIndicatorLinks_ProjectId_IndicatorId",
                schema: "teta",
                table: "ProjectIndicatorLinks",
                columns: new[] { "ProjectId", "IndicatorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPriorityScores_CriterionId",
                schema: "teta",
                table: "ProjectPriorityScores",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPriorityScores_ProjectId_CriterionId",
                schema: "teta",
                table: "ProjectPriorityScores",
                columns: new[] { "ProjectId", "CriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DraftReference",
                schema: "teta",
                table: "Projects",
                column: "DraftReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProgrammeId_Status",
                schema: "teta",
                table: "Projects",
                columns: new[] { "ProgrammeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectNumber",
                schema: "teta",
                table: "Projects",
                column: "ProjectNumber",
                unique: true,
                filter: "[ProjectNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStakeholders_ProjectId",
                schema: "teta",
                table: "ProjectStakeholders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusHistory_ProjectId",
                schema: "teta",
                table: "ProjectStatusHistory",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_ProcurementId",
                schema: "teta",
                table: "Publications",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_Date",
                schema: "teta",
                table: "PublicHolidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceData_Category_Code",
                schema: "teta",
                table: "ReferenceData",
                columns: new[] { "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requisitions_BudgetLineId",
                schema: "teta",
                table: "Requisitions",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Requisitions_Number",
                schema: "teta",
                table: "Requisitions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requisitions_ProjectId",
                schema: "teta",
                table: "Requisitions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAssignments_WbsElementId",
                schema: "teta",
                table: "ResourceAssignments",
                column: "WbsElementId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_RecordClass",
                schema: "teta",
                table: "RetentionPolicies",
                column: "RecordClass",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskControls_RiskId",
                schema: "teta",
                table: "RiskControls",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_Number",
                schema: "teta",
                table: "Risks",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Risks_ParentType_ParentId",
                schema: "teta",
                table: "Risks",
                columns: new[] { "ParentType", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatments_RiskId",
                schema: "teta",
                table: "RiskTreatments",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleCode_Permission",
                schema: "teta",
                table: "RolePermissions",
                columns: new[] { "RoleCode", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                schema: "teta",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBaselines_ProjectId_BaselineNumber",
                schema: "teta",
                table: "ScheduleBaselines",
                columns: new[] { "ProjectId", "BaselineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SodRules_Code",
                schema: "teta",
                table: "SodRules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ProcurementId",
                schema: "teta",
                table: "Specifications",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_StageGateChecks_ReviewId",
                schema: "teta",
                table: "StageGateChecks",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_StageGateCriteria_Stage_Code",
                schema: "teta",
                table: "StageGateCriteria",
                columns: new[] { "Stage", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageGateReviews_ProjectId",
                schema: "teta",
                table: "StageGateReviews",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_OutcomeId",
                schema: "teta",
                table: "StrategicObjectives",
                column: "OutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicObjectives_PlanId_Code",
                schema: "teta",
                table: "StrategicObjectives",
                columns: new[] { "PlanId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicOutcomes_PlanId_Code",
                schema: "teta",
                table: "StrategicOutcomes",
                columns: new[] { "PlanId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_Code",
                schema: "teta",
                table: "StrategicPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlanVersions_PlanId_VersionNumber",
                schema: "teta",
                table: "StrategicPlanVersions",
                columns: new[] { "PlanId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CsdNumber",
                schema: "teta",
                table: "Suppliers",
                column: "CsdNumber",
                unique: true,
                filter: "[CsdNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_NormalizedName",
                schema: "teta",
                table: "Suppliers",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_RegistrationNumber",
                schema: "teta",
                table: "Suppliers",
                column: "RegistrationNumber",
                unique: true,
                filter: "[RegistrationNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierNumber",
                schema: "teta",
                table: "Suppliers",
                column: "SupplierNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierUsers_SupplierId_UserId",
                schema: "teta",
                table: "SupplierUsers",
                columns: new[] { "SupplierId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                schema: "teta",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionActions_EntityType_EntityId",
                schema: "teta",
                table: "TransactionActions",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_Status",
                schema: "teta",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityProfiles_UserId",
                schema: "teta",
                table: "UserSecurityProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                schema: "teta",
                table: "UserSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsDependencies_PredecessorId",
                schema: "teta",
                table: "WbsDependencies",
                column: "PredecessorId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsDependencies_SuccessorId",
                schema: "teta",
                table: "WbsDependencies",
                column: "SuccessorId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsElements_ParentId",
                schema: "teta",
                table: "WbsElements",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsElements_ProjectId_ParentId",
                schema: "teta",
                table: "WbsElements",
                columns: new[] { "ProjectId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_Code_DefinitionVersion",
                schema: "teta",
                table: "WorkflowDefinitions",
                columns: new[] { "Code", "DefinitionVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_DefinitionId",
                schema: "teta",
                table: "WorkflowInstances",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_EntityType_EntityId",
                schema: "teta",
                table: "WorkflowInstances",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepDefinitions_DefinitionId",
                schema: "teta",
                table: "WorkflowStepDefinitions",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTasks_Decision_AssignedRole",
                schema: "teta",
                table: "WorkflowTasks",
                columns: new[] { "Decision", "AssignedRole" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTasks_InstanceId",
                schema: "teta",
                table: "WorkflowTasks",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Workstreams_ProjectId",
                schema: "teta",
                table: "Workstreams",
                column: "ProjectId");

            // SEC-010 / SRS §14: audit records are append-only at the database level too.
            migrationBuilder.Sql(@"CREATE TRIGGER [teta].[TR_AuditLogs_AppendOnly] ON [teta].[AuditLogs]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50010, 'Audit records are append-only and cannot be updated or deleted.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [teta].[TR_AuditLogs_AppendOnly]");

            migrationBuilder.DropTable(
                name: "Accruals",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Adjudications",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "AppTargets",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "AssuranceCoverage",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "AuditFindings",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "BeneficiaryStatusHistory",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "BenefitReviews",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "BidderCommunications",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "BoardReportPacks",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "BudgetRevisions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "BusinessCases",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ChangeRequests",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Comments",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Commitments",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "CommitteeMeetings",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "CommitteeMembers",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ComplianceAttestations",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ContractBreaches",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ContractObligations",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ContractPerformanceReviews",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ContractVariations",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ControlAssessments",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "CorrectiveActions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "CostForecasts",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "DataQualityIssues",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Declarations",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Delegations",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "DueDiligenceChecks",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ErpInterfaceMessages",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ErpReconciliations",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "EscalationEvents",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "EvaluationScores",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Evidence",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Expenditures",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Findings",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ImportJobs",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Issues",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "MePlans",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "MonitoringTemplates",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "MonitoringVisits",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "NotificationTemplates",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "NumberSequences",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "PaymentScheduleItems",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "PerformanceResults",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "PricePreferenceSystems",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProcurementExceptions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProcurementMethodRules",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProcurementPlanItems",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProgressUpdates",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectCharters",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectClosures",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectDependencies",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectHealthSnapshots",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectIndicatorLinks",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectPriorityScores",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectStakeholders",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ProjectStatusHistory",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Publications",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "PublicHolidays",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ReferenceData",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ReportSchedules",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ResourceAssignments",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "RetentionPolicies",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "RiskRatingBands",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "RiskTreatments",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ScheduleBaselines",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "SodRules",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Specifications",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "StageGateChecks",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "StageGateCriteria",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "StrategicPlanVersions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Substitutions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "SupplierUsers",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "SystemSettings",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "TransactionActions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "UserRoleAssignments",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "UserSecurityProfiles",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "UserSessions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "WbsDependencies",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "WorkflowStepDefinitions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "WorkflowTasks",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Workstreams",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Beneficiaries",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Committees",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "ComplianceObligations",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "RiskControls",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "EvaluationCriteria",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "AppIndicators",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "PrioritisationCriteria",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "StageGateReviews",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "WbsElements",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "WorkflowInstances",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Risks",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Deliverables",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "StrategicObjectives",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Contracts",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "StrategicOutcomes",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Awards",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "StrategicPlans",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Bids",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Procurements",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Requisitions",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "BudgetLines",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Programmes",
                schema: "teta");

            migrationBuilder.DropTable(
                name: "Portfolios",
                schema: "teta");
        }
    }
}
