using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addNew_colomn_wholesalerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WholesalerId",
                table: "Sale",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sale_WholesalerId",
                table: "Sale",
                column: "WholesalerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sale_Wholesalers_WholesalerId",
                table: "Sale",
                column: "WholesalerId",
                principalTable: "Wholesalers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sale_Wholesalers_WholesalerId",
                table: "Sale");

            migrationBuilder.DropIndex(
                name: "IX_Sale_WholesalerId",
                table: "Sale");

            migrationBuilder.DropColumn(
                name: "WholesalerId",
                table: "Sale");
        }
    }
}
