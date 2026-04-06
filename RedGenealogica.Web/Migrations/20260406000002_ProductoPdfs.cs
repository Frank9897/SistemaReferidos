using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RedGenealogica.Web.Migrations
{
    public partial class ProductoPdfs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar columnas fijas de PDF que reemplazamos con tabla
            migrationBuilder.DropColumn(name: "PdfUrl1",    table: "Productos");
            migrationBuilder.DropColumn(name: "PdfNombre1", table: "Productos");
            migrationBuilder.DropColumn(name: "PdfUrl2",    table: "Productos");
            migrationBuilder.DropColumn(name: "PdfNombre2", table: "Productos");

            // Crear tabla ProductoPdfs
            migrationBuilder.CreateTable(
                name: "ProductoPdfs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductoId  = table.Column<int>(nullable: false),
                    Nombre      = table.Column<string>(maxLength: 150, nullable: false),
                    Url         = table.Column<string>(maxLength: 350, nullable: false),
                    Orden       = table.Column<int>(nullable: false, defaultValue: 1),
                    FechaSubida = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoPdfs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoPdfs_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoPdfs_ProductoId",
                table: "ProductoPdfs",
                column: "ProductoId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProductoPdfs");

            migrationBuilder.AddColumn<string>(
                name: "PdfUrl1", table: "Productos",
                type: "character varying(350)", maxLength: 350, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "PdfNombre1", table: "Productos",
                type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "PdfUrl2", table: "Productos",
                type: "character varying(350)", maxLength: 350, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "PdfNombre2", table: "Productos",
                type: "character varying(120)", maxLength: 120, nullable: true);
        }
    }
}
