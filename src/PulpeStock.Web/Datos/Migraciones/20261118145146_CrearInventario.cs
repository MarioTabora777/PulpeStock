using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulpeStock.Web.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class CrearInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Marca = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Presentacion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CodigoBarras = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnidadesSueltas = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalUnidades = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conteos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conteos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaquetesContados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    UnidadesPorPaquete = table.Column<int>(type: "INTEGER", nullable: false),
                    ConteoInventarioId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaquetesContados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaquetesContados_Conteos_ConteoInventarioId",
                        column: x => x.ConteoInventarioId,
                        principalTable: "Conteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conteos_ProductoId_FechaUtc_Id",
                table: "Conteos",
                columns: new[] { "ProductoId", "FechaUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PaquetesContados_ConteoInventarioId",
                table: "PaquetesContados",
                column: "ConteoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos",
                column: "CodigoBarras",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaquetesContados");

            migrationBuilder.DropTable(
                name: "Conteos");

            migrationBuilder.DropTable(
                name: "Productos");
        }
    }
}
