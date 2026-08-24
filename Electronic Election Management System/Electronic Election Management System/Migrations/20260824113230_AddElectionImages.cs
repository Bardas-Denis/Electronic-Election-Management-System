using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddElectionImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "Options",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "ElectionQuestions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElectionImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ByteSize = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionImages_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectionImages_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Options_ImageId",
                table: "Options",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionQuestions_ImageId",
                table: "ElectionQuestions",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionImages_ElectionId",
                table: "ElectionImages",
                column: "ElectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionImages_UploadedByUserId",
                table: "ElectionImages",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElectionQuestions_ElectionImages_ImageId",
                table: "ElectionQuestions",
                column: "ImageId",
                principalTable: "ElectionImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Options_ElectionImages_ImageId",
                table: "Options",
                column: "ImageId",
                principalTable: "ElectionImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectionQuestions_ElectionImages_ImageId",
                table: "ElectionQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_Options_ElectionImages_ImageId",
                table: "Options");

            migrationBuilder.DropTable(
                name: "ElectionImages");

            migrationBuilder.DropIndex(
                name: "IX_Options_ImageId",
                table: "Options");

            migrationBuilder.DropIndex(
                name: "IX_ElectionQuestions_ImageId",
                table: "ElectionQuestions");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "ElectionQuestions");
        }
    }
}
