import { describe, expect, test } from "bun:test";
import { createHmac } from "node:crypto";
import type { GenericEndpointContext } from "better-auth";
import { APIError } from "better-auth/api";
import { symmetricEncrypt } from "better-auth/crypto";
import type { Pool } from "pg";

import {
  type AuthAccess,
  type AuthApi,
  AuthUnavailableError,
  createAuthAccess,
  type GrantTokenService,
} from "./auth";
import {
  assertSameOrigin,
  CrossOriginActionError,
  readJwtClaims,
  safeReturnTo,
} from "./auth-claims";
import {
  assertVerifiedKeycloakProvider,
  buildAuthOptions,
  type VerifiedProviderSource,
} from "./auth-config";
import { AuthConfigError, type AuthEnv, readAuthEnv } from "./auth-env";
import {
  bindCapturedGrant,
  buildRefreshedGrant,
  captureVerifiedGrant,
  createSessionGrantStore,
  type GrantAdapter,
  grantAccessTokenIsFresh,
  grantMatchesSession,
  grantRowId,
  grantRowIdentifier,
  parseSessionGrant,
  SESSION_GRANT_VERSION,
  type SessionGrant,
  type SessionGrantStore,
  serializeSessionGrant,
} from "./auth-grant";
import { buildSignInScopes, performSignIn, SignInInputError } from "./auth-sign-in";

const SECRET = "admin-auth-test-secret-at-least-32-characters";
const ISSUER = "https://identity.example.test/realms/teck";
const CLIENT_ID = "admin-web";
const SUBJECT = "subject-1";
const SESSION_ID = "session-1";
const USER_ID = "user-1";
const ORG = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
const NOW = 1_800_000_000_000;
const DAY = 24 * 60 * 60 * 1_000;

const ENV: AuthEnv = {
  app: "admin",
  baseURL: "https://admin.example.test",
  secret: SECRET,
  databaseURL: "postgres://auth:auth@db.example.test/admin_auth",
  issuer: ISSUER,
  clientId: CLIENT_ID,
  clientSecret: "client-secret",
  gatewayURL: "https://gateway.example.test",
};

const SECRET_CONFIG = {
  keys: new Map([[1, SECRET]]),
  currentVersion: 1,
  legacySecret: SECRET,
};

function jwt(claims: Record<string, unknown>): string {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${encode({ alg: "none" })}.${encode(claims)}.signature`;
}

function plainAccess(overrides: Record<string, unknown> = {}): string {
  return jwt({
    iss: ISSUER,
    sub: SUBJECT,
    aud: CLIENT_ID,
    exp: Math.floor(NOW / 1_000) + 600,
    iat: Math.floor(NOW / 1_000),
    organization: { alpha: { id: ORG } },
    ...overrides,
  });
}

function idToken(overrides: Record<string, unknown> = {}): string {
  return jwt({
    iss: ISSUER,
    sub: SUBJECT,
    aud: CLIENT_ID,
    exp: Math.floor(NOW / 1_000) + 600,
    iat: Math.floor(NOW / 1_000),
    nonce: "verified-request-nonce",
    ...overrides,
  });
}

/** Same shape as Better Auth's `$ba$<version>$<hex>` envelope. */
function encrypted(token: string): string {
  return `$ba$1$${Buffer.from(token).toString("hex")}`;
}

function decryptMap(entries: Record<string, string>) {
  return async (ciphertext: string): Promise<string> => {
    const plain = entries[ciphertext];
    if (!plain) throw new Error("unknown ciphertext");
    return plain;
  };
}

function captureInput(
  overrides: {
    idToken?: string | null;
    accessToken?: string | null;
    refreshToken?: string | null;
    accountId?: string | null;
    accessPlain?: string;
  } = {},
) {
  const accessPlain = overrides.accessPlain ?? plainAccess();
  const accessToken = "accessToken" in overrides ? overrides.accessToken : encrypted(accessPlain);
  return {
    account: {
      providerId: "keycloak",
      accountId: overrides.accountId === undefined ? SUBJECT : overrides.accountId,
      userId: USER_ID,
      accessToken: accessToken ?? null,
      refreshToken: overrides.refreshToken ?? null,
      idToken: overrides.idToken === undefined ? idToken() : overrides.idToken,
      accessTokenExpiresAt: new Date(NOW + 600_000),
      refreshTokenExpiresAt: null,
    },
    env: ENV,
    app: "admin" as const,
    decrypt: decryptMap(accessToken ? { [accessToken]: accessPlain } : {}),
  };
}

async function capturedGrant(overrides: { accessPlain?: string } = {}) {
  const captured = await captureVerifiedGrant(captureInput(overrides));
  if (!captured) throw new Error("expected a captured grant");
  const grant = bindCapturedGrant({
    captured,
    session: { sessionId: SESSION_ID, userId: USER_ID },
    env: ENV,
    app: "admin",
  });
  if (!grant) throw new Error("expected a bound grant");
  return grant;
}

function grantFixture(overrides: Partial<SessionGrant> = {}): SessionGrant {
  const accessPlain = plainAccess();
  return {
    version: SESSION_GRANT_VERSION,
    sessionId: SESSION_ID,
    userId: USER_ID,
    issuer: ISSUER,
    subject: SUBJECT,
    organizationId: null,
    accessToken: encrypted(accessPlain),
    refreshToken: encrypted("refresh-token-1"),
    accessTokenExpiresAt: new Date(NOW + 600_000).toISOString(),
    refreshTokenExpiresAt: null,
    ...overrides,
  };
}

function sessionResult(
  overrides: { sessionId?: string; userId?: string; expiresAt?: unknown } = {},
) {
  const sessionId = overrides.sessionId ?? SESSION_ID;
  const userId = overrides.userId ?? USER_ID;
  return {
    session: {
      id: sessionId,
      userId,
      expiresAt: overrides.expiresAt ?? new Date(NOW + 7 * DAY),
    },
    user: { id: userId },
  };
}

interface MemoryRow {
  readonly [field: string]: unknown;
}

type MemoryAdapter = GrantAdapter & { readonly rows: Map<string, MemoryRow> };

function matches(row: MemoryRow, where: { field: string; value: unknown; operator?: string }[]) {
  return where.every((condition) => {
    const value = row[condition.field];
    if (condition.operator === "gt") {
      return (
        value instanceof Date &&
        condition.value instanceof Date &&
        value.getTime() > condition.value.getTime()
      );
    }
    return value === condition.value;
  });
}

function memoryAdapter(): MemoryAdapter {
  const rows = new Map<string, MemoryRow>();
  return {
    rows,
    async create({ data }) {
      const id = data.id as string;
      if (rows.has(id)) throw new Error("duplicate verification id");
      rows.set(id, { ...data });
      return { ...data };
    },
    async findOne({ where }) {
      const id = where.find((condition) => condition.field === "id")?.value;
      const row = rows.get(String(id));
      if (!row || !matches(row, where)) return null;
      return { ...row };
    },
    async update({ where, update }) {
      for (const [id, row] of rows) {
        if (!matches(row, where)) continue;
        const next = { ...row, ...update };
        rows.set(id, next);
        return { ...next };
      }
      return null;
    },
  };
}

function fakeContext(adapter: MemoryAdapter): GenericEndpointContext {
  return {
    context: {
      adapter,
      options: { account: { encryptOAuthTokens: true } },
      secretConfig: SECRET_CONFIG,
    },
  } as unknown as GenericEndpointContext;
}

async function encryptedAccess(plain: string): Promise<string> {
  return await symmetricEncrypt({ key: SECRET_CONFIG, data: plain });
}

function storeFor(adapter: MemoryAdapter, now = () => NOW): SessionGrantStore {
  return createSessionGrantStore(adapter, ENV, "admin", now);
}

async function storeHarness(grant: SessionGrant = grantFixture()): Promise<{
  adapter: MemoryAdapter;
  store: SessionGrantStore;
}> {
  const adapter = memoryAdapter();
  const store = storeFor(adapter);
  await store.create({ grant, sessionExpiresAt: new Date(NOW + 7 * DAY) });
  return { adapter, store };
}

function tokensFor(
  entries: Record<string, string>,
  refreshed:
    | { accessToken?: unknown; refreshToken?: unknown; accessTokenExpiresAt?: unknown }
    | Error
    | null,
): GrantTokenService & { refreshCalls: string[] } {
  const refreshCalls: string[] = [];
  return {
    refreshCalls,
    async encrypt(token) {
      return encrypted(token);
    },
    async decrypt(ciphertext) {
      const plain = entries[ciphertext];
      if (!plain) throw new Error("unknown ciphertext");
      return plain;
    },
    async refresh(refreshToken) {
      refreshCalls.push(refreshToken);
      if (refreshed instanceof Error) throw refreshed;
      return refreshed;
    },
  };
}

function accessFor(
  store: SessionGrantStore,
  tokens: GrantTokenService,
  api: AuthApi = {
    async getSession() {
      return sessionResult();
    },
  },
): AuthAccess {
  return createAuthAccess({
    api,
    env: ENV,
    app: "admin",
    tokens,
    grants: store,
    now: () => NOW,
  });
}

describe("admin verified callback grant", () => {
  test("captures only a verified ID token and access token for the same subject", async () => {
    const grant = await capturedGrant();
    expect(grant.issuer).toBe(ISSUER);
    expect(grant.subject).toBe(SUBJECT);
    expect(grant.userId).toBe(USER_ID);
    expect(grant.sessionId).toBe(SESSION_ID);
    expect(grant.organizationId).toBeNull();
    expect(grant.accessToken.startsWith("$ba$1$")).toBe(true);
    expect(serializeSessionGrant(grant)).not.toContain("verified-request-nonce");

    for (const idTokenValue of [
      null,
      "",
      "not-a-jwt",
      idToken({ iss: "https://other.example.test/realms/teck" }),
      idToken({ aud: "other-client" }),
      idToken({ sub: "other-subject" }),
    ]) {
      await expect(
        captureVerifiedGrant(captureInput({ idToken: idTokenValue })),
      ).resolves.toBeNull();
    }
    await expect(captureVerifiedGrant(captureInput({ accountId: "other" }))).resolves.toBeNull();
    await expect(
      captureVerifiedGrant(captureInput({ accessToken: plainAccess() })),
    ).resolves.toBeNull();
    await expect(
      captureVerifiedGrant(captureInput({ accessPlain: plainAccess({ sub: "other" }) })),
    ).resolves.toBeNull();
    await expect(
      captureVerifiedGrant(captureInput({ refreshToken: "plaintext" })),
    ).resolves.toBeNull();
  });

  test("Admin ignores an organization claim but keeps the strict grant shape", async () => {
    const grant = await capturedGrant({
      accessPlain: plainAccess({ organization: { alpha: { id: ORG } } }),
    });
    expect(grant.organizationId).toBeNull();
    expect(
      bindCapturedGrant({
        captured: { ...grant, organizationId: ORG },
        session: { sessionId: SESSION_ID, userId: USER_ID },
        env: ENV,
        app: "admin",
      }),
    ).toBeNull();
    expect(parseSessionGrant(serializeSessionGrant(grant))).toEqual(grant);
    expect(
      bindCapturedGrant({
        captured: grant,
        session: { sessionId: SESSION_ID, userId: "user-2" },
        env: ENV,
        app: "admin",
      }),
    ).toBeNull();
  });

  test("callback and session hooks keep separate grants per request", async () => {
    const options = buildAuthOptions(ENV, "admin");
    const createAccount = options.databaseHooks?.account?.create?.before;
    const updateAccount = options.databaseHooks?.account?.update?.before;
    const createSession = options.databaseHooks?.session?.create?.after;
    if (!createAccount || !updateAccount || !createSession) {
      throw new Error("expected account and session hooks");
    }

    const firstAdapter = memoryAdapter();
    const secondAdapter = memoryAdapter();
    const firstContext = fakeContext(firstAdapter);
    const secondContext = fakeContext(secondAdapter);
    const firstAccess = await encryptedAccess(plainAccess());
    const secondAccess = await encryptedAccess(plainAccess());
    const refresh = await encryptedAccess("refresh-token-1");

    const firstAccount = await createAccount(
      {
        providerId: "keycloak",
        accountId: SUBJECT,
        userId: USER_ID,
        accessToken: firstAccess,
        refreshToken: refresh,
        idToken: idToken(),
        accessTokenExpiresAt: new Date(NOW + 600_000),
        refreshTokenExpiresAt: null,
      } as never,
      firstContext,
    );
    const secondAccount = await updateAccount(
      {
        providerId: "keycloak",
        accessToken: secondAccess,
        refreshToken: refresh,
        idToken: idToken(),
        accessTokenExpiresAt: new Date(NOW + 600_000),
        refreshTokenExpiresAt: null,
      } as never,
      secondContext,
    );
    expect(firstAccount).toMatchObject({ data: { idToken: null } });
    expect(secondAccount).toMatchObject({ data: { idToken: null } });

    await createSession(
      { id: SESSION_ID, userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
      firstContext,
    );
    await createSession(
      { id: "session-2", userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
      secondContext,
    );

    const firstRow = firstAdapter.rows.get(
      grantRowId({
        secret: SECRET,
        app: "admin",
        issuer: ISSUER,
        sessionId: SESSION_ID,
        userId: USER_ID,
      }),
    );
    const secondRow = secondAdapter.rows.get(
      grantRowId({
        secret: SECRET,
        app: "admin",
        issuer: ISSUER,
        sessionId: "session-2",
        userId: USER_ID,
      }),
    );
    expect(parseSessionGrant(firstRow?.value)?.accessToken).toBe(firstAccess);
    expect(parseSessionGrant(secondRow?.value)?.accessToken).toBe(secondAccess);
    expect(firstRow?.identifier).toBe(grantRowIdentifier("admin"));
    expect(firstAdapter.rows.size).toBe(1);
    expect(secondAdapter.rows.size).toBe(1);

    await expect(
      createSession(
        { id: "session-3", userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
        fakeContext(memoryAdapter()),
      ),
    ).rejects.toBeInstanceOf(AuthConfigError);
    const pool = options.database as Pool;
    await pool.end();
  });

  test("a callback without a verified grant is refused before session creation", async () => {
    const options = buildAuthOptions(ENV, "admin");
    const createAccount = options.databaseHooks?.account?.create?.before;
    const createSession = options.databaseHooks?.session?.create?.after;
    if (!createAccount || !createSession) throw new Error("expected hooks");

    const context = fakeContext(memoryAdapter());
    await expect(
      createAccount(
        {
          providerId: "keycloak",
          accountId: SUBJECT,
          userId: USER_ID,
          accessToken: encrypted(plainAccess()),
          idToken: null,
        } as never,
        context,
      ),
    ).rejects.toBeInstanceOf(AuthConfigError);
    await expect(
      createSession(
        { id: SESSION_ID, userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
        context,
      ),
    ).rejects.toBeInstanceOf(AuthConfigError);
    await expect(
      createSession(
        { id: SESSION_ID, userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
        null,
      ),
    ).rejects.toBeInstanceOf(AuthConfigError);
    const pool = options.database as Pool;
    await pool.end();
  });

  test("verification rows are namespaced, opaque and bounded by the session", async () => {
    const adapter = memoryAdapter();
    const store = storeFor(adapter);
    const grant = grantFixture({ refreshTokenExpiresAt: new Date(NOW + 2 * DAY).toISOString() });
    await expect(store.create({ grant, sessionExpiresAt: new Date(NOW + 7 * DAY) })).resolves.toBe(
      true,
    );

    const id = grantRowId({
      secret: SECRET,
      app: "admin",
      issuer: ISSUER,
      sessionId: SESSION_ID,
      userId: USER_ID,
    });
    expect(id.startsWith("teck.keycloak-grant.v1:admin:")).toBe(true);
    expect(id).not.toContain(SESSION_ID);
    expect(id).not.toContain(USER_ID);
    const row = adapter.rows.get(id);
    if (!row || !(row.expiresAt instanceof Date)) throw new Error("expected a dated grant row");
    expect(row.identifier).toBe(grantRowIdentifier("admin"));
    expect(row.expiresAt.toISOString()).toBe(new Date(NOW + 2 * DAY).toISOString());
    expect(serializeSessionGrant(grant)).not.toContain("id_token");

    const read = await store.read({ sessionId: SESSION_ID, userId: USER_ID });
    expect(read?.grant).toEqual(grant);
    await expect(store.read({ sessionId: SESSION_ID, userId: "user-2" })).resolves.toBeNull();
    await expect(store.create({ grant, sessionExpiresAt: new Date(NOW - 1) })).resolves.toBe(false);
  });

  test("expired, stale and wrongly named rows are rejected", async () => {
    const adapter = memoryAdapter();
    const store = storeFor(adapter);
    const grant = grantFixture();
    await store.create({ grant, sessionExpiresAt: new Date(NOW + DAY) });
    const id = grantRowId({
      secret: SECRET,
      app: "admin",
      issuer: ISSUER,
      sessionId: SESSION_ID,
      userId: USER_ID,
    });
    const row = adapter.rows.get(id);
    if (!row) throw new Error("expected row");

    adapter.rows.set(id, { ...row, expiresAt: new Date(NOW - 1) });
    await expect(store.read({ sessionId: SESSION_ID, userId: USER_ID })).resolves.toBeNull();

    adapter.rows.set(id, { ...row, identifier: "other" });
    await expect(store.read({ sessionId: SESSION_ID, userId: USER_ID })).resolves.toBeNull();

    adapter.rows.set(id, {
      ...row,
      value: serializeSessionGrant({ ...grant, userId: "user-2" }),
    });
    await expect(store.read({ sessionId: SESSION_ID, userId: USER_ID })).resolves.toBeNull();
  });

  test("a session without its own grant is unauthenticated", async () => {
    for (const result of [
      null,
      sessionResult({ userId: "user-2" }),
      sessionResult({ sessionId: "session-other" }),
    ]) {
      const { store } = await storeHarness();
      const access = accessFor(store, tokensFor({}, null), {
        async getSession() {
          return result;
        },
      });
      await expect(access.getGatewayAccessToken(new Headers())).resolves.toBeNull();
      await expect(access.getAuthSession(new Headers())).resolves.toBeNull();
    }
  });

  test("never rereads the mutable provider account and narrows token claims", async () => {
    const grant = grantFixture();
    const plain = plainAccess();
    const { store } = await storeHarness(grant);
    let accountReads = 0;
    const api = {
      async getSession() {
        return sessionResult();
      },
      async listUserAccounts() {
        accountReads += 1;
        return [];
      },
      async getAccessToken() {
        accountReads += 1;
        return { accessToken: plainAccess({ sub: "other" }) };
      },
    } as unknown as AuthApi;
    const access = accessFor(store, tokensFor({ [grant.accessToken]: plain }, null), api);
    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBe(plain);
    expect(accountReads).toBe(0);

    for (const claims of [
      plainAccess({ iss: "https://other.example.test/realms/teck" }),
      plainAccess({ sub: "other-subject" }),
    ]) {
      const other = grantFixture();
      const { store: otherStore } = await storeHarness(other);
      await expect(
        accessFor(
          otherStore,
          tokensFor({ [other.accessToken]: claims }, null),
        ).getGatewayAccessToken(new Headers()),
      ).resolves.toBeNull();
    }
  });

  test("refreshes only this session's grant under compare-and-swap", async () => {
    const oldPlain = plainAccess({ exp: Math.floor(NOW / 1_000) - 10 });
    const newPlain = plainAccess({ exp: Math.floor(NOW / 1_000) + 900 });
    const grant = grantFixture({
      accessToken: encrypted(oldPlain),
      accessTokenExpiresAt: new Date(NOW - 10_000).toISOString(),
    });
    const { adapter, store } = await storeHarness(grant);
    const tokens = tokensFor(
      {
        [grant.accessToken]: oldPlain,
        [grant.refreshToken as string]: "refresh-token-1",
        [encrypted(newPlain)]: newPlain,
      },
      {
        accessToken: newPlain,
        refreshToken: "refresh-token-2",
        accessTokenExpiresAt: new Date(NOW + 900_000),
      },
    );
    await expect(accessFor(store, tokens).getGatewayAccessToken(new Headers())).resolves.toBe(
      newPlain,
    );
    expect(tokens.refreshCalls).toEqual(["refresh-token-1"]);
    const row = adapter.rows.get(
      grantRowId({
        secret: SECRET,
        app: "admin",
        issuer: ISSUER,
        sessionId: SESSION_ID,
        userId: USER_ID,
      }),
    );
    const rotated = parseSessionGrant(row?.value);
    expect(rotated?.accessToken).toBe(encrypted(newPlain));
    expect(rotated?.refreshToken).toBe(encrypted("refresh-token-2"));
    expect(rotated?.organizationId).toBeNull();
  });

  test("a lost rotation race re-reads the winner instead of overwriting it", async () => {
    const oldPlain = plainAccess({ exp: Math.floor(NOW / 1_000) - 10 });
    const winnerPlain = plainAccess({ exp: Math.floor(NOW / 1_000) + 900 });
    const stale = grantFixture({
      accessToken: encrypted(oldPlain),
      accessTokenExpiresAt: new Date(NOW - 10_000).toISOString(),
    });
    const winner = grantFixture({
      accessToken: encrypted(winnerPlain),
      accessTokenExpiresAt: new Date(NOW + 900_000).toISOString(),
    });
    let reads = 0;
    const grants: SessionGrantStore = {
      async create() {
        return true;
      },
      async read() {
        reads += 1;
        return {
          id: "row",
          value: serializeSessionGrant(reads === 1 ? stale : winner),
          expiresAt: new Date(NOW + DAY).toISOString(),
          grant: reads === 1 ? stale : winner,
        };
      },
      async rotate() {
        return false;
      },
    };
    const tokens = tokensFor(
      {
        [stale.accessToken]: oldPlain,
        [stale.refreshToken as string]: "refresh-token-1",
        [winner.accessToken]: winnerPlain,
      },
      { accessToken: plainAccess() },
    );
    await expect(
      accessFor(grants, tokens, {
        async getSession() {
          return sessionResult();
        },
      }).getGatewayAccessToken(new Headers()),
    ).resolves.toBe(winnerPlain);
    expect(reads).toBe(2);
  });

  test("API errors require sign-in; store failures are unavailable", async () => {
    const oldPlain = plainAccess({ exp: Math.floor(NOW / 1_000) - 10 });
    const grant = grantFixture({
      accessToken: encrypted(oldPlain),
      accessTokenExpiresAt: new Date(NOW - 10_000).toISOString(),
    });
    const { store } = await storeHarness(grant);
    const apiError = tokensFor(
      { [grant.accessToken]: oldPlain, [grant.refreshToken as string]: "refresh-token-1" },
      APIError.from("BAD_REQUEST", {
        code: "FAILED_TO_REFRESH_ACCESS_TOKEN",
        message: "Refresh denied",
      }),
    );
    await expect(
      accessFor(store, apiError).getGatewayAccessToken(new Headers()),
    ).resolves.toBeNull();

    const transport = tokensFor(
      { [grant.accessToken]: oldPlain, [grant.refreshToken as string]: "refresh-token-1" },
      new Error("issuer unreachable"),
    );
    await expect(
      accessFor(store, transport).getGatewayAccessToken(new Headers()),
    ).rejects.toBeInstanceOf(AuthUnavailableError);

    const unavailable = accessFor(
      {
        async create() {
          return true;
        },
        async read() {
          throw new Error("pool failed");
        },
        async rotate() {
          return false;
        },
      },
      tokensFor({}, null),
      {
        async getSession() {
          return sessionResult();
        },
      },
    );
    await expect(unavailable.getGatewayAccessToken(new Headers())).rejects.toBeInstanceOf(
      AuthUnavailableError,
    );
  });

  test("session scope stays opaque and tenant-free", async () => {
    const { store } = await storeHarness();
    const access = accessFor(store, tokensFor({}, null));
    const scope = await access.appSessionScope(new Headers());
    const expected = createHmac("sha256", SECRET)
      .update(
        JSON.stringify(["pagination-session-scope:v1", "admin", ISSUER, USER_ID, SESSION_ID, ""]),
      )
      .digest("base64url");
    expect(scope).toBe(expected);
    expect(scope as string).not.toContain(USER_ID);
  });

  test("rotation is scoped to the exact row and previous serialized value", async () => {
    const previous = grantFixture();
    const next = grantFixture({ accessToken: encrypted(plainAccess()) });
    const base = memoryAdapter();
    const calls: unknown[] = [];
    const adapter: MemoryAdapter = {
      rows: base.rows,
      create: base.create,
      findOne: base.findOne,
      async update(input) {
        calls.push(input);
        return null;
      },
    };
    const store = createSessionGrantStore(adapter, ENV, "admin", () => NOW);
    await expect(
      store.rotate({ previous, next, sessionExpiresAt: new Date(NOW + DAY) }),
    ).resolves.toBe(false);
    expect(calls).toHaveLength(1);
    const call = calls[0] as {
      model: string;
      where: { field: string; value: unknown; operator?: string }[];
      update: Record<string, unknown>;
    };
    expect(call.model).toBe("verification");
    expect(call.where).toContainEqual({
      field: "id",
      value: grantRowId({
        secret: SECRET,
        app: "admin",
        issuer: ISSUER,
        sessionId: SESSION_ID,
        userId: USER_ID,
      }),
    });
    expect(call.where).toContainEqual({
      field: "value",
      value: serializeSessionGrant(previous),
    });
    expect(call.where).toContainEqual({
      field: "expiresAt",
      value: new Date(NOW),
      operator: "gt",
    });
    expect(call.update.value).toBe(serializeSessionGrant(next));
  });

  test("refreshed tokens are revalidated and re-encrypted without an ID token", async () => {
    const previous = grantFixture();
    const newPlain = plainAccess();
    const next = await buildRefreshedGrant({
      previous,
      tokens: {
        accessToken: newPlain,
        refreshToken: "refresh-token-2",
        accessTokenExpiresAt: new Date(NOW + 900_000),
      },
      env: ENV,
      app: "admin",
      encrypt: async (token) => encrypted(token),
    });
    expect(next?.accessToken).toBe(encrypted(newPlain));
    expect(next?.refreshToken).toBe(encrypted("refresh-token-2"));
    expect(next?.organizationId).toBeNull();
    await expect(
      buildRefreshedGrant({
        previous,
        tokens: { accessToken: plainAccess({ iss: "https://other.test" }) },
        env: ENV,
        app: "admin",
        encrypt: async (token) => encrypted(token),
      }),
    ).resolves.toBeNull();
  });

  test("grant binding and freshness reject a different session, user or issuer", () => {
    const grant = grantFixture();
    expect(
      grantMatchesSession({
        grant,
        session: { sessionId: SESSION_ID, userId: USER_ID },
        env: ENV,
        app: "admin",
      }),
    ).toBe(true);
    expect(
      grantMatchesSession({
        grant,
        session: { sessionId: "session-2", userId: USER_ID },
        env: ENV,
        app: "admin",
      }),
    ).toBe(false);
    expect(
      grantMatchesSession({
        grant,
        session: { sessionId: SESSION_ID, userId: "user-2" },
        env: ENV,
        app: "admin",
      }),
    ).toBe(false);
    expect(
      grantMatchesSession({
        grant,
        session: { sessionId: SESSION_ID, userId: USER_ID },
        env: { ...ENV, issuer: "https://other.test" },
        app: "admin",
      }),
    ).toBe(false);
    expect(grantAccessTokenIsFresh({ grant, claims: readJwtClaims(plainAccess()), now: NOW })).toBe(
      true,
    );
    expect(
      grantAccessTokenIsFresh({
        grant: grantFixture({ accessTokenExpiresAt: new Date(NOW - 1).toISOString() }),
        claims: null,
        now: NOW,
      }),
    ).toBe(false);
  });
});

describe("admin provider verification gate", () => {
  function source(
    provider: Partial<
      Awaited<VerifiedProviderSource["$context"]>["socialProviders"][number]
    > | null,
  ) {
    return {
      $context: Promise.resolve({
        socialProviders: provider ? [{ id: "keycloak", ...provider }] : [],
      }),
    };
  }
  const verified = {
    issuer: ISSUER,
    requiresIdTokenNonce: true,
    idToken: { jwks: { key: "set" }, issuer: ISSUER, audience: CLIENT_ID },
  };

  test("requires configured issuer, discovery JWKS and nonce binding", async () => {
    await expect(assertVerifiedKeycloakProvider(source(verified), ENV)).resolves.toBeUndefined();
    for (const provider of [
      null,
      { ...verified, issuer: "https://other.example.test/realms/teck" },
      { ...verified, requiresIdTokenNonce: false },
      { ...verified, idToken: undefined },
      { ...verified, idToken: { verify: async () => true } },
      { ...verified, idToken: { ...verified.idToken, jwks: undefined } },
      { ...verified, idToken: { ...verified.idToken, allowOpaqueToken: true } },
      { ...verified, idToken: { ...verified.idToken, audience: "other-client" } },
    ]) {
      await expect(assertVerifiedKeycloakProvider(source(provider), ENV)).rejects.toBeInstanceOf(
        AuthConfigError,
      );
    }
  });
});

describe("admin sign-in and narrowing", () => {
  const base = {
    app: "admin" as const,
    baseURL: ENV.baseURL,
    headers: new Headers({ origin: ENV.baseURL }),
    returnTo: "/tenants",
  };

  test("admin requests base scopes and ignores any organization input", () => {
    expect(buildSignInScopes("admin", "ignored")).toEqual(["openid", "profile", "email"]);
    expect(() => buildSignInScopes("storefront", "")).toThrow(SignInInputError);
  });

  test("starts Keycloak without a tenant parameter", async () => {
    const bodies: unknown[] = [];
    await performSignIn({
      ...base,
      organization: "ignored",
      api: {
        async signInSocial(input) {
          bodies.push(input.body);
          return { url: "https://identity.example.test/authorize" };
        },
      },
    });
    expect(bodies).toEqual([
      {
        provider: "keycloak",
        callbackURL: "/tenants",
        errorCallbackURL: "/sign-in?error=sign-in",
        scopes: ["openid", "profile", "email"],
      },
    ]);
  });

  test("foreign origins send no provider request", async () => {
    let called = false;
    await expect(
      performSignIn({
        ...base,
        headers: new Headers({ origin: "https://evil.example" }),
        organization: null,
        api: {
          async signInSocial() {
            called = true;
            return { url: "https://identity.example.test/authorize" };
          },
        },
      }),
    ).rejects.toBeInstanceOf(CrossOriginActionError);
    expect(called).toBe(false);
  });

  test("return URLs stay local and JWT decoding rejects malformed payloads", () => {
    expect(safeReturnTo("/tenants?cursor=abc", ENV.baseURL)).toBe("/tenants?cursor=abc");
    expect(safeReturnTo("//evil.example", ENV.baseURL)).toBe("/");
    expect(readJwtClaims(plainAccess())?.sub).toBe(SUBJECT);
    expect(readJwtClaims("a.not-json.c")).toBeNull();
    expect(() => assertSameOrigin(new Headers(), ENV.baseURL)).toThrow(CrossOriginActionError);
  });
});

describe("admin configuration", () => {
  test("missing env fails closed", () => {
    expect(() => readAuthEnv({}, "admin")).toThrow(AuthConfigError);
  });

  test("separate cookie and auth boundaries are explicit", async () => {
    const options = buildAuthOptions(ENV, "admin");
    expect(options.advanced?.cookiePrefix).toBe("teck-admin");
    expect(options.account?.accountLinking).toEqual({
      enabled: false,
      disableImplicitLinking: true,
    });
    expect(options.session?.cookieCache).toEqual({ enabled: false });
    expect(options.session?.additionalFields).toBeUndefined();
    expect(options.emailAndPassword?.enabled).toBe(false);
    expect(options.account?.storeAccountCookie).toBe(false);
    expect(options.account?.encryptOAuthTokens).toBe(true);
    expect(options.databaseHooks?.account?.create?.before).toBeDefined();
    expect(options.databaseHooks?.account?.update?.before).toBeDefined();
    expect(options.databaseHooks?.session?.create?.after).toBeDefined();
    expect(options.advanced?.defaultCookieAttributes).toMatchObject({
      httpOnly: true,
      sameSite: "lax",
      secure: true,
    });

    const plugins = options.plugins as unknown as { id?: string }[];
    expect(plugins.map((plugin) => plugin.id)).toEqual(["generic-oauth", "next-cookies"]);

    const pool = options.database as Pool;
    await pool.end();
  });
});
