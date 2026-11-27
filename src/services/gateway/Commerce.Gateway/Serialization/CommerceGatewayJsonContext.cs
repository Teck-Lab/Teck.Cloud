using System.Text.Json;
using System.Text.Json.Serialization;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Serialization;

/// <summary>Generated JSON metadata for every gateway request and response body.</summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CatalogView))]
[JsonSerializable(typeof(BasketView))]
[JsonSerializable(typeof(CustomerOrderView))]
[JsonSerializable(typeof(OrderPageView))]
[JsonSerializable(typeof(CreateOrderView))]
[JsonSerializable(typeof(AdminOrderView))]
[JsonSerializable(typeof(AdminOrderPageView))]
[JsonSerializable(typeof(CommerceProblemView))]
[JsonSerializable(typeof(PutBasketBody))]
[JsonSerializable(typeof(CreateOrderBody))]
[JsonSerializable(typeof(DecisionBody))]
[JsonSerializable(typeof(PlacementEvidenceBody))]
public sealed partial class CommerceGatewayJsonContext : JsonSerializerContext;
