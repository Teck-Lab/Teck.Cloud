using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Features.Products.GetProductById.V1
{
    /// <summary>
    /// The get product by id validator.
    /// </summary>
    public sealed class GetProductByIdValidator : Validator<GetProductByIdRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetProductByIdValidator"/> class.
        /// </summary>
        public GetProductByIdValidator()
        {
            RuleFor(product => product.ProductId)
                .NotEmpty();
        }
    }
}
