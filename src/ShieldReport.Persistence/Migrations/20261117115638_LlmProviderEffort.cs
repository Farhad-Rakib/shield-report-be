using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LlmProviderEffort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Effort",
                table: "llm_provider_configurations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Effort",
                table: "llm_provider_configurations");
        }
    }
}
