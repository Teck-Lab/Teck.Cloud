import type { ThemeConfig } from "./vendor/keycloak-theme/lib/themeConfig";

// Deliberately static: no public realm-attribute SPI, extra network request or cache.
const branding: ThemeConfig = {};
export function useThemeConfig(_realmName: string): ThemeConfig {
  return branding;
}
