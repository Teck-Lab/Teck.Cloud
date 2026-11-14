using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using SharedKernel.Core.Events;
using Shouldly;
using Xunit;

namespace SharedKernel.Core.UnitTests;

/// <summary>Checks event metadata survives source-generated serialization and replay.</summary>
public sealed partial class EventSerializationTests
{
    /// <summary>Deserialization must preserve the producer's identity and occurrence time.</summary>
    [Fact]
    public void EventIdentitySurvivesReplay()
    {
        var data = new Faker { Random = new Randomizer(873) };
        var trace = data.Random.AlphaNumeric(32);
        var original = new ReplayEvent
        {
            Id = Guid.NewGuid(),
            CreatedOn = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            MetaData = new Dictionary<string, string>(StringComparer.Ordinal) { ["trace"] = trace },
        };
        var json = JsonSerializer.Serialize(original, EventJsonContext.Default.ReplayEvent);
        var replay = JsonSerializer.Deserialize(json, EventJsonContext.Default.ReplayEvent);
        _ = replay.ShouldNotBeNull();
        replay.Id.ShouldBe(original.Id);
        replay.CreatedOn.ShouldBe(original.CreatedOn);
        replay.MetaData["trace"].ShouldBe(trace);
    }

    /// <summary>Public integration contract used to exercise generated JSON metadata.</summary>
    public sealed class ReplayEvent : IntegrationEvent;

    [JsonSerializable(typeof(ReplayEvent))]
    private sealed partial class EventJsonContext : JsonSerializerContext;
}
