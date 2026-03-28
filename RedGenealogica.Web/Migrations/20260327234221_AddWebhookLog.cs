using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedGenealogica.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Referidos_Usuarios_UsuarioConvertidoId",
                table: "Referidos");

            migrationBuilder.AddColumn<bool>(
                name: "Confirmado",
                table: "Pagos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Monto",
                table: "MovimientosPuntos",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Nivel",
                table: "MovimientosPuntos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Referidos_Usuarios_UsuarioConvertidoId",
                table: "Referidos",
                column: "UsuarioConvertidoId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Referidos_Usuarios_UsuarioConvertidoId",
                table: "Referidos");

            migrationBuilder.DropColumn(
                name: "Confirmado",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "Monto",
                table: "MovimientosPuntos");

            migrationBuilder.DropColumn(
                name: "Nivel",
                table: "MovimientosPuntos");

            migrationBuilder.AddForeignKey(
                name: "FK_Referidos_Usuarios_UsuarioConvertidoId",
                table: "Referidos",
                column: "UsuarioConvertidoId",
                principalTable: "Usuarios",
                principalColumn: "Id");
        }
    }
}
