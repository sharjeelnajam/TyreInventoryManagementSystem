using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSaleCustomerForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            // Use conditional SQL to safely drop foreign keys
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Sale_Customer')
        BEGIN
            ALTER TABLE Sale DROP CONSTRAINT FK_Sale_Customer;
        END
    ");

            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Sale_Wholesalers_WholesalerId')
        BEGIN
            ALTER TABLE Sale DROP CONSTRAINT FK_Sale_Wholesalers_WholesalerId;
        END
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            // Restore Customer FK
            migrationBuilder.AddForeignKey(
                name: "FK_Sale_Customer",
                table: "Sale",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Restore Wholesaler FK
            migrationBuilder.AddForeignKey(
                name: "FK_Sale_Wholesalers_WholesalerId",
                table: "Sale",
                column: "WholesalerId",
                principalTable: "Wholesalers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
