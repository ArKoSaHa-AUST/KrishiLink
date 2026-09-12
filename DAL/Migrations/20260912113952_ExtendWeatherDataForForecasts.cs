using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ExtendWeatherDataForForecasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "WeatherData",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FetchedAt",
                table: "WeatherData",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "HumidityMax",
                table: "WeatherData",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HumidityMin",
                table: "WeatherData",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PrecipitationMm",
                table: "WeatherData",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PrecipitationProbability",
                table: "WeatherData",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "TemperatureMax",
                table: "WeatherData",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "TemperatureMin",
                table: "WeatherData",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "WeatherCode",
                table: "WeatherData",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "District",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "FetchedAt",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "HumidityMax",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "HumidityMin",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "PrecipitationMm",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "PrecipitationProbability",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "TemperatureMax",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "TemperatureMin",
                table: "WeatherData");

            migrationBuilder.DropColumn(
                name: "WeatherCode",
                table: "WeatherData");
        }
    }
}
