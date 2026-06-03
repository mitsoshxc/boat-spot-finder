using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoatSpotFinder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDismissedByOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DismissedByOwner",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DismissedByOwner",
                table: "Bookings");
        }
    }
}
