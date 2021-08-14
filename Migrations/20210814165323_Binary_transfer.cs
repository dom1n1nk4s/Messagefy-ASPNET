using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Migrations
{
    public partial class Binary_transfer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageTitle",
                table: "Images",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "ImageData",
                table: "Images",
                newName: "Data");

            migrationBuilder.AddColumn<bool>(
                name: "IsReferenceToFile",
                table: "Messages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: true),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_ConversationId",
                table: "Files",
                column: "ConversationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropColumn(
                name: "IsReferenceToFile",
                table: "Messages");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "Images",
                newName: "ImageTitle");

            migrationBuilder.RenameColumn(
                name: "Data",
                table: "Images",
                newName: "ImageData");
        }
    }
}
