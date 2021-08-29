using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace API.Migrations
{
    public partial class Group_Update_changes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Friends_FriendId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_FriendId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "FriendId",
                table: "Conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "Friends",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friends_ConversationId",
                table: "Friends",
                column: "ConversationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_Conversations_ConversationId",
                table: "Friends",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Friends_Conversations_ConversationId",
                table: "Friends");

            migrationBuilder.DropIndex(
                name: "IX_Friends_ConversationId",
                table: "Friends");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "Friends");

            migrationBuilder.AddColumn<Guid>(
                name: "FriendId",
                table: "Conversations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_FriendId",
                table: "Conversations",
                column: "FriendId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Friends_FriendId",
                table: "Conversations",
                column: "FriendId",
                principalTable: "Friends",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
