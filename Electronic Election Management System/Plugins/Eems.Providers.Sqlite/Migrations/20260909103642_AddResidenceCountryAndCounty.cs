using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eems.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddResidenceCountryAndCounty : Migration
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
                name: "ResidenceCountyCode",
                table: "VoterDeclarations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidenceCountry",
                table: "UserDetails",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidenceCountyCode",
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
                name: "ResidenceCountyCode",
                table: "VoterDeclarations");

            migrationBuilder.DropColumn(
                name: "ResidenceCountry",
                table: "UserDetails");

            migrationBuilder.DropColumn(
                name: "ResidenceCountyCode",
                table: "UserDetails");
        }
    }
}
