using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulpeStock.Web.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarMinimoReposicion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExistenciaMinima",
                table: "Productos",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExistenciaMinima",
                table: "Productos");
        }
    }
}
