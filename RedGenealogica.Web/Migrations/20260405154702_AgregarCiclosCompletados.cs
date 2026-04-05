using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedGenealogica.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCiclosCompletados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CiclosCompletados",
                table: "Usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CiclosCompletados",
                table: "Usuarios");
        }
    }
}
