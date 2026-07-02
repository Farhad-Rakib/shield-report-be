using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_IdentityAndRegistrationInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClientOrganizationId",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClientPortalUser",
                table: "users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "registration_invites",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClientOrganizationId = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegisteredUserId = table.Column<long>(type: "bigint", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_registration_invites_client_organizations_ClientOrganizationId",
                        column: x => x.ClientOrganizationId,
                        principalTable: "client_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_invites_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_invites_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_invites_users_RegisteredUserId",
                        column: x => x.RegisteredUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_invites_users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_ClientOrganizationId",
                table: "users",
                column: "ClientOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_invites_ClientOrganizationId",
                table: "registration_invites",
                column: "ClientOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_invites_CreatedByUserId",
                table: "registration_invites",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_invites_Email",
                table: "registration_invites",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_registration_invites_RegisteredUserId",
                table: "registration_invites",
                column: "RegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_invites_RevokedByUserId",
                table: "registration_invites",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_invites_RoleId",
                table: "registration_invites",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_registration_invites_TokenHash",
                table: "registration_invites",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_users_client_organizations_ClientOrganizationId",
                table: "users",
                column: "ClientOrganizationId",
                principalTable: "client_organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_client_organizations_ClientOrganizationId",
                table: "users");

            migrationBuilder.DropTable(
                name: "registration_invites");

            migrationBuilder.DropIndex(
                name: "IX_users_ClientOrganizationId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ClientOrganizationId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsClientPortalUser",
                table: "users");
        }
    }
}
