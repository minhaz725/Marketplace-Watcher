using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketplaceWatcher.Migrations
{
    /// <inheritdoc />
    public partial class SeenAtAsDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF generated this migration EMPTY: DateTimeOffset and DateTime both map to
            // SQLite TEXT, so the model diff saw no schema change. The stored *format* did
            // change though — rows written as DateTimeOffset carry a "+00:00" offset that
            // DateTime cannot parse. Migrations diff the model, never the data, so the
            // backfill has to be written by hand or every existing row fails to read.
            migrationBuilder.Sql(
                "UPDATE Seen SET SeenAt = replace(SeenAt, '+00:00', '') WHERE SeenAt LIKE '%+00:00';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Seen SET SeenAt = SeenAt || '+00:00' WHERE SeenAt NOT LIKE '%+00:00';");
        }
    }
}
