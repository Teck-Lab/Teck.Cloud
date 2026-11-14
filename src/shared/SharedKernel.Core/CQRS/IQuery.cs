using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Core.CQRS;

/// <summary>
/// Query interface with a response. Marker interface — WolverineFx discovers handlers by convention.
/// </summary>
/// <typeparam name="T">The type of the response returned by the query's handler.</typeparam>
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "The response type is a compile-time CQRS contract consumed by handler constraints.")]
public interface IQuery<out T>
    where T : notnull;
