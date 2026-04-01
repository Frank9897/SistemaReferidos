using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RedGenealogica.Web.Migrations
{
    /// <inheritdoc />
    public partial class LogicaProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_DocumentoIdentidad",
                table: "Usuarios");

            migrationBuilder.AlterColumn<decimal>(
                name: "Monto",
                table: "MovimientosPuntos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");


            migrationBuilder.UpdateData(
                table: "RangosUsuario",
                keyColumn: "Id",
                keyValue: 1,
                column: "BonusComisionPorcentaje",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "RangosUsuario",
                keyColumn: "Id",
                keyValue: 2,
                column: "BonusComisionPorcentaje",
                value: 10m);

            migrationBuilder.UpdateData(
                table: "RangosUsuario",
                keyColumn: "Id",
                keyValue: 3,
                column: "BonusComisionPorcentaje",
                value: 20m);

            migrationBuilder.UpdateData(
                table: "RangosUsuario",
                keyColumn: "Id",
                keyValue: 4,
                column: "BonusComisionPorcentaje",
                value: 40m);

            migrationBuilder.UpdateData(
                table: "RangosUsuario",
                keyColumn: "Id",
                keyValue: 5,
                column: "BonusComisionPorcentaje",
                value: 60m);

            migrationBuilder.UpdateData(
                table: "RangosUsuario",
                keyColumn: "Id",
                keyValue: 6,
                column: "BonusComisionPorcentaje",
                value: 80m);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AlterColumn<decimal>(
                name: "Monto",
                table: "MovimientosPuntos",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_DocumentoIdentidad",
                table: "Usuarios",
                column: "DocumentoIdentidad");
        }
    }
}
