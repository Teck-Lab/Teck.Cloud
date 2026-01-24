# Catalog Service

Product catalog microservice for Teck.Cloud platform.

## Overview

The Catalog service manages the product catalog, including product information, categories, pricing, and inventory tracking.

## Architecture

This service follows Clean Architecture principles with the following layers:

- **Catalog.Api** - REST API endpoints and HTTP interface
- **Catalog.Application** - Business logic and use cases
- **Catalog.Domain** - Domain models and business rules
- **Catalog.Infrastructure** - Data persistence and external integrations
- **Catalog.Migrator** - Database migration tooling

## Technology Stack

- .NET 10
- Entity Framework Core
- PostgreSQL
- RabbitMQ (messaging)
- Wolverine (message handling)

## Getting Started

### Prerequisites

- .NET 10 SDK
- Podman and Podman Compose (for local dependencies)

### Running Locally

```bash
# Start infrastructure dependencies
podman-compose up -d postgres rabbitmq

# Run migrations
dotnet run --project Catalog.Migrator

# Start the API
dotnet run --project Catalog.Api
```

The API will be available at `http://localhost:8080`

## API Documentation

Once running, visit `http://localhost:8080/swagger` for interactive API documentation.

## Contributing

Contributions are welcome! Please see the main repository [contributing guidelines](../../CONTRIBUTING.md) for details.

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->
<!-- ALL-CONTRIBUTORS-LIST:END -->

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE) file for details.
