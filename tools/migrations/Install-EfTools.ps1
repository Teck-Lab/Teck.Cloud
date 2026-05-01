# Install-EfTools.ps1
# This script installs EF Core tools and ensures migration projects have required design-time packages.

# Install the global EF Core tools if not already installed
if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) {
    Write-Host "Installing dotnet-ef global tool..." -ForegroundColor Cyan
    dotnet tool install --global dotnet-ef
}

# Infrastructure projects (contexts)
$infrastructureProjects = @(
    "src/services/basket/Basket.Infrastructure/Basket.Infrastructure.csproj",
    "src/services/catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj",
    "src/services/customer/Customer.Infrastructure/Customer.Infrastructure.csproj",
    "src/services/order/Order.Infrastructure/Order.Infrastructure.csproj"
)

# Provider-specific migration projects
$migrationProjects = @(
    "src/services/basket/Basket.Infrastructure.Migrations.PostgreSQL/Basket.Infrastructure.Migrations.PostgreSQL.csproj",
    "src/services/basket/Basket.Infrastructure.Migrations.SqlServer/Basket.Infrastructure.Migrations.SqlServer.csproj",
    "src/services/basket/Basket.Infrastructure.Migrations.MySql/Basket.Infrastructure.Migrations.MySql.csproj",
    "src/services/catalog/Catalog.Infrastructure.Migrations.PostgreSQL/Catalog.Infrastructure.Migrations.PostgreSQL.csproj",
    "src/services/catalog/Catalog.Infrastructure.Migrations.SqlServer/Catalog.Infrastructure.Migrations.SqlServer.csproj",
    "src/services/catalog/Catalog.Infrastructure.Migrations.MySql/Catalog.Infrastructure.Migrations.MySql.csproj",
    "src/services/customer/Customer.Infrastructure.Migrations.PostgreSQL/Customer.Infrastructure.Migrations.PostgreSQL.csproj",
    "src/services/customer/Customer.Infrastructure.Migrations.SqlServer/Customer.Infrastructure.Migrations.SqlServer.csproj",
    "src/services/customer/Customer.Infrastructure.Migrations.MySql/Customer.Infrastructure.Migrations.MySql.csproj",
    "src/services/order/Order.Infrastructure.Migrations.PostgreSQL/Order.Infrastructure.Migrations.PostgreSQL.csproj",
    "src/services/order/Order.Infrastructure.Migrations.SqlServer/Order.Infrastructure.Migrations.SqlServer.csproj",
    "src/services/order/Order.Infrastructure.Migrations.MySql/Order.Infrastructure.Migrations.MySql.csproj"
)

foreach ($project in $infrastructureProjects) {
    if (-not (Test-Path $project)) {
        Write-Warning "Project not found, skipping: $project"
        continue
    }

    Write-Host "Adding EF Core migration packages to $project ..." -ForegroundColor Cyan
    dotnet add $project package Microsoft.EntityFrameworkCore.Design
    dotnet add $project package Microsoft.EntityFrameworkCore.Tools
}

foreach ($project in $migrationProjects) {
    if (-not (Test-Path $project)) {
        Write-Warning "Migration project not found, skipping package install: $project"
        continue
    }

    Write-Host "Adding EF Core design package to $project ..." -ForegroundColor Cyan
    dotnet add $project package Microsoft.EntityFrameworkCore.Design
}

Write-Host "EF Core tools installed successfully!" -ForegroundColor Green
Write-Host "`nYou can now use Add-Migration.ps1 / Remove-Migration.ps1 with provider-specific migration projects." -ForegroundColor Green
