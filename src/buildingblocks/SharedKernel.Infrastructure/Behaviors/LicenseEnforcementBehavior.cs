// <copyright file="LicenseEnforcementBehavior.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using ErrorOr;
using Mediator;
using SharedKernel.Core.Licensing;

namespace SharedKernel.Infrastructure.Behaviors;

/// <summary>
/// A pipeline behavior that enforces license validation for requests
/// that implement <see cref="ILicenseGatedRequest"/>.
/// </summary>
public sealed class LicenseEnforcementBehavior<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TResponse>(
    ILicenseValidator licenseValidator)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    private static readonly Func<List<Error>, TResponse> ErrorResponseFactory = CreateErrorResponseFactory();

    /// <summary>
    /// Validates the license for license-gated requests before proceeding.
    /// </summary>
    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message is not ILicenseGatedRequest gatedRequest)
        {
            return await next(message, cancellationToken);
        }

        LicenseValidationResult validation = await licenseValidator.ValidateAsync(
            gatedRequest.TenantId,
            gatedRequest.LocationId,
            cancellationToken).ConfigureAwait(false);

        if (!validation.IsValid)
        {
            return ErrorResponseFactory([Error.Forbidden("License.Enforcement", validation.ErrorMessage ?? "License validation failed.")]);
        }

        return await next(message, cancellationToken);
    }

    private static Func<List<Error>, TResponse> CreateErrorResponseFactory()
    {
        Type responseType = typeof(TResponse);
        MethodInfo? fromMethod = responseType.GetMethod(
            nameof(ErrorOr<object>.From),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(List<Error>)],
            modifiers: null);

        if (fromMethod is null)
        {
            throw new InvalidOperationException(
                $"{nameof(LicenseEnforcementBehavior<TRequest, TResponse>)} requires {responseType.FullName} to be an ErrorOr<T> response type.");
        }

        ParameterExpression errorsParameter = Expression.Parameter(typeof(List<Error>), "errors");
        MethodCallExpression fromCall = Expression.Call(fromMethod, errorsParameter);
        UnaryExpression castResponse = Expression.Convert(fromCall, typeof(TResponse));

        return Expression.Lambda<Func<List<Error>, TResponse>>(castResponse, errorsParameter).Compile();
    }
}
