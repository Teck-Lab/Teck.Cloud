using Mediator;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Infrastructure.Behaviors;
using Shouldly;

namespace SharedKernel.Infrastructure.UnitTests.Behaviors
{
    public class LoggingBehaviorTests
    {
        private readonly ILogger<LoggingBehavior<TestMessage, TestResponse>> _logger;
        private readonly LoggingBehavior<TestMessage, TestResponse> _sut;

        public LoggingBehaviorTests()
        {
            _logger = Substitute.For<ILogger<LoggingBehavior<TestMessage, TestResponse>>>();
            _sut = new LoggingBehavior<TestMessage, TestResponse>(_logger);
        }

        [Fact]
        public async Task Handle_Should_LogStartAndEndMessages()
        {
            // Arrange
            var message = new TestMessage { Data = "Test" };
            var expectedResponse = new TestResponse { Result = "Success" };
            
            MessageHandlerDelegate<TestMessage, TestResponse> next = (msg, ct) =>
                new ValueTask<TestResponse>(expectedResponse);

            // Act
            var response = await _sut.Handle(message, next, CancellationToken.None);

            // Assert
            response.ShouldBe(expectedResponse);
            // Verify that Log was called with Information level at least 2 times (START and END)
            _logger.ReceivedWithAnyArgs().Log(LogLevel.Information, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Handle_Should_LogPerformanceWarning_WhenRequestTakesMoreThan3Seconds()
        {
            // Arrange
            var message = new TestMessage { Data = "Slow Test" };
            var expectedResponse = new TestResponse { Result = "Success" };
            
            MessageHandlerDelegate<TestMessage, TestResponse> next = async (msg, ct) =>
            {
                await Task.Delay(3100, ct);
                return expectedResponse;
            };

            // Act
            var response = await _sut.Handle(message, next, CancellationToken.None);

            // Assert
            response.ShouldBe(expectedResponse);
            // Verify that Log was called with Warning level
            _logger.ReceivedWithAnyArgs().Log(LogLevel.Warning, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Handle_Should_NotLogPerformanceWarning_WhenRequestIsFast()
        {
            // Arrange
            var message = new TestMessage { Data = "Fast Test" };
            var expectedResponse = new TestResponse { Result = "Success" };
            
            MessageHandlerDelegate<TestMessage, TestResponse> next = (msg, ct) =>
                new ValueTask<TestResponse>(expectedResponse);

            // Act
            var response = await _sut.Handle(message, next, CancellationToken.None);

            // Assert
            response.ShouldBe(expectedResponse);
            // Verify that Log was never called with Warning level
            _logger.DidNotReceive().Log(LogLevel.Warning, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Handle_Should_CallNextHandler()
        {
            // Arrange
            var message = new TestMessage { Data = "Test" };
            var expectedResponse = new TestResponse { Result = "Success" };
            var nextCalled = false;
            
            MessageHandlerDelegate<TestMessage, TestResponse> next = (msg, ct) =>
            {
                nextCalled = true;
                msg.ShouldBe(message);
                return new ValueTask<TestResponse>(expectedResponse);
            };

            // Act
            var response = await _sut.Handle(message, next, CancellationToken.None);

            // Assert
            nextCalled.ShouldBeTrue();
            response.ShouldBe(expectedResponse);
        }

        [Fact]
        public async Task Handle_Should_ReturnResponseFromNextHandler()
        {
            // Arrange
            var message = new TestMessage { Data = "Test" };
            var expectedResponse = new TestResponse { Result = "Handler Result" };
            
            MessageHandlerDelegate<TestMessage, TestResponse> next = (msg, ct) =>
                new ValueTask<TestResponse>(expectedResponse);

            // Act
            var response = await _sut.Handle(message, next, CancellationToken.None);

            // Assert
            response.Result.ShouldBe("Handler Result");
        }
    }

    #pragma warning disable CA1515
    public class TestMessage : IMessage
    {
        public string Data { get; set; } = string.Empty;
    }

    public class TestResponse
    {
        public string Result { get; set; } = string.Empty;
    }
    #pragma warning restore CA1515
}
