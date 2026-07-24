using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePropertyNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DomiciliuLocalitate",
                table: "VoterDeclarations",
                newName: "ResidenceCounty");

            migrationBuilder.RenameColumn(
                name: "DomiciliuJudet",
                table: "VoterDeclarations",
                newName: "ResidenceCity");

            migrationBuilder.RenameColumn(
                name: "DomiciliuAdresa",
                table: "VoterDeclarations",
                newName: "ResidenceAddress");

            migrationBuilder.RenameColumn(
                name: "DomiciliuLocalitate",
                table: "UserDetails",
                newName: "ResidenceCounty");

            migrationBuilder.RenameColumn(
                name: "DomiciliuJudet",
                table: "UserDetails",
                newName: "ResidenceCity");

            migrationBuilder.RenameColumn(
                name: "DomiciliuAdresa",
                table: "UserDetails",
                newName: "ResidenceAddress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ResidenceCounty",
                table: "VoterDeclarations",
                newName: "DomiciliuLocalitate");

            migrationBuilder.RenameColumn(
                name: "ResidenceCity",
                table: "VoterDeclarations",
                newName: "DomiciliuJudet");

            migrationBuilder.RenameColumn(
                name: "ResidenceAddress",
                table: "VoterDeclarations",
                newName: "DomiciliuAdresa");

            migrationBuilder.RenameColumn(
                name: "ResidenceCounty",
                table: "UserDetails",
                newName: "DomiciliuLocalitate");

            migrationBuilder.RenameColumn(
                name: "ResidenceCity",
                table: "UserDetails",
                newName: "DomiciliuJudet");

            migrationBuilder.RenameColumn(
                name: "ResidenceAddress",
                table: "UserDetails",
                newName: "DomiciliuAdresa");
        }
    }
}
