using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedGenealogica.Web.Migrations
{
    /// <inheritdoc />
    public partial class ReferidoConvertido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Referidos_UsuarioConvertidoId",
                table: "Referidos",
                column: "UsuarioConvertidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Referidos_Usuarios_UsuarioConvertidoId",
                table: "Referidos",
                column: "UsuarioConvertidoId",
                principalTable: "Usuarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Referidos_Usuarios_UsuarioConvertidoId",
                table: "Referidos");

            migrationBuilder.DropIndex(
                name: "IX_Referidos_UsuarioConvertidoId",
                table: "Referidos");
        }
    }
}
