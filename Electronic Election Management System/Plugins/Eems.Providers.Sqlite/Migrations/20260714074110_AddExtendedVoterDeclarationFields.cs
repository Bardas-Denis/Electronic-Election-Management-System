using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedVoterDeclarationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Citizenship",
                table: "VoterDeclarations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "VoterDeclarations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DomiciliuLocalitate",
                table: "VoterDeclarations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "VoterDeclarations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkEmail",
                table: "VoterDeclarations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Citizenship",
                table: "VoterDeclarations");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "VoterDeclarations");

            migrationBuilder.DropColumn(
                name: "DomiciliuLocalitate",
                table: "VoterDeclarations");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "VoterDeclarations");

            migrationBuilder.DropColumn(
                name: "WorkEmail",
                table: "VoterDeclarations");
        }
    }
}
