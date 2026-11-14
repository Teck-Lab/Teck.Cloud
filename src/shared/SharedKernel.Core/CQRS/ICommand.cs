using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Core.CQRS;

/// <summary>
/// Command with no response. Marker interface — WolverineFx discovers handlers by convention.
/// </summary>
public interface ICommand : ICommand<Unit>;

/// <summary>
/// Command with response. Marker interface — WolverineFx discovers handlers by convention.
/// </summary>
/// <typeparam name="TResponse">The type of the response returned by the command's handler.</typeparam>
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "The response type is a compile-time CQRS contract consumed by handler constraints.")]
public interface ICommand<out TResponse>;
