using System.Data;
using Mediator;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using SharedKernel.Infrastructure.Behaviors;
using Shouldly;

namespace SharedKernel.Infrastructure.UnitTests.Behaviors
{
    public class TransactionalBehaviorTests
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TransactionalBehavior<TestTransactionalCommand, TestTransactionalResponse>> _logger;
        private readonly TransactionalBehavior<TestTransactionalCommand, TestTransactionalResponse> _sut;
        private readonly IDbTransaction _transaction;

        public TransactionalBehaviorTests()
        {
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _logger = Substitute.For<ILogger<TransactionalBehavior<TestTransactionalCommand, TestTransactionalResponse>>>();
            _transaction = Substitute.For<IDbTransaction>();
            _sut = new TransactionalBehavior<TestTransactionalCommand, TestTransactionalResponse>(_unitOfWork, _logger);
        }

        [Fact]
        public async Task Handle_Should_BeginTransaction()
        {
            // Arrange
            var command = new TestTransactionalCommand { Data = "Test" };
            var expectedResponse = new TestTransactionalResponse { Result = "Success" };
            
            _unitOfWork.BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>())
                .Returns(_transaction);
            
            MessageHandlerDelegate<TestTransactionalCommand, TestTransactionalResponse> next = (msg, ct) =>
                new ValueTask<TestTransactionalResponse>(expectedResponse);

            // Act
            await _sut.Handle(command, next, CancellationToken.None);

            // Assert
            await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_CommitTransaction_WhenSuccessful()
        {
            // Arrange
            var command = new TestTransactionalCommand { Data = "Test" };
            var expectedResponse = new TestTransactionalResponse { Result = "Success" };
            
            _unitOfWork.BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>())
                .Returns(_transaction);
            
            MessageHandlerDelegate<TestTransactionalCommand, TestTransactionalResponse> next = (msg, ct) =>
                new ValueTask<TestTransactionalResponse>(expectedResponse);

            // Act
            await _sut.Handle(command, next, CancellationToken.None);

            // Assert
            _transaction.Received(1).Commit();
        }

        [Fact]
        public async Task Handle_Should_ReturnResponse()
        {
            // Arrange
            var command = new TestTransactionalCommand { Data = "Test" };
            var expectedResponse = new TestTransactionalResponse { Result = "Handler Result" };
            
            _unitOfWork.BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>())
                .Returns(_transaction);
            
            MessageHandlerDelegate<TestTransactionalCommand, TestTransactionalResponse> next = (msg, ct) =>
                new ValueTask<TestTransactionalResponse>(expectedResponse);

            // Act
            var response = await _sut.Handle(command, next, CancellationToken.None);

            // Assert
            response.ShouldBe(expectedResponse);
        }

        [Fact]
        public async Task Handle_Should_LogBeginningAndCommitMessages()
        {
            // Arrange
            var command = new TestTransactionalCommand { Data = "Test" };
            var expectedResponse = new TestTransactionalResponse { Result = "Success" };
            
            _unitOfWork.BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>())
                .Returns(_transaction);
            
            MessageHandlerDelegate<TestTransactionalCommand, TestTransactionalResponse> next = (msg, ct) =>
                new ValueTask<TestTransactionalResponse>(expectedResponse);

            // Act
            await _sut.Handle(command, next, CancellationToken.None);

            // Assert
            // Verify that Log was called with Information level at least 2 times
            _logger.ReceivedWithAnyArgs(2).Log(LogLevel.Information, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Handle_Should_CallNextHandler_WithCorrectParameters()
        {
            // Arrange
            var command = new TestTransactionalCommand { Data = "Test" };
            var expectedResponse = new TestTransactionalResponse { Result = "Success" };
            var nextCalled = false;
            
            _unitOfWork.BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>())
                .Returns(_transaction);
            
            MessageHandlerDelegate<TestTransactionalCommand, TestTransactionalResponse> next = (msg, ct) =>
            {
                nextCalled = true;
                msg.ShouldBe(command);
                return new ValueTask<TestTransactionalResponse>(expectedResponse);
            };

            // Act
            await _sut.Handle(command, next, CancellationToken.None);

            // Assert
            nextCalled.ShouldBeTrue();
        }

        [Fact]
        public async Task Handle_Should_DisposeTransaction()
        {
            // Arrange
            var command = new TestTransactionalCommand { Data = "Test" };
            var expectedResponse = new TestTransactionalResponse { Result = "Success" };
            
            _unitOfWork.BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>())
                .Returns(_transaction);
            
            MessageHandlerDelegate<TestTransactionalCommand, TestTransactionalResponse> next = (msg, ct) =>
                new ValueTask<TestTransactionalResponse>(expectedResponse);

            // Act
            await _sut.Handle(command, next, CancellationToken.None);

            // Assert
            _transaction.Received(1).Dispose();
        }
    }

    #pragma warning disable CA1515
    public class TestTransactionalCommand : ITransactionalCommand<TestTransactionalResponse>
    {
        public string Data { get; set; } = string.Empty;
    }

    public class TestTransactionalResponse
    {
        public string Result { get; set; } = string.Empty;
    }
    #pragma warning restore CA1515
}
