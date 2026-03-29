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

            migrationBuilder.AddColumn<string>(
                name: "CbuAlias",
                table: "Usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoDisponible",
                table: "Usuarios",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoPendienteRetiro",
                table: "Usuarios",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BonusComisionPorcentaje",
                table: "RangosUsuario",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ComisionNivel1Porcentaje",
                table: "Productos",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ComisionNivel2Porcentaje",
                table: "Productos",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ComisionNivel3Porcentaje",
                table: "Productos",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ImagenUrl",
                table: "Productos",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockDisponible",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Monto",
                table: "MovimientosPuntos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateTable(
                name: "SolicitudesRetiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    CbuAlias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReferenciaTransferencia = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NotaAdmin = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FechaSolicitud = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminResolvidoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesRetiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesRetiro_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Productos",
                columns: new[] { "Id", "Activo", "ComisionNivel1Porcentaje", "ComisionNivel2Porcentaje", "ComisionNivel3Porcentaje", "Descripcion", "FechaCreacion", "ImagenUrl", "Nombre", "Precio", "StockDisponible" },
                values: new object[] { 1, true, 10m, 5m, 2m, "Switch de red no administrable de 8 puertos 10/100 Mbps. Ideal para pequeñas oficinas y hogares.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Switch TP-Link 8 puertos", 50000m, null });

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

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesRetiro_UsuarioId",
                table: "SolicitudesRetiro",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesRetiro");

            migrationBuilder.DeleteData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "CbuAlias",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SaldoDisponible",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SaldoPendienteRetiro",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "BonusComisionPorcentaje",
                table: "RangosUsuario");

            migrationBuilder.DropColumn(
                name: "ComisionNivel1Porcentaje",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ComisionNivel2Porcentaje",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ComisionNivel3Porcentaje",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ImagenUrl",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "StockDisponible",
                table: "Productos");

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
