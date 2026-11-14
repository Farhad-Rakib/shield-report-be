using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromptLibraryCustomToolsTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomScanToolId",
                table: "scans",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldValuesJson",
                table: "reports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldsJson",
                table: "report_templates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "menus",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "custom_scan_tools",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Image = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ArgumentsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    OutputFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultSeverity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    MemoryMb = table.Column<int>(type: "int", nullable: false),
                    Cpus = table.Column<double>(type: "float", nullable: false),
                    RunAsRoot = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_scan_tools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_prompts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_prompts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_custom_scan_tools_Name",
                table: "custom_scan_tools",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_prompts_Key",
                table: "llm_prompts",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_scan_tools");

            migrationBuilder.DropTable(
                name: "llm_prompts");

            migrationBuilder.DropColumn(
                name: "CustomScanToolId",
                table: "scans");

            migrationBuilder.DropColumn(
                name: "CustomFieldValuesJson",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "CustomFieldsJson",
                table: "report_templates");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "menus");
        }
    }
}
