using Grpc.Core;
using Teck.Cloud.Commerce.Orders.V1;
using OrdersService = Teck.Cloud.Commerce.Orders.V1.Orders;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Fake Orders gRPC service that records requests and returns the scripted outcome.</summary>
/// <param name="script">Scripted Orders replies served to the customer gateway.</param>
/// <param name="recorder">Recorder that captures each gateway call.</param>
internal sealed class FakeOrdersService(FakeCommerceScript script, CommerceCallRecorder recorder) : OrdersService.OrdersBase
{
    /// <inheritdoc/>
    public override Task<GetCustomerOrderResponse> GetCustomerOrder(GetCustomerOrderRequest request, ServerCallContext context)
    {
        recorder.Record(new RecordedCommerceCall
        {
            Method = "GetCustomerOrder",
            OrderId = request.OrderId,
            Metadata = FakeGrpcReplies.Capture(context),
        });
        return script.NextOrder(request.OrderId) switch
        {
            CustomerOrderReply.Success success => AnsweredAsync(context, success.Trailers, new GetCustomerOrderResponse { Order = success.Order }),
            CustomerOrderReply.Failed failed => Task.FromException<GetCustomerOrderResponse>(FakeGrpcReplies.Failure(failed.DependencyError, context)),
            _ => throw new InvalidOperationException("Unknown order reply."),
        };
    }

    /// <inheritdoc/>
    public override Task<ListCustomerOrdersResponse> ListCustomerOrders(ListCustomerOrdersRequest request, ServerCallContext context)
    {
        recorder.Record(new RecordedCommerceCall
        {
            Method = "ListCustomerOrders",
            Limit = request.Limit,
            Cursor = request.Cursor,
            Metadata = FakeGrpcReplies.Capture(context),
        });
        return script.NextPage() switch
        {
            ListOrdersReply.Success success => AnsweredPageAsync(context, success),
            ListOrdersReply.Failed failed => Task.FromException<ListCustomerOrdersResponse>(FakeGrpcReplies.Failure(failed.DependencyError, context)),
            _ => throw new InvalidOperationException("Unknown page reply."),
        };
    }

    /// <inheritdoc/>
    public override Task<CreateOrderResponse> CreateOrder(CreateOrderRequest request, ServerCallContext context)
    {
        recorder.Record(new RecordedCommerceCall
        {
            Method = "CreateOrder",
            BasketRevision = request.BasketRevision,
            IdempotencyKey = request.IdempotencyKey,
            Metadata = FakeGrpcReplies.Capture(context),
        });
        return script.NextCreate() switch
        {
            CreateOrderReply.Success success => AnsweredAsync(context, success.Trailers, CreateResponse(success)),
            CreateOrderReply.Failed failed => Task.FromException<CreateOrderResponse>(FakeGrpcReplies.Failure(failed.DependencyError, context)),
            _ => throw new InvalidOperationException("Unknown create reply."),
        };
    }

    private static CreateOrderResponse CreateResponse(CreateOrderReply.Success success)
    {
        var response = new CreateOrderResponse { Order = success.Order };
        if (success.HasCheckoutUrl)
        {
            response.CheckoutUrl = success.CheckoutUrl ?? throw new InvalidOperationException("A present checkout URL must have a value.");
        }

        return response;
    }

    private static Task<T> AnsweredAsync<T>(ServerCallContext context, IReadOnlyList<string> trailers, T response)
    {
        FakeGrpcReplies.AddTrailers(context, trailers);
        return Task.FromResult(response);
    }

    private static Task<ListCustomerOrdersResponse> AnsweredPageAsync(ServerCallContext context, ListOrdersReply.Success success)
    {
        FakeGrpcReplies.AddTrailers(context, success.Trailers);
        var response = new ListCustomerOrdersResponse();
        response.Items.AddRange(success.Items);
        if (success.HasNextCursor)
        {
            response.NextCursor = success.NextCursor;
        }

        return Task.FromResult(response);
    }
}
