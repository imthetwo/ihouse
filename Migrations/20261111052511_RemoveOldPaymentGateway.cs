using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ihouse.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldPaymentGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_MoMoTransactionId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "MoMoTransactionId",
                table: "PaymentTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MoMoTransactionId",
                table: "PaymentTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_MoMoTransactionId",
                table: "PaymentTransactions",
                column: "MoMoTransactionId",
                unique: true);
        }
    }
}
