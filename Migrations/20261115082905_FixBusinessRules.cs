using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ihouse.Migrations
{
    /// <inheritdoc />
    public partial class FixBusinessRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_InvoiceId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_RoomId",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId",
                table: "Invoices");

            migrationBuilder.AddColumn<int>(
                name: "BilledInvoiceId",
                table: "MaintenanceRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingMonth",
                table: "Invoices",
                type: "integer",
                nullable: false,
                computedColumnSql: "EXTRACT(YEAR FROM \"InvoiceDate\")::integer * 100 + EXTRACT(MONTH FROM \"InvoiceDate\")::integer",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IdentityNumber",
                table: "Tenants",
                column: "IdentityNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RoomNumber",
                table: "Rooms",
                column: "RoomNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InvoiceId",
                table: "PaymentTransactions",
                column: "InvoiceId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionCode",
                table: "Payments",
                column: "TransactionCode",
                unique: true,
                filter: "\"TransactionCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_RoomId_ReadingDay",
                table: "MeterReadings",
                columns: new[] { "RoomId", "ReadingDay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_BilledInvoiceId",
                table: "MaintenanceRequests",
                column: "BilledInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_BillingMonth",
                table: "Invoices",
                columns: new[] { "ContractId", "BillingMonth" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_Invoices_BilledInvoiceId",
                table: "MaintenanceRequests",
                column: "BilledInvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_Invoices_BilledInvoiceId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_IdentityNumber",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_RoomNumber",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_InvoiceId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TransactionCode",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_RoomId_ReadingDay",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_BilledInvoiceId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_BillingMonth",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingMonth",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BilledInvoiceId",
                table: "MaintenanceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InvoiceId",
                table: "PaymentTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_RoomId",
                table: "MeterReadings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId",
                table: "Invoices",
                column: "ContractId");
        }
    }
}
