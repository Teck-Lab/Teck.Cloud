namespace SharedKernel.Core.Domain;

/// <summary>
/// Represents a base class for value objects, which are immutable and compared based on their properties.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Determines whether the specified object is equal to the current value object.
    /// </summary>
    /// <param name="obj">The object to compare with the current value object.</param>
    /// <returns><see langword="true"/> if the specified object is equal to the current value object; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Serves as the default hash function for the value object.
    /// </summary>
    /// <returns>A hash code for the current value object.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var obj in GetEqualityComponents())
            {
                hash = (hash * 23) + (obj?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    /// <summary>
    /// Provides the components that are used to determine equality for the value object.
    /// </summary>
    /// <returns>An enumerable of objects representing the equality components.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();
}
