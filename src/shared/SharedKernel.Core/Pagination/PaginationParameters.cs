namespace SharedKernel.Core.Pagination;

/// <summary>
/// The pagination parameters.
/// </summary>
public abstract class PaginationParameters
{
    /// <summary>
    /// Gets or sets the page.
    /// </summary>
    public virtual int Page
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 1;

    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    public int Size
    {
        get => DefaultPageSize;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            DefaultPageSize = Math.Min(value, MaxPageSize);
        }
    }

    /// <summary>
    /// Gets the max page size.
    /// </summary>
    internal virtual int MaxPageSize { get; } = 100;

    /// <summary>
    /// Gets or sets the default page size.
    /// </summary>
    internal virtual int DefaultPageSize { get; set; } = 10;
}
