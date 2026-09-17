using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrestamosCobros.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoCredito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PermitirConfigCorreo",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirConfigWhatsApp",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirEnviarNotificaciones",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirGestionClientes",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirGestionPersonal",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirGestionPrestamos",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirPapelera",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirRegistroGastos",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirRegistroPagos",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirVerAuditoria",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirVerReportes",
                table: "Usuarios",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Usuarios",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoCredito",
                table: "Prestamos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ConfiguracionesWhatsApp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhoneNumberId = table.Column<string>(type: "TEXT", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesWhatsApp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MensajesRecibidos",
                columns: table => new
                {
                    MensajeRecibidoId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: true),
                    TelefonoRemitente = table.Column<string>(type: "TEXT", nullable: false),
                    TipoMensaje = table.Column<string>(type: "TEXT", nullable: false),
                    Contenido = table.Column<string>(type: "TEXT", nullable: false),
                    MetaMediaId = table.Column<string>(type: "TEXT", nullable: false),
                    FechaRecibido = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Leido = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesRecibidos", x => x.MensajeRecibidoId);
                    table.ForeignKey(
                        name: "FK_MensajesRecibidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "ClienteId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesRecibidos_ClienteId",
                table: "MensajesRecibidos",
                column: "ClienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesWhatsApp");

            migrationBuilder.DropTable(
                name: "MensajesRecibidos");

            migrationBuilder.DropColumn(
                name: "PermitirConfigCorreo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirConfigWhatsApp",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirEnviarNotificaciones",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirGestionClientes",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirGestionPersonal",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirGestionPrestamos",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirPapelera",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirRegistroGastos",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirRegistroPagos",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirVerAuditoria",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermitirVerReportes",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TipoCredito",
                table: "Prestamos");
        }
    }
}
