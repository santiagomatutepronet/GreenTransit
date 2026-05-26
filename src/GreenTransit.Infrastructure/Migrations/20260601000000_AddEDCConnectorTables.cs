using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEDCConnectorTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las tablas UserEDCConnector y ProfileEDCConsumer ya existen en BD.
            // Esta migración solo registra el historial en __EFMigrationsHistory.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se eliminan las tablas en Down para no afectar entornos donde ya existen datos.
        }
    }
}
