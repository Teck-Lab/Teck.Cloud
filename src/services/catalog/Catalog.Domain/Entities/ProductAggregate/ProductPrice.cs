using Catalog.Domain.Entities.ProductAggregate.Errors;
using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using ErrorOr;
using Finbuckle.MultiTenant;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.ProductAggregate
{
    /// <summary>
    /// The product price.
    /// </summary>
    [MultiTenant]
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
        /// Update product price.
        /// </summary>
        /// <param name="salePrice"></param>
        /// <param name="currencyCode"></param>
        /// <returns></returns>
        public ErrorOr<Updated> Update(decimal? salePrice, string? currencyCode)
        {
            var errors = new List<Error>();

            if (salePrice.HasValue)
            {
                if (salePrice.Value < 0)
                {
                    errors.Add(ProductPriceErrors.NegativePrice);
                }
                else if (!SalePrice.Equals(salePrice.Value))
                {
                    SalePrice = salePrice.Value;
                }
            }

            if (currencyCode is not null && !string.Equals(CurrencyCode, currencyCode, StringComparison.Ordinal))
            {
                CurrencyCode = currencyCode;
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            return Result.Updated;
        }

        /// <summary>
        /// Create product price.
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="salePrice"></param>
        /// <param name="currencyCode"></param>
        /// <param name="productPriceTypeId"></param>
        /// <returns></returns>
        public static ErrorOr<ProductPrice> Create(
            Guid productId,
            decimal salePrice,
            string? currencyCode,
            Guid productPriceTypeId)
        {
            var errors = new List<Error>();

            if (salePrice < 0)
            {
                errors.Add(ProductPriceErrors.NegativePrice);
            }

            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                errors.Add(ProductPriceErrors.EmptyCurrencyCode);
            }

            if (productId == Guid.Empty)
            {
                errors.Add(ProductPriceErrors.DefaultProductId);
            }

            if (productPriceTypeId == Guid.Empty)
            {
                errors.Add(ProductPriceErrors.DefaultProductPriceTypeId);
            }

            if (errors.Count != 0)
            {
                return errors;
            }

            ProductPrice productPrice = new()
            {
                ProductId = productId,
                SalePrice = salePrice,
                CurrencyCode = currencyCode!,
                ProductPriceTypeId = productPriceTypeId,
            };
            return productPrice;
        }
    }
}
