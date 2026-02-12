using Testcontainers.RabbitMq;

namespace Catalog.IntegrationTests.Shared
{
    internal static class RabbitMqTestContainerFactory
    {
        public static RabbitMqContainer Create()
        {
            return new RabbitMqBuilder("rabbitmq:3.11-management")
                .WithUsername("guest")
                .WithPassword("guest")
                .Build();
        }
    }
}

