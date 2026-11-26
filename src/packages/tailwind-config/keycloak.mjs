import { colorTokens } from "@teck/design-tokens";

/**
 * Build the Tailwind v3 semantic palette from the generated token names.
 * Values stay CSS variable references so `@teck/design-tokens/css` remains
 * the only source of palette values at runtime.
 */
function semanticColors(tokens) {
  const colors = {};
  for (const token of tokens) {
    if (token.endsWith("-foreground")) continue;
    if (token.startsWith("chart-")) {
      colors.chart = {
        ...(colors.chart ?? {}),
        [token.slice("chart-".length)]: `hsl(var(--${token}))`,
      };
      continue;
    }
    colors[token] = `hsl(var(--${token}))`;
  }
  for (const token of tokens) {
    if (!token.endsWith("-foreground")) continue;
    const base = token.slice(0, -"-foreground".length);
    colors[base] = {
      DEFAULT: `hsl(var(--${base}))`,
      foreground: `hsl(var(--${token}))`,
    };
  }
  return colors;
}

export default {
  darkMode: ["class"],
  theme: {
    extend: {
      borderRadius: {
        lg: "var(--radius)",
        md: "calc(var(--radius) - 2px)",
        sm: "calc(var(--radius) - 4px)",
      },
      colors: semanticColors(colorTokens),
    },
  },
};
