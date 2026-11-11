using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketplaceWatcher.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeMeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxOdometerKm",
                table: "Watches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinExpiryMonths",
                table: "Watches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TradeMe",
                table: "Watches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Seen",
                type: "TEXT",
                nullable: false,
                defaultValue: "Facebook");

            migrationBuilder.AddColumn<DateTime>(
                name: "RegoExpires",
                table: "CheckedListings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WofExpires",
                table: "CheckedListings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxOdometerKm",
                table: "Watches");

            migrationBuilder.DropColumn(
                name: "MinExpiryMonths",
                table: "Watches");

            migrationBuilder.DropColumn(
                name: "TradeMe",
                table: "Watches");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Seen");

            migrationBuilder.DropColumn(
                name: "RegoExpires",
                table: "CheckedListings");

            migrationBuilder.DropColumn(
                name: "WofExpires",
                table: "CheckedListings");
        }
    }
}
