using Testcontainers.RabbitMq;

namespace Catalog.IntegrationTests.Shared
{
    internal static class RabbitMqTestContainerFactory
    {
        public static RabbitMqContainer Create()
        {
            return new RabbitMqBuilder()
                .WithUsername("guest")
                .WithPassword("guest")
                .Build();
        }
    }
}
