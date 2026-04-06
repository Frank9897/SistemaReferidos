using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedGenealogica.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregaCamposProductoYPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Eliminar columnas de comisiones multinivel (ya no se usan) ──
            migrationBuilder.DropColumn(name: "ComisionNivel1Porcentaje", table: "Productos");
            migrationBuilder.DropColumn(name: "ComisionNivel2Porcentaje", table: "Productos");
            migrationBuilder.DropColumn(name: "ComisionNivel3Porcentaje", table: "Productos");

            // ── Agregar PorcentajeAbueloComision ─────────────────────────
            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeAbueloComision",
                table: "Productos",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 10m);

            // ── Agregar campos de PDFs ────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "PdfUrl1",
                table: "Productos",
                type: "character varying(350)",
                maxLength: 350,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfNombre1",
                table: "Productos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfUrl2",
                table: "Productos",
                type: "character varying(350)",
                maxLength: 350,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfNombre2",
                table: "Productos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaurar columnas de comisiones multinivel
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

            // Eliminar campos nuevos
            migrationBuilder.DropColumn(name: "PorcentajeAbueloComision", table: "Productos");
            migrationBuilder.DropColumn(name: "PdfUrl1",    table: "Productos");
            migrationBuilder.DropColumn(name: "PdfNombre1", table: "Productos");
            migrationBuilder.DropColumn(name: "PdfUrl2",    table: "Productos");
            migrationBuilder.DropColumn(name: "PdfNombre2", table: "Productos");
        }
    }
}