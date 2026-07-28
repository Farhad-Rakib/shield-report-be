using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_templates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyDisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EngagementId = table.Column<long>(type: "bigint", nullable: false),
                    ReportTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyFindings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyRecommendations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GeneratedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reports_report_templates_ReportTemplateId",
                        column: x => x.ReportTemplateId,
                        principalTable: "report_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reports_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reports_users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reports_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reports_ApprovedByUserId",
                table: "reports",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_EngagementId_Version",
                table: "reports",
                columns: new[] { "EngagementId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_GeneratedByUserId",
                table: "reports",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_PublicId",
                table: "reports",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_RejectedByUserId",
                table: "reports",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReportTemplateId",
                table: "reports",
                column: "ReportTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "report_templates");
        }
    }
}
