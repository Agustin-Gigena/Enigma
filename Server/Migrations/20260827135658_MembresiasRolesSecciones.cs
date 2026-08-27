using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enigma.Server.Migrations
{
    /// <inheritdoc />
    public partial class MembresiasRolesSecciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BorradoEn",
                table: "AspNetRoles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BorradoLogico",
                table: "AspNetRoles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreadoEn",
                table: "AspNetRoles",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "AspNetRoles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Membresias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    InstitucionId = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    BorradoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BorradoPorId = table.Column<int>(type: "int", nullable: true),
                    BorradoLogico = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Membresias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Membresias_AspNetUsers_BorradoPorId",
                        column: x => x.BorradoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Membresias_AspNetUsers_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Membresias_AspNetUsers_ModificadoPorId",
                        column: x => x.ModificadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Membresias_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Membresias_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Preservación de datos: la N:M implícita UsuarioInstitucion se copia a
            // Membresias antes de borrar la tabla (audit básico: creado por el propio
            // usuario del vínculo, sin soft-delete).
            migrationBuilder.Sql("""
                INSERT INTO Membresias (UsuarioId, InstitucionId, CreadoEn, ModificadoEn, BorradoEn, BorradoLogico, CreadoPorId)
                SELECT UsuariosId AS UsuarioId, InstitucionesId AS InstitucionId, UTC_TIMESTAMP(), NULL, NULL, 0, UsuariosId AS CreadoPorId
                FROM UsuarioInstitucion;
                """);

            migrationBuilder.DropTable(
                name: "UsuarioInstitucion");

            migrationBuilder.CreateTable(
                name: "MembresiaRol",
                columns: table => new
                {
                    MembresiaId = table.Column<int>(type: "int", nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembresiaRol", x => new { x.MembresiaId, x.RolId });
                    table.ForeignKey(
                        name: "FK_MembresiaRol_AspNetRoles_RolId",
                        column: x => x.RolId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembresiaRol_Membresias_MembresiaId",
                        column: x => x.MembresiaId,
                        principalTable: "Membresias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MembresiaRol_RolId",
                table: "MembresiaRol",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_Membresias_BorradoPorId",
                table: "Membresias",
                column: "BorradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Membresias_CreadoPorId",
                table: "Membresias",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Membresias_InstitucionId",
                table: "Membresias",
                column: "InstitucionId");

            migrationBuilder.CreateIndex(
                name: "IX_Membresias_ModificadoPorId",
                table: "Membresias",
                column: "ModificadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Membresias_UsuarioId_InstitucionId",
                table: "Membresias",
                columns: new[] { "UsuarioId", "InstitucionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembresiaRol");

            migrationBuilder.DropTable(
                name: "Membresias");

            migrationBuilder.DropColumn(
                name: "BorradoEn",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "BorradoLogico",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "CreadoEn",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "AspNetRoles");

            migrationBuilder.CreateTable(
                name: "UsuarioInstitucion",
                columns: table => new
                {
                    InstitucionesId = table.Column<int>(type: "int", nullable: false),
                    UsuariosId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioInstitucion", x => new { x.InstitucionesId, x.UsuariosId });
                    table.ForeignKey(
                        name: "FK_UsuarioInstitucion_AspNetUsers_UsuariosId",
                        column: x => x.UsuariosId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioInstitucion_Instituciones_InstitucionesId",
                        column: x => x.InstitucionesId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioInstitucion_UsuariosId",
                table: "UsuarioInstitucion",
                column: "UsuariosId");
        }
    }
}
