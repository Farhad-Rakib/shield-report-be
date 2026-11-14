using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LlmProviderNamesAndReportNarrativeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_llm_provider_configurations_Provider",
                table: "llm_provider_configurations");

            migrationBuilder.AddColumn<string>(
                name: "NarrativeError",
                table: "reports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "NarrativeStatus",
                table: "reports",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "llm_provider_configurations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Existing rows (at most one per provider until now) get "<Provider> · <Model>",
            // unique because the old index made Provider unique.
            migrationBuilder.Sql("""
                UPDATE llm_provider_configurations
                SET Name = LEFT(CONCAT(
                    CASE Provider WHEN 0 THEN N'OpenRouter' WHEN 1 THEN N'Gemini' WHEN 2 THEN N'OpenAI' ELSE N'Provider' END,
                    N' · ', Model), 100)
                WHERE Name = N'';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_llm_provider_configurations_Name",
                table: "llm_provider_configurations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_provider_configurations_Provider",
                table: "llm_provider_configurations",
                column: "Provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_llm_provider_configurations_Name",
                table: "llm_provider_configurations");

            migrationBuilder.DropIndex(
                name: "IX_llm_provider_configurations_Provider",
                table: "llm_provider_configurations");

            migrationBuilder.DropColumn(
                name: "NarrativeError",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "NarrativeStatus",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "llm_provider_configurations");

            migrationBuilder.CreateIndex(
                name: "IX_llm_provider_configurations_Provider",
                table: "llm_provider_configurations",
                column: "Provider",
                unique: true);
        }
    }
}
