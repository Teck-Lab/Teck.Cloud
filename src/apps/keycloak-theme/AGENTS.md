# Keycloak theme

- Keep this Bun/Nx application and its authored configuration separate from the pinned third-party snapshot. See README.md and upstream.json before updating vendor files.
- Preserve upstream licensing and security pages. Record source adaptations; never weaken the repository TypeScript policy to accept upstream code.
- Use src/packages/design-tokens/tokens.json for palette changes; regenerate its CSS and JS exports. Do not edit the vendored starter palette as the active source.
- Keep branding static unless a separately reviewed provider is explicitly requested. Do not silently enable Java SPIs, experimental admin UI, authentication flows or external theme URLs.
- Build the one matching theme JAR into the digest-pinned Keycloak image. Tests, Aspire and KIND must consume the same candidate through the shared identity descriptor.
- Validate typecheck/build/test/lint, frozen Bun install/audit and the source image build. Browser smoke must use a disposable realm/server, never a live identity deployment.
