import { fileURLToPath, URL } from "node:url";
import react from "@vitejs/plugin-react";
import { keycloakify } from "keycloakify/vite-plugin";
import { defineConfig } from "vite";
import { keycloakThemeConfig } from "./src/theme-config";

export default defineConfig({
  publicDir: "vendor/keycloak-shadcn/public",
  plugins: [
    react(),
    keycloakify({
      themeName: keycloakThemeConfig.themeName,
      artifactId: "teck-keycloak-theme",
      accountThemeImplementation: "Multi-Page",
      keycloakVersionTargets: {
        "26.2-and-above": "teck-keycloak-theme.jar",
        "26.0-to-26.1": false,
        "25": false,
        "24": false,
        "23": false,
        "21-and-below": false,
      },
    }),
  ],
  resolve: {
    alias: [
      {
        find: "@/lib/useThemeConfig",
        replacement: fileURLToPath(new URL("./src/branding.ts", import.meta.url)),
      },
      {
        find: "@",
        replacement: fileURLToPath(new URL("./src/vendor/keycloak-theme", import.meta.url)),
      },
      {
        find: "config",
        replacement: fileURLToPath(new URL("./src/theme-config.ts", import.meta.url)),
      },
    ],
  },
});
