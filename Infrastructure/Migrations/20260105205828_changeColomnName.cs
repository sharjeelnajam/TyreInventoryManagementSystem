using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class changeColomnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Thread",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "SizeId",
                table: "WholeSalerPrices",
                newName: "ThreadId");

            migrationBuilder.RenameColumn(
                name: "Size",
                table: "Products",
                newName: "ThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ThreadId",
                table: "WholeSalerPrices",
                newName: "SizeId");

            migrationBuilder.RenameColumn(
                name: "ThreadId",
                table: "Products",
                newName: "Size");

            migrationBuilder.AddColumn<string>(
                name: "Thread",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
