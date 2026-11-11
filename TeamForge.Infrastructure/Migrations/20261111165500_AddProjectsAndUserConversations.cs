using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsAndUserConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUsername",
                table: "Submissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Submissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerUsername_CreatedAt",
                table: "Projects",
                columns: new[] { "OwnerUsername", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_OwnerUsername_ProjectId_CreatedAt",
                table: "Submissions",
                columns: new[] { "OwnerUsername", "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ProjectId",
                table: "Submissions",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Projects_ProjectId",
                table: "Submissions",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Projects_ProjectId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_OwnerUsername_ProjectId_CreatedAt",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_ProjectId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "OwnerUsername",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Submissions");
        }
    }
}
