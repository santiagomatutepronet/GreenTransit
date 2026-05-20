using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLerCodeToWasteMoveResidue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdLerCode",
                table: "WasteMoveResidues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LerCodeId",
                table: "WasteMoveResidues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoveResidues_LerCodeId",
                table: "WasteMoveResidues",
                column: "LerCodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WasteMoveResidues_LERCodes_LerCodeId",
                table: "WasteMoveResidues",
                column: "LerCodeId",
                principalTable: "LERCodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WasteMoveResidues_LERCodes_LerCodeId",
                table: "WasteMoveResidues");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoveResidues_LerCodeId",
                table: "WasteMoveResidues");

            migrationBuilder.DropColumn(
                name: "IdLerCode",
                table: "WasteMoveResidues");

            migrationBuilder.DropColumn(
                name: "LerCodeId",
                table: "WasteMoveResidues");
        }
    }
}
