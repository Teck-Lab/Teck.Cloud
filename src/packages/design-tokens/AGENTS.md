# Design tokens

Edit tokens.json as the palette source. Regenerate tokens.css, tokens.css.d.ts, index.js and
index.d.ts with node generate.mjs. Never edit generated outputs independently.
Run the package build, typecheck, lint and tests after changes. Keep exports
platform neutral: no DOM, React or React Native dependency.
