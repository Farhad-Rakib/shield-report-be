using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportTemplateKindRemoveDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "report_templates");

            migrationBuilder.AddColumn<byte>(
                name: "Kind",
                table: "report_templates",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "report_templates");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "report_templates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
