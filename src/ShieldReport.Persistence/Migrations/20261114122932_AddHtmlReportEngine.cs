using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHtmlReportEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Caption",
                table: "vulnerability_attachments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInReport",
                table: "vulnerability_attachments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Limitations",
                table: "reports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectGoals",
                table: "reports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RenderedHtml",
                table: "reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeStatusesJson",
                table: "reports",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ShowToolNames",
                table: "reports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HtmlContent",
                table: "report_templates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TestingType",
                table: "engagements",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Caption",
                table: "vulnerability_attachments");

            migrationBuilder.DropColumn(
                name: "IncludeInReport",
                table: "vulnerability_attachments");

            migrationBuilder.DropColumn(
                name: "Limitations",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "ProjectGoals",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "RenderedHtml",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "ScopeStatusesJson",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "ShowToolNames",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "HtmlContent",
                table: "report_templates");

            migrationBuilder.DropColumn(
                name: "TestingType",
                table: "engagements");
        }
    }
}
