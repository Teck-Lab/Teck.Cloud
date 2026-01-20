
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Catalog.IntegrationTests.Shared
{
    public class SharedTestcontainersFixture : IAsyncLifetime
    {
        public PostgreSqlContainer DbContainer { get; }
        public RabbitMqContainer RabbitMqContainer { get; }

        public SharedTestcontainersFixture()
        {
            DbContainer = new PostgreSqlBuilder("postgres:latest")
                .WithDatabase("testdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
            RabbitMqContainer = RabbitMqTestContainerFactory.Create();
        }

        public async ValueTask InitializeAsync()
        {
            await DbContainer.StartAsync();
            await RabbitMqContainer.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await DbContainer.DisposeAsync();
            await RabbitMqContainer.DisposeAsync();
        }
    }
}
