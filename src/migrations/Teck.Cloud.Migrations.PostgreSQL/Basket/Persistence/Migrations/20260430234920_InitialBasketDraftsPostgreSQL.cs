using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Basket.Infrastructure.Migrations.PostgreSQL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBasketDraftsPostgreSQL : global::Microsoft.EntityFrameworkCore.Migrations.Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BasketDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketDraftLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
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
