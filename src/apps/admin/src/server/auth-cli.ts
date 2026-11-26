/**
 * Eager configuration used only by the Better Auth 1.7.28 migration CLI
 * (`tools/run-auth-migration.mjs`). Database migrations need a constructed
 * instance; the Next runtime imports the lazy `getAuth` from `./auth` instead,
 * so `next build` never reads env or opens the auth store.
 */
import { betterAuth } from "better-auth";

import { buildAuthOptions } from "./auth-config";
import { readAuthEnv } from "./auth-env";

export const auth = betterAuth(buildAuthOptions(readAuthEnv(process.env, "admin"), "admin"));
