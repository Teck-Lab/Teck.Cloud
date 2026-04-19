// <copyright file="DatabaseServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.ProductPriceTypes.Repositories;
using Catalog.Application.Products.Repositories;
using Catalog.Application.Promotions.Repositories;
using Catalog.Application.Suppliers.Repositories;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using Catalog.Domain.Entities.ProductAggregate.Repositories;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Repositories;
using Catalog.Domain.Entities.PromotionAggregate.Repositories;
using Catalog.Domain.Entities.SupplierAggregate.Repositories;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Builder;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.EFCore;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Catalog.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extensions for configuring database-related services for the Catalog API.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Adds database contexts and repositories with CQRS pattern support.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="migrationsAssembly">The assembly containing migrations.</param>
    /// <param name="defaultWriteConnectionString">The default write connection string.</param>
    /// <param name="defaultReadConnectionString">The default read connection string.</param>
    /// <param name="provider">The deployment-selected database provider.</param>
    public static void AddCqrsDatabase(
        this WebApplicationBuilder builder,
        Assembly migrationsAssembly,
        string defaultWriteConnectionString,
        string defaultReadConnectionString,
        DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(migrationsAssembly);

        builder.AddHybridMultiTenantDbContexts<ApplicationWriteDbContext, ApplicationReadDbContext>(
            migrationsAssembly,
            defaultWriteConnectionString,
            defaultReadConnectionString,
            provider,
            serviceName: "catalog");

        // Register repositories with appropriate read/write contexts
        builder.Services.AddScoped<IBrandWriteRepository, BrandWriteRepository>();
        builder.Services.AddScoped<IBrandReadRepository, BrandReadRepository>();
        builder.Services.AddScoped<IGenericReadRepository<BrandReadModel, Guid>>(sp => sp.GetRequiredService<IBrandReadRepository>());
        builder.Services.AddScoped<IProductWriteRepository, ProductWriteRepository>();
        builder.Services.AddScoped<IProductReadRepository, ProductReadRepository>();
        builder.Services.AddScoped<ICategoryWriteRepository, CategoryWriteRepository>();
        builder.Services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
        builder.Services.AddScoped<IPromotionWriteRepository, PromotionWriteRepository>();
        builder.Services.AddScoped<IPromotionReadRepository, PromotionReadRepository>();
        builder.Services.AddScoped<IProductPriceTypeWriteRepository, ProductPriceTypeWriteRepository>();
        builder.Services.AddScoped<IProductPriceTypeReadRepository, ProductPriceTypeReadRepository>();
        builder.Services.AddScoped<ISupplierWriteRepository, SupplierWriteRepository>();
        builder.Services.AddScoped<ISupplierReadRepository, SupplierReadRepository>();
        builder.Services.AddScoped<IProductPriceReadRepository, ProductPriceReadRepository>();

        // Register UnitOfWork with the write context.
        builder.Services.AddScoped<IUnitOfWork>(sp =>
        {
            var writeDbContext = sp.GetRequiredService<ApplicationWriteDbContext>();
            return new UnitOfWork<ApplicationWriteDbContext>(writeDbContext);
        });
    }
}
