using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servisnaknizka.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleExpiryAndNotifCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmissionExpiry",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InsuranceExpiry",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StkExpiry",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Notifications",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "service");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmissionExpiry",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "InsuranceExpiry",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "StkExpiry",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Notifications");
        }
    }
}
