// <copyright file="ProductPrice.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.ProductAggregate.Errors;
using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.ProductAggregate
{
    /// <summary>
    /// The product price.
    /// </summary>
    public class ProductPrice : BaseEntity
    {
        /// <summary>
        /// Gets the product id.
        /// </summary>
        public Guid? ProductId { get; private set; } = null!;

        /// <summary>
        /// Gets the product.
        /// </summary>
        public Product Product { get; private set; } = null!;

        /// <summary>
        /// Gets the sale price without VAT.
        /// </summary>
        public decimal SalePrice { get; private set; }

        /// <summary>
        /// Gets the currency code.
        /// </summary>
        public string? CurrencyCode { get; private set; }

        /// <summary>
        /// Gets the product price type.
        /// </summary>
        public ProductPriceType ProductPriceType { get; private set; } = null!;

        /// <summary>
        /// Gets the product price type id.
        /// </summary>
        public Guid? ProductPriceTypeId { get; private set; } = null!;

        /// <summary>
        /// Creates product price.
        /// </summary>
        /// <param name="productId">The product identifier.</param>
        /// <param name="salePrice">The sale price.</param>
        /// <param name="currencyCode">The currency code.</param>
        /// <param name="productPriceTypeId">The price type identifier.</param>
        /// <returns>The created product price, or validation errors.</returns>
        public static ErrorOr<ProductPrice> Create(
            Guid productId,
            decimal salePrice,
            string? currencyCode,
            Guid productPriceTypeId)
        {
            var errors = new List<Error>();

            ValidateSalePrice(salePrice, errors);
            ValidateCurrencyCode(currencyCode, errors);
            ValidateProductId(productId, errors);
            ValidatePriceTypeId(productPriceTypeId, errors);

            return errors.Count != 0
                ? errors
                : new ProductPrice
                {
                    ProductId = productId,
                    SalePrice = salePrice,
                    CurrencyCode = currencyCode!,
                    ProductPriceTypeId = productPriceTypeId,
                };
        }

        /// <summary>
        /// Updates product price.
        /// </summary>
        /// <param name="salePrice">The updated sale price.</param>
        /// <param name="currencyCode">The updated currency code.</param>
        /// <returns>An updated result, or validation errors.</returns>
        public ErrorOr<Updated> Update(decimal? salePrice, string? currencyCode)
        {
            var errors = new List<Error>();

            this.UpdateSalePrice(salePrice, errors);
            this.UpdateCurrencyCode(currencyCode);

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
        }

        private static void ValidateSalePrice(decimal salePrice, List<Error> errors)
        {
            if (salePrice < 0)
            {
                errors.Add(ProductPriceErrors.NegativePrice);
            }
        }

        private static void ValidateCurrencyCode(string? currencyCode, List<Error> errors)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                errors.Add(ProductPriceErrors.EmptyCurrencyCode);
            }
        }

        private static void ValidateProductId(Guid productId, List<Error> errors)
        {
            if (productId == Guid.Empty)
            {
                errors.Add(ProductPriceErrors.DefaultProductId);
            }
        }

        private static void ValidatePriceTypeId(Guid productPriceTypeId, List<Error> errors)
        {
            if (productPriceTypeId == Guid.Empty)
            {
                errors.Add(ProductPriceErrors.DefaultProductPriceTypeId);
            }
        }

        private void UpdateSalePrice(decimal? salePrice, List<Error> errors)
        {
            if (salePrice is null)
            {
                return;
            }

            if (salePrice.Value < 0)
            {
                errors.Add(ProductPriceErrors.NegativePrice);
                return;
            }

            if (!this.SalePrice.Equals(salePrice.Value))
            {
                this.SalePrice = salePrice.Value;
            }
        }

        private void UpdateCurrencyCode(string? currencyCode)
        {
            if (currencyCode is not null && !string.Equals(this.CurrencyCode, currencyCode, StringComparison.Ordinal))
            {
                this.CurrencyCode = currencyCode;
            }
        }
    }
}
