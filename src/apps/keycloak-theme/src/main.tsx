import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { KcPage } from "./vendor/keycloak-theme/kc.gen";
import "./vendor/keycloak-theme/index.css";
import "@teck/design-tokens/css";

const root = document.getElementById("root");
if (!root) throw new Error("Missing theme root");
createRoot(root).render(
  <StrictMode>
    {window.kcContext ? (
      <KcPage kcContext={window.kcContext} />
    ) : (
      <h1>Open this theme through Keycloak.</h1>
    )}
  </StrictMode>,
);
