using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eems.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddResidenceCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResidenceCountry",
                table: "VoterDeclarations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidenceCountry",
                table: "UserDetails",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResidenceCountry",
                table: "VoterDeclarations");

            migrationBuilder.DropColumn(
                name: "ResidenceCountry",
                table: "UserDetails");
        }
    }
}
