using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyToUserEDCConnector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "UserEDCConnector",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "UserEDCConnector");
        }
    }
}
