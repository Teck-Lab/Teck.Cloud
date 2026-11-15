# Shared design tokens

tokens.json is the single source for the current light and dark semantic palette.
Run node src/packages/design-tokens/generate.mjs after changing it. The generated
tokens.css, tokens.css.d.ts, index.js and index.d.ts are committed; the Nx build target checks for
drift without changing files.

Web apps import @teck/design-tokens/css once at their entry point. The Keycloak
theme imports it after the pinned vendor stylesheet, so these variables override
the starter defaults. Existing Tailwind color names use these CSS variables.

React Native apps import colors from @teck/design-tokens and select light or dark
using their own color-scheme state. The values are HSL strings supported by React
Native. This package contains no browser or native runtime dependencies.

The package only shares palette values. It does not share React components,
authentication pages, fonts, logos, layout, or platform-specific UI. Frontend
apps will be wired to it when those apps are created.
