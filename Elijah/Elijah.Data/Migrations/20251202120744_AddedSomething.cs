using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elijah.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedSomething : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceFilter_DeviceId",
                table: "DeviceFilter");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFilter_DeviceId",
                table: "DeviceFilter",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceFilter_DeviceId",
                table: "DeviceFilter");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFilter_DeviceId",
                table: "DeviceFilter",
                column: "DeviceId",
                unique: true);
        }
    }
}
