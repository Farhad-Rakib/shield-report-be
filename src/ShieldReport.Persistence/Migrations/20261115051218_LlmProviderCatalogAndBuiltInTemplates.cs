using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LlmProviderCatalogAndBuiltInTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "BuiltInTestingType",
                table: "report_templates",
                type: "tinyint",
                nullable: true);

            // The seeded "White Box Testing — Selise Standard" row becomes the White built-in.
            // DefaultReportTemplateSeeder adds the Gray and Black Box rows on the next start.
            migrationBuilder.Sql(
                "UPDATE report_templates SET BuiltInTestingType = 0 " +
                "WHERE BuiltInTestingType IS NULL AND Name LIKE N'White Box Testing%' AND DocxTemplatePath IS NULL;");

            migrationBuilder.CreateTable(
                name: "llm_providers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_providers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_llm_providers_Name",
                table: "llm_providers",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_providers");

            migrationBuilder.DropColumn(
                name: "BuiltInTestingType",
                table: "report_templates");
        }
    }
}
