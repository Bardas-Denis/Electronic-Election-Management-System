using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eems.Providers.Postgres.Migrations
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
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidenceCountry",
                table: "UserDetails",
                type: "text",
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
