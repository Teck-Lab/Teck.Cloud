using SharedKernel.Core.Options;
using Keycloak.AuthServices.Authentication;

namespace SharedKernel.Infrastructure.Auth
{
    /// <summary>
    /// The keycloak options.
    /// </summary>
    public class KeycloakOptions : KeycloakAuthenticationOptions, IOptionsRoot
    { }
}
