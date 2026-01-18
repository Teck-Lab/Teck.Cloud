using Catalog.Domain.Entities.BrandAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Catalog.Domain.Entities.BrandAggregate.ValueObjects
{
    /// <summary>
    /// Represents a website URL value object.
    /// </summary>
    public sealed class Website : ValueObject
    {
        /// <summary>
        /// Gets the website URL value.
        /// </summary>
        public string Value { get; }
        private Website(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a new <see cref="Website"/> instance if the provided value is valid.
        /// </summary>
        /// <param name="value">The website URL value.</param>
        /// <returns>An <see cref="ErrorOr{T}"/> containing the <see cref="Website"/> instance or validation errors.</returns>
        public static ErrorOr<Website> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return WebsiteErrors.Empty;

            if (!Uri.IsWellFormedUriString(value, UriKind.Absolute) ||
                !(value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return WebsiteErrors.Invalid;

            return new Website(value);
        }

        /// <summary>
        /// Gets the components used for equality comparison.
        /// </summary>
        /// <returns>An enumerable of equality components.</returns>
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
        }

        /// <summary>
        /// Returns the string representation of the website URL.
        /// </summary>
        /// <returns>The website URL as a string.</returns>
        public override string ToString() => Value;
    }
}
