using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_ScanExecutionEngagementApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EngagementTaskId",
                table: "vulnerabilities",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EngagementTaskId",
                table: "scans",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vulnerabilities_EngagementId",
                table: "vulnerabilities",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_vulnerabilities_EngagementTaskId",
                table: "vulnerabilities",
                column: "EngagementTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_scans_EngagementId",
                table: "scans",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_scans_EngagementTaskId",
                table: "scans",
                column: "EngagementTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_scans_engagement_tasks_EngagementTaskId",
                table: "scans",
                column: "EngagementTaskId",
                principalTable: "engagement_tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_scans_engagements_EngagementId",
                table: "scans",
                column: "EngagementId",
                principalTable: "engagements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vulnerabilities_engagement_tasks_EngagementTaskId",
                table: "vulnerabilities",
                column: "EngagementTaskId",
                principalTable: "engagement_tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vulnerabilities_engagements_EngagementId",
                table: "vulnerabilities",
                column: "EngagementId",
                principalTable: "engagements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scans_engagement_tasks_EngagementTaskId",
                table: "scans");

            migrationBuilder.DropForeignKey(
                name: "FK_scans_engagements_EngagementId",
                table: "scans");

            migrationBuilder.DropForeignKey(
                name: "FK_vulnerabilities_engagement_tasks_EngagementTaskId",
                table: "vulnerabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_vulnerabilities_engagements_EngagementId",
                table: "vulnerabilities");

            migrationBuilder.DropIndex(
                name: "IX_vulnerabilities_EngagementId",
                table: "vulnerabilities");

            migrationBuilder.DropIndex(
                name: "IX_vulnerabilities_EngagementTaskId",
                table: "vulnerabilities");

            migrationBuilder.DropIndex(
                name: "IX_scans_EngagementId",
                table: "scans");

            migrationBuilder.DropIndex(
                name: "IX_scans_EngagementTaskId",
                table: "scans");

            migrationBuilder.DropColumn(
                name: "EngagementTaskId",
                table: "vulnerabilities");

            migrationBuilder.DropColumn(
                name: "EngagementTaskId",
                table: "scans");
        }
    }
}
