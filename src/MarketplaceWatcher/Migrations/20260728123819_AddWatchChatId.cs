using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketplaceWatcher.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchChatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChatId",
                table: "Watches",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "Watches");
        }
    }
}
