using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseCore.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountPrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsNew",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Products");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Salt",
                table: "Users",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VoucherCode",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderStatusLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusLogs_OrderId",
                table: "OrderStatusLogs",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderStatusLogs");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherCode",
                table: "Orders");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Salt",
                table: "Users",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Products",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPrice",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNew",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DiscountPrice", "IsNew", "SortOrder" },
                values: new object[] { new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2676), null, false, 0 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DiscountPrice", "IsNew", "SortOrder" },
                values: new object[] { new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2709), null, false, 0 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "DiscountPrice", "IsNew", "SortOrder" },
                values: new object[] { new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2710), null, false, 0 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DiscountPrice", "IsNew", "SortOrder" },
                values: new object[] { new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2711), null, false, 0 });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "DiscountPrice", "IsNew", "SortOrder" },
                values: new object[] { new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2711), null, false, 0 });
        }
    }
}
