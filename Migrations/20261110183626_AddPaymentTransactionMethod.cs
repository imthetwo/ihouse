using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ihouse.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactionMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "PaymentTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestedByUserId",
                table: "PaymentTransactions",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "PaymentTransactions");
        }
    }
}
