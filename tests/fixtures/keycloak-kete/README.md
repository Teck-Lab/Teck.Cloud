# Optional KETE evaluation

This image is deliberately separate from the application image. It enables a test
of upstream publication and its privacy side effects using disposable data only.
Build the normal `keycloak-theme:container:build` Nx target first, then:

```sh
docker build -f tests/fixtures/keycloak-kete/Dockerfile \
  -t teck/keycloak-kete:26.8.2-evaluation tests/fixtures/keycloak-kete
dotnet run --project tests/integration/SharedKernel.Infrastructure.IntegrationTests \
  -c Release -- --explicit only --filter-class '*KeycloakKeteEvaluationTests'
```

The explicitly selected test owns its Keycloak, PostgreSQL, RabbitMQ and network.
It provides generated credentials, imports the normal realm, permits only the
organization CRUD route and checks the actual six-field event with the production
mapper. It also asserts that upstream enabled admin representation storage,
which is the reason this provider is not enabled by default. Normal adapter tests
run without this image and verify real broker/inbox/directory behavior separately.

Do not enable this image for live identity data. See
[the privacy and operational gate](../../../docs/identity-events.md).
