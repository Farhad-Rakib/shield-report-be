using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_EngagementSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "engagements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeadPentesterId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_engagements_client_organizations_ClientOrganizationId",
                        column: x => x.ClientOrganizationId,
                        principalTable: "client_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_engagements_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_engagements_users_LeadPentesterId",
                        column: x => x.LeadPentesterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "engagement_assignees",
                columns: table => new
                {
                    EngagementId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagement_assignees", x => new { x.EngagementId, x.UserId });
                    table.ForeignKey(
                        name: "FK_engagement_assignees_engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_engagement_assignees_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "engagement_tasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EngagementId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedToUserId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagement_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_engagement_tasks_engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_engagement_tasks_users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_engagement_tasks_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "engagement_task_assets",
                columns: table => new
                {
                    EngagementTaskId = table.Column<long>(type: "bigint", nullable: false),
                    ClientAssetId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagement_task_assets", x => new { x.EngagementTaskId, x.ClientAssetId });
                    table.ForeignKey(
                        name: "FK_engagement_task_assets_client_assets_ClientAssetId",
                        column: x => x.ClientAssetId,
                        principalTable: "client_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_engagement_task_assets_engagement_tasks_EngagementTaskId",
                        column: x => x.EngagementTaskId,
                        principalTable: "engagement_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_engagement_assignees_UserId",
                table: "engagement_assignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_engagement_task_assets_ClientAssetId",
                table: "engagement_task_assets",
                column: "ClientAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_engagement_tasks_AssignedToUserId",
                table: "engagement_tasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_engagement_tasks_CreatedByUserId",
                table: "engagement_tasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_engagement_tasks_EngagementId",
                table: "engagement_tasks",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_engagements_ClientOrganizationId",
                table: "engagements",
                column: "ClientOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_engagements_CreatedByUserId",
                table: "engagements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_engagements_LeadPentesterId",
                table: "engagements",
                column: "LeadPentesterId");

            migrationBuilder.CreateIndex(
                name: "IX_engagements_PublicId",
                table: "engagements",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "engagement_assignees");

            migrationBuilder.DropTable(
                name: "engagement_task_assets");

            migrationBuilder.DropTable(
                name: "engagement_tasks");

            migrationBuilder.DropTable(
                name: "engagements");
        }
    }
}
