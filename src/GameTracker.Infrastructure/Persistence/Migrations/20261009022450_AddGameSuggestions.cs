using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuggested",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuggested",
                table: "Games");
        }
    }
}
