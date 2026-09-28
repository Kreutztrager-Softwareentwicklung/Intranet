using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet2.Migrations
{
    /// <inheritdoc />
    public partial class NamentlicheUmfrageauswertung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NamentlicheAuswertung",
                table: "Umfragen",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NamentlicheAuswertung",
                table: "Umfragen");
        }
    }
}
