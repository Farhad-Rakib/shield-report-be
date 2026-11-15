using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldReport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeededFindingsSummaryTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The seeded "Findings Summary — Selise Standard" row is no longer wanted (and no longer
            // seeded). Findings summary exports are not stored as reports, so nothing references it.
            migrationBuilder.Sql(
                "DELETE FROM report_templates WHERE Kind = 1 AND Name = N'Findings Summary — Selise Standard';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
