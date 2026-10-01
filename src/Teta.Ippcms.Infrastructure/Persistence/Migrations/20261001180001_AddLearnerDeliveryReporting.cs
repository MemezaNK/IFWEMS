using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teta.Ippcms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnerDeliveryReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiaries_ProjectId",
                schema: "teta",
                table: "Beneficiaries");

            migrationBuilder.AddColumn<string>(
                name: "Cohort",
                schema: "teta",
                table: "Beneficiaries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LearnerDeliveryTargets",
                schema: "teta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractedLearners = table.Column<int>(type: "int", nullable: false),
                    LearnersDueForCompletion = table.Column<int>(type: "int", nullable: false),
                    MonitoringVisitsPlanned = table.Column<int>(type: "int", nullable: false),
                    WithdrawalTolerancePercent = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerDeliveryTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_ProjectId_Cohort",
                schema: "teta",
                table: "Beneficiaries",
                columns: new[] { "ProjectId", "Cohort" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerDeliveryTargets_ProjectId",
                schema: "teta",
                table: "LearnerDeliveryTargets",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearnerDeliveryTargets",
                schema: "teta");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiaries_ProjectId_Cohort",
                schema: "teta",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "Cohort",
                schema: "teta",
                table: "Beneficiaries");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_ProjectId",
                schema: "teta",
                table: "Beneficiaries",
                column: "ProjectId");
        }
    }
}
