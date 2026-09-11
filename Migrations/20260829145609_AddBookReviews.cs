using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookworm.Migrations
{
    /// <inheritdoc />
    public partial class AddBookReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookReviews",
                columns: table => new
                {
                    Id = table.Column<int>(
                        type: "INTEGER",
                        nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),

                    UserId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false),

                    BookId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false),

                    ReviewText = table.Column<string>(
                        type: "TEXT",
                        nullable: false),

                    CreatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookReviews", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookReviews");
        }
    }
}