using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teck.Cloud.Migrations.MySql.Billing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBillingSchemaPostgreSQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BillingTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    PaymentMethodId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ExternalChargeId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    UpdatedBy = table.Column<string>(type: "longtext", nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    DeletedBy = table.Column<string>(type: "longtext", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingTransactions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingTransactions");
        }
    }
}
