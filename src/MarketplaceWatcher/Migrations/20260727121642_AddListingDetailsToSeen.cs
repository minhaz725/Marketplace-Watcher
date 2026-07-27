using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketplaceWatcher.Migrations
{
    /// <inheritdoc />
    public partial class AddListingDetailsToSeen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Seen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Seen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Seen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WatchId",
                table: "Seen",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WatchName",
                table: "Seen",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Location",
                table: "Seen");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Seen");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "Seen");

            migrationBuilder.DropColumn(
                name: "WatchId",
                table: "Seen");

            migrationBuilder.DropColumn(
                name: "WatchName",
                table: "Seen");
        }
    }
}
