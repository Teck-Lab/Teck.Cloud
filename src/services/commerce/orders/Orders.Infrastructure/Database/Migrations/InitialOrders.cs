using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Migrations;

/// <summary>Creates the initial orders schema and its durable provider/probe work tables.</summary>
[DbContext(typeof(OrdersWriteDbContext))]
[Migration("20261127000000_InitialOrders")]
public sealed class InitialOrders : Migration
{
    private static readonly string[] BasketsSqlLines = [
        "CREATE SCHEMA IF NOT EXISTS orders;",
        string.Empty,
        "CREATE TABLE IF NOT EXISTS orders.\"Baskets\" (",
        "    \"Id\" uuid NOT NULL,",
        "    \"CreatedAt\" timestamp with time zone NOT NULL,",
        "    \"CreatedBy\" text NULL,",
        "    \"UpdatedOn\" timestamp with time zone NULL,",
        "    \"UpdatedBy\" text NULL,",
        "    \"DeletedOn\" timestamp with time zone NULL,",
        "    \"DeletedBy\" text NULL,",
        "    \"IsDeleted\" boolean NOT NULL,",
        "    \"TenantId\" text NOT NULL,",
        "    \"SubjectId\" text NOT NULL,",
        "    \"Revision\" bigint NOT NULL,",
        "    \"OfferId\" text NULL,",
        "    \"AddOnCodesCsv\" text NOT NULL,",
        "    \"TotalAmountMinor\" bigint NULL,",
        "    \"TotalCurrency\" text NULL,",
        "    CONSTRAINT \"PK_Baskets\" PRIMARY KEY (\"Id\")",
        ");",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Baskets_TenantId_SubjectId\" ON orders.\"Baskets\" (\"TenantId\", \"SubjectId\");",
        string.Empty,
    ];

    private static readonly string[] OrdersSqlLines = [
        "CREATE TABLE IF NOT EXISTS orders.\"Orders\" (",
        "    \"Id\" uuid NOT NULL,",
        "    \"CreatedAt\" timestamp with time zone NOT NULL,",
        "    \"CreatedBy\" text NULL,",
        "    \"UpdatedOn\" timestamp with time zone NULL,",
        "    \"UpdatedBy\" text NULL,",
        "    \"DeletedOn\" timestamp with time zone NULL,",
        "    \"DeletedBy\" text NULL,",
        "    \"IsDeleted\" boolean NOT NULL,",
        "    \"TenantId\" text NOT NULL,",
        "    \"SubjectId\" text NOT NULL,",
        "    \"SubscriptionId\" uuid NOT NULL,",
        "    \"OfferId\" text NOT NULL,",
        "    \"Tier\" integer NOT NULL,",
        "    \"AddOnCodesCsv\" text NOT NULL,",
        "    \"TotalAmountMinor\" bigint NOT NULL,",
        "    \"Currency\" text NOT NULL,",
        "    \"Revision\" bigint NOT NULL,",
        "    \"SortKey\" text NOT NULL,",
        "    \"CheckoutRequestedAt\" timestamp with time zone NOT NULL,",
        "    \"CheckoutCreateAttempts\" integer NOT NULL,",
        "    \"LastCheckoutAttemptAt\" timestamp with time zone NULL,",
        "    \"CatalogPriceSnapshot\" text NOT NULL,",
        "    \"VerifiedPriceSnapshot\" text NULL,",
        "    \"ProviderIdempotencyKey\" text NOT NULL,",
        "    \"CheckoutProviderSessionId\" text NULL,",
        "    \"CheckoutUrl\" text NULL,",
        "    \"CheckoutSessionBoundAt\" timestamp with time zone NULL,",
        "    \"CheckoutLeaseToken\" uuid NULL,",
        "    \"CheckoutLeaseUntil\" timestamp with time zone NULL,",
        "    \"CheckoutLastErrorCategory\" text NULL,",
        "    \"CheckoutProviderMode\" text NULL,",
        "    \"ProviderAccountId\" text NULL,",
        "    \"PaymentStatus\" integer NOT NULL,",
        "    \"PaidThrough\" timestamp with time zone NULL,",
        "    \"ProviderSubscriptionId\" text NULL,",
        "    \"ProviderInvoiceId\" text NULL,",
        "    \"ProviderChargeId\" text NULL,",
        "    \"ProviderPaymentIntentId\" text NULL,",
        "    \"ProviderMode\" text NULL,",
        "    \"VerifiedPriceKey\" text NULL,",
        "    \"PaymentVerifiedAt\" timestamp with time zone NULL,",
        "    \"ApprovalStatus\" integer NOT NULL,",
        "    \"ApprovalActorId\" text NULL,",
        "    \"ApprovalReason\" text NULL,",
        "    \"ApprovalOccurredAt\" timestamp with time zone NULL,",
        "    \"EvidenceRevision\" bigint NOT NULL,",
        "    \"EvidencePlacementId\" text NULL,",
        "    \"EvidenceReference\" text NULL,",
        "    \"EvidenceRecordedAt\" timestamp with time zone NULL,",
        "    \"FulfillmentStatus\" integer NOT NULL,",
        "    \"ProofEvidenceRevision\" bigint NOT NULL,",
        "    \"ProofPlacementId\" text NULL,",
        "    \"ProofObservedAt\" timestamp with time zone NULL,",
        "    \"ProofFailureCode\" integer NULL,",
        "    \"DesiredRevision\" bigint NOT NULL,",
        "    \"DesiredEnabled\" boolean NOT NULL,",
        "    \"DesiredTier\" integer NOT NULL,",
        "    \"DesiredAddOnCodesCsv\" text NOT NULL,",
        "    \"DesiredNotAfter\" timestamp with time zone NULL,",
        "    \"DesiredUpdatedAt\" timestamp with time zone NULL,",
        "    \"LicenseState\" integer NOT NULL,",
        "    \"LicenseRevision\" bigint NULL,",
        "    \"LicenseKeyId\" text NULL,",
        "    \"LicenseExpiresAt\" timestamp with time zone NULL,",
        "    \"AppliedDesiredRevision\" bigint NOT NULL,",
        "    CONSTRAINT \"PK_Orders\" PRIMARY KEY (\"Id\")",
        ");",
        "CREATE INDEX IF NOT EXISTS \"IX_Orders_Tenant_Subject_Updated_Sort\"",
        "    ON orders.\"Orders\" (\"TenantId\", \"SubjectId\", \"UpdatedOn\", \"SortKey\");",
        "CREATE INDEX IF NOT EXISTS \"IX_Orders_Tenant_Session\" ON orders.\"Orders\" (\"TenantId\", \"CheckoutProviderSessionId\");",
        "CREATE INDEX IF NOT EXISTS \"IX_Orders_Tenant_Subscription\" ON orders.\"Orders\" (\"TenantId\", \"ProviderSubscriptionId\");",
        "CREATE INDEX IF NOT EXISTS \"IX_Orders_Tenant_Invoice\" ON orders.\"Orders\" (\"TenantId\", \"ProviderInvoiceId\");",
        "CREATE INDEX IF NOT EXISTS \"IX_Orders_Tenant_Charge\" ON orders.\"Orders\" (\"TenantId\", \"ProviderChargeId\");",
        "CREATE INDEX IF NOT EXISTS \"IX_Orders_Tenant_PaymentIntent\" ON orders.\"Orders\" (\"TenantId\", \"ProviderPaymentIntentId\");",
        "CREATE INDEX IF NOT EXISTS \"IX_Orders_DesiredEnabled\" ON orders.\"Orders\" (\"DesiredEnabled\");",
        string.Empty,
    ];

    private static readonly string[] OrderAuditSqlLines = [
        "CREATE TABLE IF NOT EXISTS orders.\"OrderAudit\" (",
        "    \"Id\" uuid NOT NULL,",
        "    \"CreatedAt\" timestamp with time zone NOT NULL,",
        "    \"CreatedBy\" text NULL,",
        "    \"UpdatedOn\" timestamp with time zone NULL,",
        "    \"UpdatedBy\" text NULL,",
        "    \"DeletedOn\" timestamp with time zone NULL,",
        "    \"DeletedBy\" text NULL,",
        "    \"IsDeleted\" boolean NOT NULL,",
        "    \"TenantId\" text NOT NULL,",
        "    \"OrderId\" uuid NOT NULL,",
        "    \"ActorId\" text NOT NULL,",
        "    \"Action\" integer NOT NULL,",
        "    \"Reason\" text NULL,",
        "    \"OccurredAt\" timestamp with time zone NOT NULL,",
        "    CONSTRAINT \"PK_OrderAudit\" PRIMARY KEY (\"Id\"),",
        "    CONSTRAINT \"FK_OrderAudit_Orders_OrderId\" FOREIGN KEY (\"OrderId\") REFERENCES orders.\"Orders\" (\"Id\") ON DELETE RESTRICT",
        ");",
        "CREATE INDEX IF NOT EXISTS \"IX_OrderAudit_Tenant_Order_Occurred\"",
        "    ON orders.\"OrderAudit\" (\"TenantId\", \"OrderId\", \"OccurredAt\");",
        string.Empty,
    ];

    private static readonly string[] IdempotencyReceiptsSqlLines = [
        "CREATE TABLE IF NOT EXISTS orders.\"IdempotencyReceipts\" (",
        "    \"Id\" uuid NOT NULL,",
        "    \"CreatedAt\" timestamp with time zone NOT NULL,",
        "    \"CreatedBy\" text NULL,",
        "    \"UpdatedOn\" timestamp with time zone NULL,",
        "    \"UpdatedBy\" text NULL,",
        "    \"DeletedOn\" timestamp with time zone NULL,",
        "    \"DeletedBy\" text NULL,",
        "    \"IsDeleted\" boolean NOT NULL,",
        "    \"TenantId\" text NOT NULL,",
        "    \"SubjectId\" text NOT NULL,",
        "    \"IdempotencyKey\" text NOT NULL,",
        "    \"BasketRevision\" bigint NOT NULL,",
        "    \"OrderId\" uuid NOT NULL,",
        "    CONSTRAINT \"PK_IdempotencyReceipts\" PRIMARY KEY (\"Id\")",
        ");",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_IdempotencyReceipts_Tenant_Subject_Key\"",
        "    ON orders.\"IdempotencyReceipts\" (\"TenantId\", \"SubjectId\", \"IdempotencyKey\");",
        "CREATE INDEX IF NOT EXISTS \"IX_IdempotencyReceipts_OrderId\" ON orders.\"IdempotencyReceipts\" (\"OrderId\");",
        string.Empty,
    ];

    private static readonly string[] PlacementProbeWorkSqlLines = [
        "CREATE TABLE IF NOT EXISTS orders.\"PlacementProbeWork\" (",
        "    \"Id\" uuid NOT NULL,",
        "    \"CreatedAt\" timestamp with time zone NOT NULL,",
        "    \"CreatedBy\" text NULL,",
        "    \"UpdatedOn\" timestamp with time zone NULL,",
        "    \"UpdatedBy\" text NULL,",
        "    \"DeletedOn\" timestamp with time zone NULL,",
        "    \"DeletedBy\" text NULL,",
        "    \"IsDeleted\" boolean NOT NULL,",
        "    \"TenantId\" text NOT NULL,",
        "    \"OrderId\" uuid NOT NULL,",
        "    \"EvidenceRevision\" bigint NOT NULL,",
        "    \"PlacementId\" text NOT NULL,",
        "    \"DueAt\" timestamp with time zone NULL,",
        "    \"LeaseToken\" uuid NULL,",
        "    \"LeaseUntil\" timestamp with time zone NULL,",
        "    \"Terminal\" boolean NOT NULL,",
        "    \"LastFailureCode\" integer NULL,",
        "    CONSTRAINT \"PK_PlacementProbeWork\" PRIMARY KEY (\"Id\")",
        ");",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PlacementProbeWork_Tenant_Order\"",
        "    ON orders.\"PlacementProbeWork\" (\"TenantId\", \"OrderId\");",
        "CREATE INDEX IF NOT EXISTS \"IX_PlacementProbeWork_Terminal_Due\" ON orders.\"PlacementProbeWork\" (\"Terminal\", \"DueAt\");",
        string.Empty,
    ];

    private static readonly string[] ProviderEventReceiptsSqlLines = [
        "CREATE TABLE IF NOT EXISTS orders.\"ProviderEventReceipts\" (",
        "    \"Id\" uuid NOT NULL,",
        "    \"CreatedAt\" timestamp with time zone NOT NULL,",
        "    \"CreatedBy\" text NULL,",
        "    \"UpdatedOn\" timestamp with time zone NULL,",
        "    \"UpdatedBy\" text NULL,",
        "    \"DeletedOn\" timestamp with time zone NULL,",
        "    \"DeletedBy\" text NULL,",
        "    \"IsDeleted\" boolean NOT NULL,",
        "    \"AccountId\" text NOT NULL,",
        "    \"EventId\" text NOT NULL,",
        "    \"EventType\" text NOT NULL,",
        "    \"PayloadHash\" text NOT NULL,",
        "    \"ReceivedAt\" timestamp with time zone NOT NULL,",
        "    \"Status\" integer NOT NULL,",
        "    \"Attempts\" integer NOT NULL,",
        "    \"ProcessedAt\" timestamp with time zone NULL,",
        "    \"OrderId\" uuid NULL,",
        "    \"TenantId\" text NULL,",
        "    \"LastErrorCategory\" text NULL,",
        "    CONSTRAINT \"PK_ProviderEventReceipts\" PRIMARY KEY (\"Id\")",
        ");",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ProviderEventReceipts_Account_Event\"",
        "    ON orders.\"ProviderEventReceipts\" (\"AccountId\", \"EventId\");",
        "CREATE INDEX IF NOT EXISTS \"IX_ProviderEventReceipts_OrderId\" ON orders.\"ProviderEventReceipts\" (\"OrderId\");",
        string.Empty,
    ];

    private static readonly string[] ReconciliationJobsSqlLines = [
        "CREATE TABLE IF NOT EXISTS orders.\"ReconciliationJobs\" (",
        "    \"Id\" uuid NOT NULL,",
        "    \"CreatedAt\" timestamp with time zone NOT NULL,",
        "    \"CreatedBy\" text NULL,",
        "    \"UpdatedOn\" timestamp with time zone NULL,",
        "    \"UpdatedBy\" text NULL,",
        "    \"DeletedOn\" timestamp with time zone NULL,",
        "    \"DeletedBy\" text NULL,",
        "    \"IsDeleted\" boolean NOT NULL,",
        "    \"TenantId\" text NOT NULL,",
        "    \"OrderId\" uuid NOT NULL,",
        "    \"Kind\" integer NOT NULL,",
        "    \"Status\" integer NOT NULL,",
        "    \"DueAt\" timestamp with time zone NOT NULL,",
        "    \"Attempts\" integer NOT NULL,",
        "    \"LeaseToken\" uuid NULL,",
        "    \"LeaseUntil\" timestamp with time zone NULL,",
        "    \"CompletedAt\" timestamp with time zone NULL,",
        "    \"LastErrorCategory\" text NULL,",
        "    \"ProviderEventReceiptId\" uuid NULL,",
        "    CONSTRAINT \"PK_ReconciliationJobs\" PRIMARY KEY (\"Id\")",
        ");",
        "CREATE INDEX IF NOT EXISTS \"IX_ReconciliationJobs_ProviderEventReceiptId\"",
        "    ON orders.\"ReconciliationJobs\" (\"ProviderEventReceiptId\");",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ReconciliationJobs_Tenant_Order_Kind_Pending\"",
        "    ON orders.\"ReconciliationJobs\" (\"TenantId\", \"OrderId\", \"Kind\") WHERE \"Status\" = 1;",
        "CREATE INDEX IF NOT EXISTS \"IX_ReconciliationJobs_Status_Due\" ON orders.\"ReconciliationJobs\" (\"Status\", \"DueAt\");",
    ];

    private static readonly string[] DownSqlLines = [
        "DROP TABLE IF EXISTS orders.\"ReconciliationJobs\";",
        "DROP TABLE IF EXISTS orders.\"ProviderEventReceipts\";",
        "DROP TABLE IF EXISTS orders.\"PlacementProbeWork\";",
        "DROP TABLE IF EXISTS orders.\"IdempotencyReceipts\";",
        "DROP TABLE IF EXISTS orders.\"OrderAudit\";",
        "DROP TABLE IF EXISTS orders.\"Orders\";",
        "DROP TABLE IF EXISTS orders.\"Baskets\";",
    ];

    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        _ = migrationBuilder.Sql(string.Join('\n', BasketsSqlLines));
        _ = migrationBuilder.Sql(string.Join('\n', OrdersSqlLines));
        _ = migrationBuilder.Sql(string.Join('\n', OrderAuditSqlLines));
        _ = migrationBuilder.Sql(string.Join('\n', IdempotencyReceiptsSqlLines));
        _ = migrationBuilder.Sql(string.Join('\n', PlacementProbeWorkSqlLines));
        _ = migrationBuilder.Sql(string.Join('\n', ProviderEventReceiptsSqlLines));
        _ = migrationBuilder.Sql(string.Join('\n', ReconciliationJobsSqlLines));
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        _ = migrationBuilder.Sql(string.Join('\n', DownSqlLines));
    }
}
