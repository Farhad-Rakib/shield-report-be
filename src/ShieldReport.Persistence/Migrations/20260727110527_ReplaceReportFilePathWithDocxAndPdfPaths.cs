using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceReportFilePathWithDocxAndPdfPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "reports",
                newName: "FinalPdfPath");

            migrationBuilder.AddColumn<string>(
                name: "DraftDocxPath",
                table: "reports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftDocxPath",
                table: "reports");

            migrationBuilder.RenameColumn(
                name: "FinalPdfPath",
                table: "reports",
                newName: "FilePath");
        }
    }
}
