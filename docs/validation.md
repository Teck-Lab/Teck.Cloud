# Validation boundaries

Teck.Cloud uses FluentValidation at two runtime boundaries. Keep each validator with
its owning request or message type and test the boundary that invokes it.

## HTTP requests

Host endpoints use the standard `FastEndpoints.Validator<TRequest>` base class.
FastEndpoints discovers validators from the generated endpoint type lists passed to
`AddFoundationApi`; `UseFoundationApi` runs them before endpoint handlers and returns
RFC-compatible Problem Details with status 400 for invalid input. Keep these validators
stateless because FastEndpoints reuses validator instances.

The `ApiTests` WebApplicationFactory test posts both invalid and valid requests through
the actual HTTP pipeline. It asserts the error status and Problem Details body, then
checks that valid input reaches the endpoint and is returned successfully. This is a
fixture proof of FastEndpoints integration, not a deployed business service.

## Wolverine messages

`UseFoundationMessaging<TWrite>` enables the `WolverineFx.FluentValidation` middleware
through `UseFoundationValidation`. Wolverine discovers public `IValidator<TMessage>`
implementations from the configured application assembly and runs them before command,
local-message or consumer handlers. An invalid message throws
`FluentValidation.ValidationException`; the handler is not invoked. The focused
`WolverineValidationTests` test starts Wolverine without a database or broker and
asserts both the rejected and accepted invocation paths.

The foundation path uses Wolverine's default validator assembly scan at startup. That
scan is not trim-safe, and the shared helper does not currently expose an explicit
registration mode. Do not use this path in a trimmed or Native AOT deployment without
adding a deliberate explicit-registration path and testing that host. Teck.Cloud does
not claim full Native AOT support. Prefer public, stateless validators without scoped
constructor dependencies because generated handler code works best with that shape.

HTTP and Wolverine validation produce different error contracts: FastEndpoints returns
Problem Details to the caller, while Wolverine stops an invalid handler invocation
with a validation exception. Validate messages before mutating aggregates; a validator
is not a replacement for domain invariants or database constraints.
