using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpanishDayAhead.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DayAheadPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BiddingZone = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Period = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryStartUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    PriceEurPerMWh = table.Column<double>(type: "REAL", nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayAheadPrices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DayAheadPrices_DeliveryDate",
                table: "DayAheadPrices",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_DayAheadPrices_DeliveryStartUtc",
                table: "DayAheadPrices",
                column: "DeliveryStartUtc");

            migrationBuilder.CreateIndex(
                name: "UX_DayAheadPrices_BiddingZone_DeliveryStartUtc",
                table: "DayAheadPrices",
                columns: new[] { "BiddingZone", "DeliveryStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DayAheadPrices");
        }
    }
}
