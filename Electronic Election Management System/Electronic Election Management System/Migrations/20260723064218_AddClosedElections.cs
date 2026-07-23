using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedElections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Elections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ElectionInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionInvitations_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectionInvitations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectionInvitations_ElectionId_Email",
                table: "ElectionInvitations",
                columns: new[] { "ElectionId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectionInvitations_UserId",
                table: "ElectionInvitations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectionInvitations");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Elections");
        }
    }
}
