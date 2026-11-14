using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditIndexesAndScanAuthorizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ScanAuthorizationId",
                table: "scans",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "scan_authorizations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientAssetId = table.Column<long>(type: "bigint", nullable: false),
                    EngagementId = table.Column<long>(type: "bigint", nullable: true),
                    AuthorizedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ClientContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AllowedTools = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidenceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    EvidenceContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EvidenceUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scan_authorizations_client_assets_ClientAssetId",
                        column: x => x.ClientAssetId,
                        principalTable: "client_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scan_authorizations_engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scan_authorizations_users_AuthorizedByUserId",
                        column: x => x.AuthorizedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scan_authorizations_users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scans_ScanAuthorizationId",
                table: "scans",
                column: "ScanAuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_scan_authorizations_AuthorizedByUserId",
                table: "scan_authorizations",
                column: "AuthorizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_scan_authorizations_ClientAssetId_RevokedAt",
                table: "scan_authorizations",
                columns: new[] { "ClientAssetId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_scan_authorizations_EngagementId",
                table: "scan_authorizations",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_scan_authorizations_RevokedByUserId",
                table: "scan_authorizations",
                column: "RevokedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_scans_scan_authorizations_ScanAuthorizationId",
                table: "scans",
                column: "ScanAuthorizationId",
                principalTable: "scan_authorizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Data step: every asset authorized through the old on/off flag gets an equivalent,
            // open-ended authorization record (all tools), so it keeps scanning after this
            // upgrade. AuthorizedByUserId falls back to the first user when the flag was set
            // without one. New records always have an end date.
            migrationBuilder.Sql(@"
INSERT INTO scan_authorizations
    (ClientAssetId, EngagementId, AuthorizedByUserId, ClientContactName, ClientContactEmail,
     ValidFrom, ValidUntil, AllowedTools, Notes, CreatedAt, CreatedBy, UpdatedBy, UpdatedAt)
SELECT a.Id, NULL, COALESCE(a.AuthorizedByUserId, (SELECT MIN(u.Id) FROM users u)), NULL, NULL,
       COALESCE(a.AuthorizedAt, SYSUTCDATETIME()), NULL, 'Naabu,Nuclei,Reconftw',
       'Migrated from the legacy authorized-for-scanning flag. Record the client approval and an end date.',
       SYSUTCDATETIME(), 0, 0, SYSUTCDATETIME()
FROM client_assets a
WHERE a.IsAuthorizedForScanning = 1
  AND EXISTS (SELECT 1 FROM users);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scans_scan_authorizations_ScanAuthorizationId",
                table: "scans");

            migrationBuilder.DropTable(
                name: "scan_authorizations");

            migrationBuilder.DropIndex(
                name: "IX_scans_ScanAuthorizationId",
                table: "scans");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ScanAuthorizationId",
                table: "scans");
        }
    }
}
