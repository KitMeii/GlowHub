using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseCore.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FixDecimalTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2676));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2709));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2710));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2711));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 48, 14, 568, DateTimeKind.Utc).AddTicks(2711));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 36, 19, 860, DateTimeKind.Utc).AddTicks(6144));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 36, 19, 860, DateTimeKind.Utc).AddTicks(6153));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 36, 19, 860, DateTimeKind.Utc).AddTicks(6154));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 36, 19, 860, DateTimeKind.Utc).AddTicks(6155));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 8, 36, 19, 860, DateTimeKind.Utc).AddTicks(6156));
        }
    }
}
