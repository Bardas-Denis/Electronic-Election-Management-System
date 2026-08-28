using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddVoterChangeRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditCount",
                table: "Votes");

            migrationBuilder.CreateTable(
                name: "VoterChangeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoterChangeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoterChangeRecords_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoterChangeRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoterChangeRecords_UserId_ElectionId",
                table: "VoterChangeRecords",
                columns: new[] { "UserId", "ElectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoterChangeRecords_ElectionId",
                table: "VoterChangeRecords",
                column: "ElectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoterChangeRecords");

            migrationBuilder.AddColumn<int>(
                name: "EditCount",
                table: "Votes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
