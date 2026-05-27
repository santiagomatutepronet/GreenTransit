using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExplorerLayoutConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "UserEDCConnector",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ExplorerLayoutConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProviderParticipantId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DatasetName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LayoutConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    SchemaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExplorerLayoutConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExplorerLayoutConfigs_OwnerId",
                table: "ExplorerLayoutConfigs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "UQ_ExplorerLayoutConfigs_Tenant_User_Asset",
                table: "ExplorerLayoutConfigs",
                columns: new[] { "OwnerId", "UserId", "AssetId", "ProviderParticipantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExplorerLayoutConfigs");

            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "UserEDCConnector",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
