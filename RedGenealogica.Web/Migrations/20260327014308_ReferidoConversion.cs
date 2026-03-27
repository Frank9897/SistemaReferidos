using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedGenealogica.Web.Migrations
{
    /// <inheritdoc />
    public partial class ReferidoConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UsuarioConvertidoId",
                table: "Referidos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsuarioConvertidoId",
                table: "Referidos");
        }
    }
}
