import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

const root =
  process.env.TECK_CONTRACT_ROOT ??
  path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../docs/contracts/dedicated-postgresql/v1",
  );
const openApi = parse(fs.readFileSync(path.join(root, "openapi.yaml"), "utf8"));
const signalR = JSON.parse(fs.readFileSync(path.join(root, "signalr-events.schema.json"), "utf8"));
const realtimeSubscription = JSON.parse(
  fs.readFileSync(path.join(root, "realtime-subscription.schema.json"), "utf8"),
);

function schemaErrors(schema, value, path = "$", seen = new Set(), document = openApi) {
  if (schema.$ref) {
    assert.ok(!seen.has(schema.$ref), `cyclic schema reference ${schema.$ref}`);
    const next = new Set(seen).add(schema.$ref);
    const target = schema.$ref
      .split("/")
      .slice(1)
      .map((part) => part.replaceAll("~1", "/").replaceAll("~0", "~"))
      .reduce((current, key) => current?.[key], document);
    assert.ok(target, `unresolved schema reference ${schema.$ref}`);
    const { $ref, ...siblings } = schema;
    return [
      ...schemaErrors(target, value, path, next, document),
      ...schemaErrors(siblings, value, path, next, document),
    ];
  }

  const errors = [];
  if (schema.allOf)
    for (const member of schema.allOf)
      errors.push(...schemaErrors(member, value, path, seen, document));
  if (schema.oneOf) {
    const matches = schema.oneOf.filter(
      (member) => schemaErrors(member, value, path, seen, document).length === 0,
    );
    if (matches.length !== 1)
      errors.push(`${path} must match exactly one oneOf branch; matched ${matches.length}`);
  }
  if (schema.const !== undefined && value !== schema.const)
    errors.push(`${path} must equal ${JSON.stringify(schema.const)}`);
  if (schema.type) {
    const types = Array.isArray(schema.type) ? schema.type : [schema.type];
    const matches = types.some((type) =>
      type === "null"
        ? value === null
        : type === "object"
          ? value !== null && typeof value === "object" && !Array.isArray(value)
          : type === "array"
            ? Array.isArray(value)
            : type === "integer"
              ? Number.isInteger(value)
              : type === "number"
                ? typeof value === "number" && Number.isFinite(value)
                : typeof value === type,
    );
    if (!matches) return [`${path} must be ${types.join(" or ")}`];
  }
  if (
    schema.type === "object" &&
    value !== null &&
    typeof value === "object" &&
    !Array.isArray(value)
  ) {
    for (const required of schema.required ?? [])
      if (!(required in value)) errors.push(`${path}.${required} is required`);
    for (const [key, child] of Object.entries(schema.properties ?? {}))
      if (key in value)
        errors.push(...schemaErrors(child, value[key], `${path}.${key}`, seen, document));
    if (schema.additionalProperties === false)
      for (const key of Object.keys(value))
        if (!(key in (schema.properties ?? {}))) errors.push(`${path}.${key} is not allowed`);
  }
  if (schema.type === "array" && Array.isArray(value)) {
    if (schema.minItems !== undefined && value.length < schema.minItems)
      errors.push(`${path} must have at least ${schema.minItems} items`);
    if (schema.maxItems !== undefined && value.length > schema.maxItems)
      errors.push(`${path} must have at most ${schema.maxItems} items`);
    for (const [index, item] of value.entries()) {
      const child = schema.prefixItems?.[index] ?? schema.items;
      if (child === false) errors.push(`${path}[${index}] is not allowed`);
      else if (child && child !== true)
        errors.push(...schemaErrors(child, item, `${path}[${index}]`, seen, document));
    }
  }
  if (typeof value === "string") {
    if (schema.pattern && !new RegExp(schema.pattern).test(value))
      errors.push(`${path} does not match ${schema.pattern}`);
    if (schema.minLength !== undefined && value.length < schema.minLength)
      errors.push(`${path} is shorter than ${schema.minLength}`);
    if (schema.maxLength !== undefined && value.length > schema.maxLength)
      errors.push(`${path} is longer than ${schema.maxLength}`);
    if (
      schema.format === "date-time" &&
      (!value.endsWith("Z") || !Number.isFinite(Date.parse(value)))
    )
      errors.push(`${path} must be an RFC 3339 UTC timestamp ending in Z`);
  }
  if (typeof value === "number") {
    if (schema.minimum !== undefined && value < schema.minimum)
      errors.push(`${path} is below ${schema.minimum}`);
    if (schema.maximum !== undefined && value > schema.maximum)
      errors.push(`${path} is above ${schema.maximum}`);
  }
  return errors;
}
function signalRErrors(value) {
  const errors = [];
  errors.push(
    ...schemaErrors(signalR.$defs.operationChangedInvocation, value, "$", new Set(), signalR),
  );
  for (const argument of value.arguments ?? [])
    errors.push(
      ...schemaErrors(signalR.$defs.operationChangedHint, argument, "$", new Set(), signalR),
    );
  return errors;
}

function assertMatches(schema, value, label) {
  const errors = schemaErrors(schema, value);
  assert.deepEqual(errors, [], `${label}: ${errors.join("; ")}`);
}

assert.equal(openApi.openapi, "3.1.0", "OpenAPI must use 3.1.0");
assert.equal(openApi.info.version, "1.0.0", "contract must be versioned 1.0.0");
assert.ok(Object.keys(openApi.paths).length > 0, "OpenAPI must define routes");
for (const schema of [signalR, realtimeSubscription]) {
  assert.equal(schema.$schema, "https://json-schema.org/draft/2020-12/schema");
  assert.match(
    schema.$id,
    /^https:\/\/api\.teck\.cloud\/contracts\/dedicated-postgresql\/v1\/.+\.schema\.json$/,
  );
}
assert.equal(
  signalR.$id,
  "https://api.teck.cloud/contracts/dedicated-postgresql/v1/signalr-events.schema.json",
);
assert.equal(
  realtimeSubscription.$id,
  "https://api.teck.cloud/contracts/dedicated-postgresql/v1/realtime-subscription.schema.json",
);
assert.deepEqual(
  realtimeSubscription.oneOf.map((branch) => branch.$ref),
  ["#/$defs/subscribeToOrder", "#/$defs/unsubscribeFromOrder"],
);
for (const [name, schema] of Object.entries(realtimeSubscription.$defs)) {
  assert.deepEqual(schema.required, ["type", "invocationId", "target", "arguments"]);
  assert.equal(schema.properties.arguments.minItems, 1);
  assert.equal(schema.properties.arguments.maxItems, 1);
  assert.equal(schema.properties.arguments.items, false);
  const selection = schema.properties.arguments.prefixItems[0];
  assert.deepEqual(selection.required, ["orderId"]);
  assert.equal(selection.properties.orderId.pattern, "^ord_[A-Za-z0-9_-]+$");
  assert.equal(selection.additionalProperties, false);
  assert.equal(selection.properties.tenantId, undefined, `${name} cannot supply tenantId`);
  assert.equal(selection.properties.groupName, undefined, `${name} cannot supply groupName`);
  assert.match(name, /^(subscribeToOrder|unsubscribeFromOrder)$/);
}

const operations = Object.values(openApi.paths).flatMap((pathItem) =>
  Object.values(pathItem).filter(
    (operation) => operation && typeof operation === "object" && operation.operationId,
  ),
);
const operationIds = operations.map((operation) => operation.operationId);
assert.equal(new Set(operationIds).size, operationIds.length, "operationId values must be unique");
for (const operation of operations) {
  assert.ok(operation.responses, `${operation.operationId} must define responses`);
  assert.ok(
    operation["x-required-permission"],
    `${operation.operationId} must define a required permission`,
  );
}

const requiredRoutes = [
  "GET /v1/dedicated-database/offerings",
  "GET /v1/dedicated-database/options",
  "GET /v1/dedicated-database/orders",
  "POST /v1/dedicated-database/orders",
  "GET /v1/dedicated-database/orders/{orderId}",
  "GET /v1/dedicated-database/operations/{operationId}",
  "GET /v1/admin/dedicated-database/orders",
  "GET /v1/admin/dedicated-database/orders/{orderId}",
  "GET /v1/admin/dedicated-database/orders/{orderId}/diagnostics",
  "POST /v1/admin/dedicated-database/orders/{orderId}/actions/retry",
  "POST /v1/admin/dedicated-database/orders/{orderId}/actions/restore",
  "POST /v1/admin/dedicated-database/orders/{orderId}/actions/hold",
  "POST /v1/admin/dedicated-database/orders/{orderId}/actions/refund",
  "GET /v1/admin/dedicated-database/operations/{operationId}",
];
for (const methodRoute of requiredRoutes) {
  const [method, route] = methodRoute.split(" ");
  assert.ok(
    openApi.paths[route]?.[method.toLowerCase()],
    `missing required operation ${methodRoute}`,
  );
}
for (const [route, pathItem] of Object.entries(openApi.paths)) {
  assert.doesNotMatch(
    route,
    /(^|\/)(delete|purge)(\/|$)/i,
    `forbidden delete/purge route ${route}`,
  );
  for (const method of Object.keys(pathItem))
    assert.notEqual(method.toLowerCase(), "delete", `delete method forbidden on ${route}`);
}

for (const name of ["CustomerOrder", "OperationSnapshot", "AdminOrder", "Problem"]) {
  assert.notEqual(
    openApi.components.schemas[name].additionalProperties,
    false,
    `${name} must allow additive v1 response fields`,
  );
}
const forwardCompatibleOrder = {
  orderId: "ord_compat_probe",
  status: "futureServerState",
  paymentStatus: "futurePaymentState",
  offeringId: "off_demo",
  optionId: "opt_demo",
  priceSnapshot: {
    amountMinor: 1,
    currency: "EUR",
    interval: "month",
    capturedAt: "2026-11-20T12:00:00Z",
  },
  createdAt: "2026-11-20T12:00:00Z",
  updatedAt: "2026-11-20T12:00:00Z",
  placementSummary: null,
  entitlementSummary: null,
  futureResponseField: { safe: true },
};
assertMatches(
  { $ref: "#/components/schemas/CustomerOrder" },
  forwardCompatibleOrder,
  "v1 additive/unknown-enum compatibility probe",
);
assert.deepEqual(
  schemaErrors(
    { $ref: "#/components/schemas/CreateOrderRequest" },
    { offeringId: "off_demo", optionId: "opt_demo", tenantId: "ten_other" },
  ).length > 0,
  true,
  "customer requests must reject tenant scope input",
);

const exampleFiles = fs
  .readdirSync(root)
  .filter((name) => name.endsWith(".json") && name.startsWith("example-"));
const requiredFixtures = [
  "example-payment-pending.json",
  "example-migration-progress.json",
  "example-operation-complete.json",
  "example-stale-overlapping-notifications.json",
  "example-manual-exception.json",
  "example-problems.json",
];
for (const name of requiredFixtures)
  assert.ok(exampleFiles.includes(name), `missing required example fixture ${name}`);
for (const [name, example] of Object.entries(openApi.components.examples)) {
  const schemaName = name === "PaymentPendingOrder" ? "CustomerOrder" : "Problem";
  assertMatches(
    { $ref: `#/components/schemas/${schemaName}` },
    example.value,
    `OpenAPI example ${name}`,
  );
}

for (const file of exampleFiles) {
  const example = JSON.parse(fs.readFileSync(path.join(root, file), "utf8"));
  if (example.kind === "paymentPendingOrder") {
    assertMatches({ $ref: "#/components/schemas/CustomerOrder" }, example.order, file);
    assert.equal(example.order.status, "paymentPending");
    assert.equal(example.order.paymentStatus, "pending");
  } else if (example.kind === "operationSnapshot" || example.kind === "adminOperationSnapshot") {
    const snapshotSchema =
      example.kind === "adminOperationSnapshot" ? "AdminOperationSnapshot" : "OperationSnapshot";
    assertMatches({ $ref: `#/components/schemas/${snapshotSchema}` }, example.snapshot, file);
    assert.equal(
      example.snapshot.steps.length,
      example.snapshot.requiredStepCount,
      `${file} must preserve its frozen denominator`,
    );
    assert.equal(
      example.snapshot.steps.filter((step) => step.status === "succeeded").length,
      example.snapshot.succeededStepCount,
      `${file} success count mismatch`,
    );
    assert.equal(
      example.snapshot.steps.filter((step) => step.status === "failed").length,
      example.snapshot.failedStepCount,
      `${file} failure count mismatch`,
    );
  } else if (example.kind === "notificationRace") {
    assert.deepEqual(signalRErrors(example.stale), [], `${file} stale hint must match schema`);
    assert.deepEqual(
      signalRErrors(example.overlapping),
      [],
      `${file} overlapping hint must match schema`,
    );
    assert.ok(
      BigInt(example.stale.arguments[0].revision) <
        BigInt(example.overlapping.arguments[0].revision),
      `${file} compares revisions numerically`,
    );
    assert.ok(example.orderId, `${file} has an order subscription scope`);
    assert.deepEqual(
      schemaErrors(
        realtimeSubscription.$defs.subscribeToOrder,
        example.reconnect.subscribe,
        "$",
        new Set(),
        realtimeSubscription,
      ),
      [],
      `${file} reconnect subscription must match the client-to-server schema`,
    );
    assert.equal(example.reconnect.subscribe.arguments[0].orderId, example.orderId);
    assert.equal(example.reconnect.completion.type, 3);
    assert.equal(
      example.reconnect.completion.invocationId,
      example.reconnect.subscribe.invocationId,
      `${file} reconnect.completion.invocationId must match reconnect.subscribe.invocationId`,
    );
    assert.equal(example.reconnect.completion.result, null);
    assert.equal(
      example.reconnect.snapshotRequest,
      "GET /v1/dedicated-database/operations/op_migration_demo",
    );
    assert.deepEqual(
      schemaErrors(
        signalR.$defs.completionMessage,
        example.reconnect.completion,
        "$",
        new Set(),
        signalR,
      ),
      [],
      `${file} resubscription completion must match the server-to-client schema`,
    );
    assert.ok(
      example.reconnectRule.indexOf("resubscribe") < example.reconnectRule.indexOf("fetch"),
    );
    assert.ok(example.reconnectRule.includes("await"));
  } else if (example.kind === "problemCatalog") {
    for (const problem of example.examples)
      assertMatches({ $ref: "#/components/schemas/Problem" }, problem, `${file} problem`);
  } else {
    assert.fail(`${file} has unknown fixture kind ${example.kind}`);
  }
}

const pending = JSON.parse(
  fs.readFileSync(path.join(root, "example-payment-pending.json"), "utf8"),
);
assert.match(pending.order.priceSnapshot.capturedAt, /Z$/, "payment price snapshot uses UTC");
const progress = JSON.parse(
  fs.readFileSync(path.join(root, "example-migration-progress.json"), "utf8"),
);
assert.equal(progress.snapshot.requiredStepCount, 10);
assert.equal(progress.snapshot.succeededStepCount, 3);
assert.equal(progress.snapshot.failedStepCount, 1);
const manualException = JSON.parse(
  fs.readFileSync(path.join(root, "example-manual-exception.json"), "utf8"),
);
assert.equal(
  manualException.snapshot.manualException.activationRequired,
  true,
  "manual exception retains activation gate",
);
assert.equal(
  manualException.snapshot.manualException.activatedAt,
  null,
  "example does not imply activation authorization",
);
const problemFixture = JSON.parse(
  fs.readFileSync(path.join(root, "example-problems.json"), "utf8"),
);
for (const problem of problemFixture.examples)
  assertMatches({ $ref: "#/components/schemas/Problem" }, problem, "problem fixture");
const signalRFixture = JSON.parse(
  fs.readFileSync(path.join(root, "example-stale-overlapping-notifications.json"), "utf8"),
);
for (const hint of [signalRFixture.stale, signalRFixture.overlapping])
  assert.deepEqual(
    schemaErrors(signalR.$defs.operationChangedInvocation, hint, "$", new Set(), signalR),
    [],
    "SignalR invocation must match the declared schema",
  );
for (const hint of [signalRFixture.stale, signalRFixture.overlapping])
  assert.deepEqual(
    schemaErrors(signalR.$defs.operationChangedHint, hint.arguments[0], "$", new Set(), signalR),
    [],
    "SignalR hint arguments must match the declared schema",
  );

const clientInvocations = [
  {
    type: 1,
    invocationId: "1",
    target: "SubscribeToOrder",
    arguments: [{ orderId: "ord_migration_demo" }],
  },
  {
    type: 1,
    invocationId: "2",
    target: "UnsubscribeFromOrder",
    arguments: [{ orderId: "ord_migration_demo" }],
  },
];
for (const invocation of clientInvocations)
  assert.deepEqual(
    schemaErrors(realtimeSubscription, invocation, "$", new Set(), realtimeSubscription),
    [],
    `Client invocation ${invocation.target} must match the realtime subscription schema`,
  );

console.log(
  `Validated ${Object.keys(openApi.paths).length} paths, ${operations.length} operations, ${exampleFiles.length} fixtures, SignalR event schema and v1 additive compatibility.`,
);
