namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Translates a tenant-owned domain event without overriding Wolverine's envelope.</summary>
public static class TenantRecordCreatedHandler
{
    /// <summary>Creates the separate external contract.</summary>
    /// <param name="message">The internal event.</param>
    /// <returns>The cascading external event.</returns>
    public static TenantRecordPublished Handle(TenantRecordCreated message) => new() { RecordId = message.RecordId, TenantId = message.TenantId };
}
