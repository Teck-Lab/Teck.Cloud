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
  canonicalOrganizationId,
  parseOrganizationAlias,
  readJwtClaims,
  safeReturnTo,
  singleOrganizationId,
} from "./auth-claims";
import {
  assertVerifiedKeycloakProvider,
  buildAuthOptions,
  STOREFRONT_SESSION_FIELDS,
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

const SECRET = "storefront-auth-test-secret-at-least-32-characters";
const ISSUER = "https://identity.example.test/realms/teck";
const CLIENT_ID = "storefront-web";
const SUBJECT = "subject-1";
const SESSION_ID = "session-1";
const USER_ID = "user-1";
const ORG = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
const OTHER_ORG = "11111111-2222-4333-8444-555555555555";
const NOW = 1_800_000_000_000;
const DAY = 24 * 60 * 60 * 1_000;

const ENV: AuthEnv = {
  app: "storefront",
  baseURL: "https://storefront.example.test",
  secret: SECRET,
  databaseURL: "postgres://auth:auth@db.example.test/storefront_auth",
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
    app: "storefront" as const,
    decrypt: decryptMap(accessToken ? { [accessToken]: accessPlain } : {}),
  };
}

async function capturedGrant(overrides: { accessPlain?: string; organizationId?: string } = {}) {
  const captured = await captureVerifiedGrant(captureInput(overrides));
  if (!captured) throw new Error("expected a captured grant");
  const grant = bindCapturedGrant({
    captured,
    session: {
      sessionId: SESSION_ID,
      userId: USER_ID,
      commerceOrganizationId: overrides.organizationId ?? ORG,
    },
    env: ENV,
    app: "storefront",
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
    organizationId: ORG,
    accessToken: encrypted(accessPlain),
    refreshToken: encrypted("refresh-token-1"),
    accessTokenExpiresAt: new Date(NOW + 600_000).toISOString(),
    refreshTokenExpiresAt: null,
    ...overrides,
  };
}

function sessionResult(
  overrides: {
    sessionId?: string;
    userId?: string;
    organizationId?: unknown;
    expiresAt?: unknown;
  } = {},
) {
  const sessionId = overrides.sessionId ?? SESSION_ID;
  const userId = overrides.userId ?? USER_ID;
  return {
    session: {
      id: sessionId,
      userId,
      commerceOrganizationId:
        overrides.organizationId === undefined ? ORG : overrides.organizationId,
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

function storeFor(adapter: MemoryAdapter): SessionGrantStore {
  return createSessionGrantStore(adapter, ENV, "storefront", () => NOW);
}

async function storeHarness(
  grant: SessionGrant = grantFixture(),
): Promise<{ adapter: MemoryAdapter; store: SessionGrantStore }> {
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
    app: "storefront",
    tokens,
    grants: store,
    now: () => NOW,
  });
}

describe("storefront verified callback grant", () => {
  test("captures only a verified ID token, access token and organization", async () => {
    const grant = await capturedGrant();
    expect(grant.issuer).toBe(ISSUER);
    expect(grant.subject).toBe(SUBJECT);
    expect(grant.userId).toBe(USER_ID);
    expect(grant.sessionId).toBe(SESSION_ID);
    expect(grant.organizationId).toBe(ORG);
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
    for (const accessPlain of [
      plainAccess({ organization: undefined }),
      plainAccess({ organization: { alpha: { id: "not-a-uuid" } } }),
      plainAccess({ organization: { alpha: { id: ORG }, beta: { id: OTHER_ORG } } }),
      plainAccess({ iss: "https://other.example.test/realms/teck" }),
      plainAccess({ sub: "other-subject" }),
    ]) {
      await expect(captureVerifiedGrant(captureInput({ accessPlain }))).resolves.toBeNull();
    }
    await expect(captureVerifiedGrant(captureInput({ accountId: "other" }))).resolves.toBeNull();
    await expect(
      captureVerifiedGrant(captureInput({ accessToken: plainAccess() })),
    ).resolves.toBeNull();
    await expect(
      captureVerifiedGrant(captureInput({ refreshToken: "plaintext" })),
    ).resolves.toBeNull();
  });

  test("the session organization must equal the captured organization", async () => {
    const captured = await captureVerifiedGrant(captureInput());
    if (!captured) throw new Error("expected a captured grant");
    expect(
      bindCapturedGrant({
        captured,
        session: { sessionId: SESSION_ID, userId: USER_ID, commerceOrganizationId: ORG },
        env: ENV,
        app: "storefront",
      }),
    ).not.toBeNull();
    for (const session of [
      { sessionId: SESSION_ID, userId: USER_ID },
      { sessionId: SESSION_ID, userId: USER_ID, commerceOrganizationId: OTHER_ORG },
      { sessionId: SESSION_ID, userId: "user-2", commerceOrganizationId: ORG },
      { sessionId: SESSION_ID, userId: USER_ID, commerceOrganizationId: "not-a-uuid" },
    ]) {
      expect(bindCapturedGrant({ captured, session, env: ENV, app: "storefront" })).toBeNull();
    }
    expect(parseSessionGrant(serializeSessionGrant(await capturedGrant()))?.organizationId).toBe(
      ORG,
    );
  });

  test("callback and session hooks keep separate organizations per request", async () => {
    const options = buildAuthOptions(ENV, "storefront");
    const createAccount = options.databaseHooks?.account?.create?.before;
    const updateAccount = options.databaseHooks?.account?.update?.before;
    const beforeSession = options.databaseHooks?.session?.create?.before;
    const createSession = options.databaseHooks?.session?.create?.after;
    if (!createAccount || !updateAccount || !beforeSession || !createSession) {
      throw new Error("expected account and session hooks");
    }

    const firstAdapter = memoryAdapter();
    const secondAdapter = memoryAdapter();
    const firstContext = fakeContext(firstAdapter);
    const secondContext = fakeContext(secondAdapter);
    const firstAccess = await encryptedAccess(plainAccess());
    const secondAccess = await encryptedAccess(
      plainAccess({ organization: { alpha: { id: OTHER_ORG } } }),
    );
    const refresh = await encryptedAccess("refresh-token-1");

    await createAccount(
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
    await updateAccount(
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

    await expect(
      beforeSession(
        { id: SESSION_ID, userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
        firstContext,
      ),
    ).resolves.toMatchObject({ data: { commerceOrganizationId: ORG } });
    await createSession(
      {
        id: SESSION_ID,
        userId: USER_ID,
        expiresAt: new Date(NOW + DAY),
        commerceOrganizationId: ORG,
      } as never,
      firstContext,
    );
    await expect(
      beforeSession(
        { id: "session-2", userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
        secondContext,
      ),
    ).resolves.toMatchObject({ data: { commerceOrganizationId: OTHER_ORG } });
    await createSession(
      {
        id: "session-2",
        userId: USER_ID,
        expiresAt: new Date(NOW + DAY),
        commerceOrganizationId: OTHER_ORG,
      } as never,
      secondContext,
    );

    const firstRow = firstAdapter.rows.get(
      grantRowId({
        secret: SECRET,
        app: "storefront",
        issuer: ISSUER,
        sessionId: SESSION_ID,
        userId: USER_ID,
      }),
    );
    const secondRow = secondAdapter.rows.get(
      grantRowId({
        secret: SECRET,
        app: "storefront",
        issuer: ISSUER,
        sessionId: "session-2",
        userId: USER_ID,
      }),
    );
    expect(parseSessionGrant(firstRow?.value)?.organizationId).toBe(ORG);
    expect(parseSessionGrant(secondRow?.value)?.organizationId).toBe(OTHER_ORG);
    expect(firstRow?.identifier).toBe(grantRowIdentifier("storefront"));
    expect(firstAdapter.rows.size).toBe(1);
    expect(secondAdapter.rows.size).toBe(1);

    await expect(
      beforeSession(
        { id: "session-3", userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
        fakeContext(memoryAdapter()),
      ),
    ).resolves.toBe(false);
    await expect(
      createSession(
        { id: "session-3", userId: USER_ID, expiresAt: new Date(NOW + DAY) } as never,
        fakeContext(memoryAdapter()),
      ),
    ).rejects.toBeInstanceOf(AuthConfigError);
    const pool = options.database as Pool;
    await pool.end();
  });

  test("verification rows are namespaced, opaque and keep the organization", async () => {
    const adapter = memoryAdapter();
    const store = storeFor(adapter);
    const grant = grantFixture();
    await expect(store.create({ grant, sessionExpiresAt: new Date(NOW + 7 * DAY) })).resolves.toBe(
      true,
    );
    const id = grantRowId({
      secret: SECRET,
      app: "storefront",
      issuer: ISSUER,
      sessionId: SESSION_ID,
      userId: USER_ID,
    });
    expect(id.startsWith("teck.keycloak-grant.v1:storefront:")).toBe(true);
    expect(id).not.toContain(SESSION_ID);
    expect(adapter.rows.get(id)?.identifier).toBe(grantRowIdentifier("storefront"));
    const read = await store.read({ sessionId: SESSION_ID, userId: USER_ID });
    expect(read?.grant.organizationId).toBe(ORG);
    await expect(store.read({ sessionId: SESSION_ID, userId: "user-2" })).resolves.toBeNull();
  });

  test("a session without its own matching grant is unauthenticated", async () => {
    for (const result of [
      null,
      sessionResult({ userId: "user-2" }),
      sessionResult({ sessionId: "session-other" }),
      sessionResult({ organizationId: null }),
      sessionResult({ organizationId: OTHER_ORG }),
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

  test("never rereads the mutable provider account and narrows claims and organization", async () => {
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
        return { accessToken: plainAccess({ organization: { alpha: { id: OTHER_ORG } } }) };
      },
    } as unknown as AuthApi;
    const access = accessFor(store, tokensFor({ [grant.accessToken]: plain }, null), api);
    await expect(access.getGatewayAccessToken(new Headers())).resolves.toBe(plain);
    expect(accountReads).toBe(0);

    for (const claims of [
      plainAccess({ iss: "https://other.example.test/realms/teck" }),
      plainAccess({ sub: "other-subject" }),
      plainAccess({ organization: { alpha: { id: OTHER_ORG } } }),
      plainAccess({ organization: undefined }),
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

  test("checkout identity carries the exact HMAC scope and current bearer", async () => {
    const grant = grantFixture();
    const plain = plainAccess();
    const { store } = await storeHarness(grant);
    const identity = await accessFor(
      store,
      tokensFor({ [grant.accessToken]: plain }, null),
    ).getCommerceIdentity(new Headers());
    const expectedScope = createHmac("sha256", SECRET)
      .update(JSON.stringify(["checkout-intents:v1", ISSUER, SUBJECT, ORG]))
      .digest("base64url");
    expect(identity).toEqual({ accessToken: plain, scope: expectedScope });
  });

  test("session scope is deterministic and bound to the session organization", async () => {
    const firstGrant = grantFixture();
    const secondGrant = grantFixture({ sessionId: "session-2", organizationId: ORG });
    const firstStore = await storeHarness(firstGrant);
    const secondStore = await storeHarness(secondGrant);
    const plain = plainAccess();
    const first = await accessFor(
      firstStore.store,
      tokensFor({ [firstGrant.accessToken]: plain }, null),
    ).appSessionScope(new Headers());
    const second = await accessFor(
      secondStore.store,
      tokensFor({ [secondGrant.accessToken]: plain }, null),
      {
        async getSession() {
          return sessionResult({ sessionId: "session-2" });
        },
      },
    ).appSessionScope(new Headers());
    expect(first).toBeTruthy();
    expect(second).not.toBe(first as string);
    expect(first as string).not.toContain(USER_ID);
    const expected = createHmac("sha256", SECRET)
      .update(
        JSON.stringify([
          "pagination-session-scope:v1",
          "storefront",
          ISSUER,
          USER_ID,
          SESSION_ID,
          ORG,
        ]),
      )
      .digest("base64url");
    expect(first).toBe(expected);
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
        app: "storefront",
        issuer: ISSUER,
        sessionId: SESSION_ID,
        userId: USER_ID,
      }),
    );
    const rotated = parseSessionGrant(row?.value);
    expect(rotated?.accessToken).toBe(encrypted(newPlain));
    expect(rotated?.refreshToken).toBe(encrypted("refresh-token-2"));
    expect(rotated?.organizationId).toBe(ORG);
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

  test("refreshed tokens are revalidated, re-encrypted and keep the organization", async () => {
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
      app: "storefront",
      encrypt: async (token) => encrypted(token),
    });
    expect(next?.accessToken).toBe(encrypted(newPlain));
    expect(next?.refreshToken).toBe(encrypted("refresh-token-2"));
    expect(next?.organizationId).toBe(ORG);
    for (const accessToken of [
      plainAccess({ iss: "https://other.test" }),
      plainAccess({ organization: { alpha: { id: OTHER_ORG } } }),
    ]) {
      await expect(
        buildRefreshedGrant({
          previous,
          tokens: { accessToken },
          env: ENV,
          app: "storefront",
          encrypt: async (token) => encrypted(token),
        }),
      ).resolves.toBeNull();
    }
  });

  test("grant binding rejects another session, user, organization or issuer", () => {
    const grant = grantFixture();
    expect(
      grantMatchesSession({
        grant,
        session: { sessionId: SESSION_ID, userId: USER_ID, commerceOrganizationId: ORG },
        env: ENV,
        app: "storefront",
      }),
    ).toBe(true);
    for (const session of [
      { sessionId: "session-2", userId: USER_ID, commerceOrganizationId: ORG },
      { sessionId: SESSION_ID, userId: "user-2", commerceOrganizationId: ORG },
      { sessionId: SESSION_ID, userId: USER_ID, commerceOrganizationId: OTHER_ORG },
      { sessionId: SESSION_ID, userId: USER_ID },
    ]) {
      expect(grantMatchesSession({ grant, session, env: ENV, app: "storefront" })).toBe(false);
    }
    expect(
      grantMatchesSession({
        grant: { ...grant, organizationId: ORG.toUpperCase() },
        session: { sessionId: SESSION_ID, userId: USER_ID, commerceOrganizationId: ORG },
        env: ENV,
        app: "storefront",
      }),
    ).toBe(false);
    expect(grantAccessTokenIsFresh({ grant, claims: readJwtClaims(plainAccess()), now: NOW })).toBe(
      true,
    );
  });
});

describe("storefront provider verification gate", () => {
  interface FakeProvider {
    issuer?: string;
    requiresIdTokenNonce?: boolean;
    idToken?: unknown;
  }
  function source(provider: FakeProvider | null) {
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
      { ...verified, idToken: { jwks: undefined, issuer: ISSUER, audience: CLIENT_ID } },
      { ...verified, idToken: { ...verified.idToken, allowOpaqueToken: true } },
      { ...verified, idToken: { ...verified.idToken, audience: "other-client" } },
    ]) {
      await expect(assertVerifiedKeycloakProvider(source(provider), ENV)).rejects.toBeInstanceOf(
        AuthConfigError,
      );
    }
  });
});

describe("claim and request narrowing", () => {
  test("rich single-organization UUID claim only", () => {
    const cases: [unknown, string | null][] = [
      [{ alpha: { id: ORG } }, ORG],
      [{ alpha: { id: ORG.toUpperCase() } }, ORG],
      [undefined, null],
      [{}, null],
      [{ alpha: {} }, null],
      [{ alpha: { id: "not-a-uuid" } }, null],
      [{ alpha: { id: ORG }, beta: { id: OTHER_ORG } }, null],
      [["alpha"], null],
      ["alpha", null],
    ];
    for (const [organization, expected] of cases) {
      expect(singleOrganizationId({ organization })).toBe(expected);
    }
    expect(canonicalOrganizationId(` ${ORG.toUpperCase()} `)).toBe(ORG);
    expect(canonicalOrganizationId("alias")).toBeNull();
  });

  test("JWT payload decoding rejects malformed and oversized tokens", () => {
    expect(readJwtClaims(plainAccess())?.sub).toBe(SUBJECT);
    for (const token of ["", "a.b", "a.not-json.c", "a.b.c.d", "x".repeat(20_000)]) {
      expect(readJwtClaims(token)).toBeNull();
    }
  });

  test("organization aliases are a single bounded hint", () => {
    expect(parseOrganizationAlias("alpha.team_1-x")).toBe("alpha.team_1-x");
    for (const value of ["", " ", "two words", "a/b", "a".repeat(129), null, 3]) {
      expect(parseOrganizationAlias(value)).toBeNull();
    }
  });

  test("return URLs stay relative to this app", () => {
    expect(safeReturnTo("/orders?cursor=abc", ENV.baseURL)).toBe("/orders?cursor=abc");
    for (const value of [
      null,
      "",
      "//evil.example",
      "https://evil.example/orders",
      "/\\evil.example",
      "javascript:alert(1)",
      "/line\nbreak",
      "/.//evil.example",
    ]) {
      expect(safeReturnTo(value, ENV.baseURL)).toBe("/");
    }
  });

  test("actions require the configured origin", () => {
    const same = new Headers({ origin: ENV.baseURL });
    expect(() => assertSameOrigin(same, ENV.baseURL)).not.toThrow();
    const fetchMetadata = new Headers({ "sec-fetch-site": "same-origin" });
    expect(() => assertSameOrigin(fetchMetadata, ENV.baseURL)).not.toThrow();
    for (const headers of [
      new Headers({ origin: "https://evil.example" }),
      new Headers(),
      new Headers({ "sec-fetch-site": "cross-site" }),
    ]) {
      expect(() => assertSameOrigin(headers, ENV.baseURL)).toThrow(CrossOriginActionError);
    }
  });
});

describe("sign-in", () => {
  const baseRequest = {
    app: "storefront" as const,
    baseURL: ENV.baseURL,
    headers: new Headers({ origin: ENV.baseURL }),
    returnTo: "/orders?cursor=abc",
  };

  test("Storefront adds one validated organization scope", () => {
    expect(buildSignInScopes("storefront", "alpha.team_1-x")).toEqual([
      "openid",
      "profile",
      "email",
      "organization:alpha.team_1-x",
    ]);
    expect(buildSignInScopes("admin", "ignored")).toEqual(["openid", "profile", "email"]);
    for (const value of ["", "two words", "a/b", "a".repeat(129)]) {
      expect(() => buildSignInScopes("storefront", value)).toThrow(SignInInputError);
    }
  });

  test("starts Keycloak with same-app callback and no tenant header", async () => {
    const calls: unknown[] = [];
    const url = await performSignIn({
      ...baseRequest,
      organization: "alpha",
      api: {
        async signInSocial(input) {
          calls.push(input.body);
          return { url: "https://identity.example.test/authorize" };
        },
      },
    });
    expect(url).toBe("https://identity.example.test/authorize");
    expect(calls).toEqual([
      {
        provider: "keycloak",
        callbackURL: "/orders?cursor=abc",
        errorCallbackURL: "/sign-in?error=sign-in",
        scopes: ["openid", "profile", "email", "organization:alpha"],
      },
    ]);
  });

  test("invalid alias or foreign origin sends no provider request", async () => {
    let called = false;
    const api = {
      async signInSocial() {
        called = true;
        return { url: "https://identity.example.test/authorize" };
      },
    };
    await expect(
      performSignIn({ ...baseRequest, organization: "bad alias", api }),
    ).rejects.toBeInstanceOf(SignInInputError);
    await expect(
      performSignIn({
        ...baseRequest,
        headers: new Headers({ origin: "https://evil.example" }),
        organization: "alpha",
        api,
      }),
    ).rejects.toBeInstanceOf(CrossOriginActionError);
    expect(called).toBe(false);
  });

  test("absolute return URLs are replaced before reaching Keycloak", async () => {
    let callbackURL = "";
    await performSignIn({
      ...baseRequest,
      returnTo: "https://evil.example/orders",
      organization: "alpha",
      api: {
        async signInSocial(input) {
          callbackURL = input.body.callbackURL;
          return { url: "https://identity.example.test/authorize" };
        },
      },
    });
    expect(callbackURL).toBe("/");
  });
});

describe("configuration", () => {
  test("missing or unsafe env fails closed without echoing values", () => {
    const valid: Record<string, string> = {
      BETTER_AUTH_URL: ENV.baseURL,
      BETTER_AUTH_SECRET: SECRET,
      AUTH_DATABASE_URL: ENV.databaseURL,
      KEYCLOAK_ISSUER: ENV.issuer,
      KEYCLOAK_CLIENT_ID: ENV.clientId,
      KEYCLOAK_CLIENT_SECRET: ENV.clientSecret,
      COMMERCE_GATEWAY_URL: ENV.gatewayURL,
    };
    expect(readAuthEnv(valid, "storefront").issuer).toBe(ISSUER);
    for (const name of Object.keys(valid)) {
      const missing = { ...valid };
      delete missing[name];
      expect(() => readAuthEnv(missing, "storefront")).toThrow(AuthConfigError);
    }
    expect(() => readAuthEnv({ ...valid, BETTER_AUTH_SECRET: "short" }, "storefront")).toThrow(
      AuthConfigError,
    );
    expect(() =>
      readAuthEnv({ ...valid, AUTH_DATABASE_URL: "mysql://db/auth" }, "storefront"),
    ).toThrow(AuthConfigError);
    expect(() =>
      readAuthEnv({ ...valid, BETTER_AUTH_URL: "http://storefront.example.test" }, "storefront"),
    ).toThrow(AuthConfigError);
    expect(() =>
      readAuthEnv({ ...valid, BETTER_AUTH_URL: "http://127.0.0.1:3000" }, "storefront"),
    ).not.toThrow();
    expect(
      readAuthEnv({ ...valid, BETTER_AUTH_URL: "http://127.0.0.1:3000" }, "storefront").baseURL,
    ).toBe("http://127.0.0.1:3000");
  });

  test("Storefront keeps only its organization session field", async () => {
    const options = buildAuthOptions(ENV, "storefront");
    expect(options.emailAndPassword?.enabled).toBe(false);
    expect(options.account?.encryptOAuthTokens).toBe(true);
    expect(options.account?.storeAccountCookie).toBe(false);
    expect(options.account?.accountLinking).toEqual({
      enabled: false,
      disableImplicitLinking: true,
    });
    expect(options.session?.cookieCache).toEqual({ enabled: false });
    expect(options.session?.additionalFields).toEqual(STOREFRONT_SESSION_FIELDS);
    expect(STOREFRONT_SESSION_FIELDS.commerceOrganizationId.input).toBe(false);
    expect(options.session?.additionalFields).not.toHaveProperty("keycloakGrant");
    expect(options.trustedOrigins).toEqual([ENV.baseURL]);
    expect(options.advanced?.cookiePrefix).toBe("teck-storefront");
    expect(options.advanced?.crossSubDomainCookies).toEqual({ enabled: false });
    expect(options.databaseHooks?.account?.create?.before).toBeDefined();
    expect(options.databaseHooks?.account?.update?.before).toBeDefined();
    expect(options.databaseHooks?.session?.create?.before).toBeDefined();
    expect(options.databaseHooks?.session?.create?.after).toBeDefined();
    expect(options.advanced?.defaultCookieAttributes).toMatchObject({
      httpOnly: true,
      sameSite: "lax",
      secure: true,
      path: "/",
    });
    expect(options.telemetry).toEqual({ enabled: false });

    const plugins = options.plugins as unknown as {
      id?: string;
      options?: { config?: { requireIdTokenVerification?: boolean; pkce?: boolean }[] };
    }[];
    expect(plugins.map((plugin) => plugin.id)).toEqual(["generic-oauth", "next-cookies"]);
    expect(plugins[0]?.options?.config?.[0]?.requireIdTokenVerification).toBe(true);
    expect(plugins[0]?.options?.config?.[0]?.pkce).toBe(true);

    const pool = options.database as Pool;
    await pool.end();
  });
});
