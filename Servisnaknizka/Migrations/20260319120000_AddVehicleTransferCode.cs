using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servisnaknizka.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleTransferCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransferCode",
                table: "Vehicles",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransferCodeExpiry",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TransferCode",
                table: "Vehicles",
                column: "TransferCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TransferCode",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "TransferCode",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "TransferCodeExpiry",
                table: "Vehicles");
        }
    }
}
