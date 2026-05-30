using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basket.Infrastructure.Migrations.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBasketDraftsSqlServer : global::Microsoft.EntityFrameworkCore.Migrations.Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BasketDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketDraftLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BasketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketDraftLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BasketDraftLines_BasketDrafts_BasketId",
                        column: x => x.BasketId,
                        principalTable: "BasketDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BasketDraftLines_BasketId",
                table: "BasketDraftLines",
                column: "BasketId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketDrafts_TenantId_CustomerId",
                table: "BasketDrafts",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BasketDraftLines");

            migrationBuilder.DropTable(
                name: "BasketDrafts");
        }
    }
}
