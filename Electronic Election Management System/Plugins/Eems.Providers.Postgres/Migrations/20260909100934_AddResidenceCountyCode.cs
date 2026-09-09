using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eems.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddResidenceCountyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResidenceCountyCode",
                table: "VoterDeclarations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidenceCountyCode",
                table: "UserDetails",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResidenceCountyCode",
                table: "VoterDeclarations");

            migrationBuilder.DropColumn(
                name: "ResidenceCountyCode",
                table: "UserDetails");
        }
    }
}
