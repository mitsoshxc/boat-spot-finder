using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoatSpotFinder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDismissedByMarina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DismissedByMarina",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DismissedByMarina",
                table: "Bookings");
        }
    }
}
