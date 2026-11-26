import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parse } from "yaml";

const root =
  process.env.TECK_COMMERCE_CONTRACT_ROOT ??
  path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../docs/contracts/commerce/v1");

const customer = parse(fs.readFileSync(path.join(root, "customer.openapi.yaml"), "utf8"));
const admin = parse(fs.readFileSync(path.join(root, "admin.openapi.yaml"), "utf8"));
const licenseDesired = JSON.parse(
  fs.readFileSync(path.join(root, "license-desired-event.schema.json"), "utf8"),
);
const licenseState = JSON.parse(
  fs.readFileSync(path.join(root, "license-state-event.schema.json"), "utf8"),
);

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const PROBLEM_CODES = new Map([
  ["invalid_request", 400],
  ["unauthenticated", 401],
  ["forbidden", 403],
  ["not_found", 404],
  ["idempotency_conflict", 409],
  ["stale_revision", 409],
  ["invalid_transition", 409],
  ["unsupported_option", 422],
  ["dependency_unavailable", 503],
]);
const ORDER_STATUSES = [
  "pendingPayment",
  "pendingApproval",
  "pendingReadiness",
  "active",
  "rejected",
  "failed",
];
const SAFE_INTEGER = 9007199254740991;
const MAX_UINT32 = 4294967295;

function schemaErrors(schema, value, pointer = "$", seen = new Set(), document = customer) {
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
      ...schemaErrors(target, value, pointer, next, document),
      ...schemaErrors(siblings, value, pointer, next, document),
    ];
  }

  const errors = [];
  if (schema.allOf)
    for (const member of schema.allOf)
      errors.push(...schemaErrors(member, value, pointer, seen, document));
  if (schema.oneOf) {
    const matches = schema.oneOf.filter(
      (member) => schemaErrors(member, value, pointer, seen, document).length === 0,
    );
    if (matches.length !== 1)
      errors.push(`${pointer} must match exactly one oneOf branch; matched ${matches.length}`);
  }
  if (schema.const !== undefined && value !== schema.const)
    errors.push(`${pointer} must equal ${JSON.stringify(schema.const)}`);
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
    if (!matches) return [`${pointer} must be ${types.join(" or ")}`];
  }
  if (
    schema.type === "object" &&
    value !== null &&
    typeof value === "object" &&
    !Array.isArray(value)
  ) {
    for (const required of schema.required ?? [])
      if (!(required in value)) errors.push(`${pointer}.${required} is required`);
    for (const [key, child] of Object.entries(schema.properties ?? {}))
      if (key in value)
        errors.push(...schemaErrors(child, value[key], `${pointer}.${key}`, seen, document));
    if (schema.additionalProperties === false)
      for (const key of Object.keys(value))
        if (!(key in (schema.properties ?? {}))) errors.push(`${pointer}.${key} is not allowed`);
  }
  if (schema.type === "array" && Array.isArray(value)) {
    if (schema.minItems !== undefined && value.length < schema.minItems)
      errors.push(`${pointer} must have at least ${schema.minItems} items`);
    if (schema.maxItems !== undefined && value.length > schema.maxItems)
      errors.push(`${pointer} must have at most ${schema.maxItems} items`);
    if (schema.uniqueItems) {
      const serialized = value.map((entry) => JSON.stringify(entry));
      if (new Set(serialized).size !== serialized.length)
        errors.push(`${pointer} must not contain duplicate items`);
    }
    for (const [index, item] of value.entries()) {
      const child = schema.prefixItems?.[index] ?? schema.items;
      if (child === false) errors.push(`${pointer}[${index}] is not allowed`);
      else if (child && child !== true)
        errors.push(...schemaErrors(child, item, `${pointer}[${index}]`, seen, document));
    }
  }
  if (schema.enum && !schema.enum.includes(value))
    errors.push(
      `${pointer} must be one of ${schema.enum.map((entry) => JSON.stringify(entry)).join(", ")}`,
    );
  if (typeof value === "string") {
    if (schema.pattern && !new RegExp(schema.pattern, "u").test(value))
      errors.push(`${pointer} does not match ${schema.pattern}`);
    if (schema.format === "uuid" && !UUID.test(value))
      errors.push(`${pointer} must be a lowercase UUID`);
    if (schema.minLength !== undefined && value.length < schema.minLength)
      errors.push(`${pointer} is shorter than ${schema.minLength}`);
    if (schema.maxLength !== undefined && value.length > schema.maxLength)
      errors.push(`${pointer} is longer than ${schema.maxLength}`);
    if (
      schema.format === "date-time" &&
      (!value.endsWith("Z") || !Number.isFinite(Date.parse(value)))
    )
      errors.push(`${pointer} must be an RFC 3339 UTC timestamp ending in Z`);
  }
  if (typeof value === "number") {
    if (schema.minimum !== undefined && value < schema.minimum)
      errors.push(`${pointer} is below ${schema.minimum}`);
    if (schema.maximum !== undefined && value > schema.maximum)
      errors.push(`${pointer} is above ${schema.maximum}`);
  }
  return errors;
}

function assertMatches(document, schema, value, label) {
  const errors = schemaErrors(schema, value, "$", new Set(), document);
  assert.deepEqual(errors, [], `${label}: ${errors.join("; ")}`);
}

function operationsOf(document) {
  return Object.entries(document.paths).flatMap(([route, pathItem]) =>
    Object.entries(pathItem)
      .filter(
        ([, operation]) => operation && typeof operation === "object" && operation.operationId,
      )
      .map(([method, operation]) => ({ method: method.toLowerCase(), route, operation })),
  );
}

function collectPropertyNames(schemas) {
  const names = new Set();
  const visit = (node) => {
    if (!node || typeof node !== "object") return;
    if (Array.isArray(node)) {
      for (const entry of node) visit(entry);
      return;
    }
    if (node.properties && typeof node.properties === "object")
      for (const key of Object.keys(node.properties)) {
        names.add(key);
        visit(node.properties[key]);
      }
    for (const key of ["items", "oneOf", "allOf", "anyOf", "additionalProperties", "prefixItems"])
      visit(node[key]);
  };
  visit(schemas);
  return names;
}

function readJson(file) {
  return JSON.parse(fs.readFileSync(path.join(root, file), "utf8"));
}

const CUSTOMER_FORBIDDEN = new Set([
  "tenantId",
  "actorId",
  "payment",
  "approval",
  "fulfillment",
  "audit",
  "paidThrough",
  "evidenceReference",
  "failureCode",
  "desiredRevision",
  "idempotencyKey",
]);
const ADMIN_FORBIDDEN = new Set([
  "license",
  "checkoutUrl",
  "secretReference",
  "connectionString",
  "stripeCustomerId",
  "stripeSessionId",
  "stripeInvoiceId",
  "stripeSubscriptionId",
  "stripePriceId",
  "providerCustomerId",
  "providerSessionId",
  "providerInvoiceId",
  "providerSubscriptionId",
  "providerPriceId",
  "providerReference",
  "rawProviderReference",
  "desiredRevision",
]);

function assertProjection(value, forbidden, label) {
  if (!value || typeof value !== "object") return;
  if (Array.isArray(value)) {
    for (const [index, child] of value.entries())
      assertProjection(child, forbidden, `${label}[${index}]`);
    return;
  }
  for (const [key, child] of Object.entries(value)) {
    assert.ok(!forbidden.has(key), `${label}.${key} is forbidden in this projection`);
    assertProjection(child, forbidden, `${label}.${key}`);
  }
}

// OpenAPI structure and authority.
const requiredResponseExamples = [
  [
    customer,
    "createCommerceOrder",
    "201",
    {
      shared: "CreatedSharedCheckout",
      dedicated: "CreatedDedicatedCheckout",
      pending: "CreatedCheckoutPending",
      paidReplay: "PaidCheckoutReplay",
    },
  ],
  [customer, "listCommerceOrders", "200", { orders: "OrdersPage" }],
  [admin, "listAdminCommerceOrders", "200", { orders: "AdminOrdersPage" }],
  [
    admin,
    "getAdminCommerceOrder",
    "200",
    {
      approved: "AdminOrderApproved",
      ready: "AdminOrderReady",
      sharedReady: "AdminOrderSharedReady",
      approvalPending: "AdminOrderApprovalPending",
      failed: "AdminOrderFailed",
      grantPending: "AdminOrderGrantPending",
      probeRetrying: "AdminOrderProbeRetrying",
      rejected: "AdminOrderRejected",
    },
  ],
  [admin, "decideAdminCommerceOrder", "200", { approved: "AdminOrderApproved" }],
  [admin, "recordAdminPlacementEvidence", "200", { verifying: "AdminOrderVerifying" }],
];
for (const [document, operationId, status, slots] of requiredResponseExamples) {
  const match = operationsOf(document).find(
    ({ operation }) => operation.operationId === operationId,
  );
  const examples = match?.operation.responses?.[status]?.content?.["application/json"]?.examples;
  for (const [slot, component] of Object.entries(slots))
    assert.equal(
      examples?.[slot]?.$ref,
      `#/components/examples/${component}`,
      `${operationId} ${status} application/json ${slot} must reference ${component}`,
    );
}

for (const [name, document] of [
  ["customer", customer],
  ["admin", admin],
]) {
  assert.equal(document.openapi, "3.1.0", `${name} OpenAPI must use 3.1.0`);
  assert.equal(document.info.version, "1.0.0", `${name} contract must be versioned 1.0.0`);
  assert.equal(
    document.servers?.[0]?.url,
    "https://api.teck.cloud",
    `${name} server must be HTTPS`,
  );
  assert.ok(document.components.securitySchemes.bearerAuth, `${name} must declare bearer auth`);
  assert.deepEqual(Object.keys(document.security?.[0] ?? {}), ["bearerAuth"]);
  const operations = operationsOf(document);
  const operationIds = operations.map(({ operation }) => operation.operationId);
  assert.equal(new Set(operationIds).size, operationIds.length, `${name} operationIds are unique`);
  for (const { method, route, operation } of operations) {
    assert.notEqual(method, "delete", `${name} forbids delete on ${route}`);
    assert.doesNotMatch(route, /\/(delete|purge|provision|deprovision)(\/|$)/i);
    assert.ok(operation.responses, `${operation.operationId} must define responses`);
    assert.ok(
      operation["x-required-permission"],
      `${operation.operationId} must define a required permission`,
    );
    assert.ok(Array.isArray(operation.tags) && operation.tags.length > 0);
  }
  const serialized = JSON.stringify(document.paths);
  assert.doesNotMatch(
    serialized,
    /mark-?paid|force-?paid|force-?ready|manual-?ready|provision-?evidence/i,
    `${name} exposes no payment or readiness override`,
  );
}

const customerRoutes = [
  "get /v1/commerce/catalog",
  "get /v1/commerce/basket",
  "put /v1/commerce/basket",
  "get /v1/commerce/orders",
  "post /v1/commerce/orders",
  "get /v1/commerce/orders/{orderId}",
];
for (const entry of customerRoutes) {
  const [method, route] = entry.split(" ");
  assert.ok(customer.paths[route]?.[method], `missing customer operation ${entry}`);
}
for (const { operation } of operationsOf(customer))
  assert.equal(operation["x-required-permission"], "commerce.customer");
for (const { operation } of operationsOf(customer)) {
  assert.equal(
    operation.responses["403"]?.$ref,
    "#/components/responses/Forbidden",
    `${operation.operationId} must declare the reachable 403 permission failure`,
  );
  assert.equal(
    operation.responses["401"]?.$ref,
    "#/components/responses/Unauthenticated",
    `${operation.operationId} must declare 401`,
  );
}
assert.equal(
  customer.paths["/v1/commerce/orders/{orderId}"].get.responses["400"]?.$ref,
  "#/components/responses/ValidationProblem",
  "customer order detail must declare 400 for a malformed orderId",
);
for (const operationId of ["listCommerceOrders", "createCommerceOrder", "getCommerceOrder"])
  assert.equal(
    operationsOf(customer).find(({ operation }) => operation.operationId === operationId)?.operation
      .responses?.["404"]?.$ref,
    "#/components/responses/NotFound",
    `${operationId} declares the scoped 404 on a missing reread`,
  );
for (const operationId of ["listCommerceOrders", "createCommerceOrder", "getCommerceOrder"]) {
  const operation = operationsOf(customer).find(
    ({ operation: candidate }) => candidate.operationId === operationId,
  )?.operation;
  assert.ok(operation, `customer operation ${operationId} exists`);
  assert.equal(
    operation.responses["503"]?.$ref,
    "#/components/responses/Unavailable",
    `${operationId} declares dependency 503`,
  );
  if (operationId !== "getCommerceOrder")
    assert.equal(
      operation.responses["404"]?.$ref,
      "#/components/responses/NotFound",
      `${operationId} declares scoped 404`,
    );
}

const adminPermissions = new Map([
  ["listAdminCommerceOrders", "CommerceOrdersRead"],
  ["getAdminCommerceOrder", "CommerceOrdersRead"],
  ["decideAdminCommerceOrder", "CommerceOrdersDecide"],
  ["recordAdminPlacementEvidence", "CommercePlacementRecord"],
]);
const adminOperations = operationsOf(admin);
assert.equal(
  adminOperations.length,
  adminPermissions.size,
  "admin exposes exactly four operations",
);
for (const { route, operation } of adminOperations) {
  assert.equal(
    operation["x-required-permission"],
    adminPermissions.get(operation.operationId),
    `${operation.operationId} permission`,
  );
  assert.match(route, /^\/v1\/admin\/commerce\/tenants\/\{tenantId\}\/orders/, "admin route scope");
}
for (const { operation } of adminOperations) {
  const names = (operation.parameters ?? []).map((parameter) =>
    parameter.$ref ? parameter.$ref.split("/").at(-1) : parameter.name,
  );
  assert.ok(names.includes("TenantId"), `${operation.operationId} requires a tenant parameter`);
}

// Schema shape and no cross-plane leakage.
const customerSchemas = customer.components.schemas;
const adminSchemas = admin.components.schemas;
const customerOrder = customerSchemas.CustomerOrder;
assert.deepEqual(customerOrder.required, [
  "orderId",
  "subscriptionId",
  "tier",
  "addOnCodes",
  "total",
  "status",
  "revision",
  "createdAt",
  "updatedAt",
  "license",
]);
assert.deepEqual(customerSchemas.OrderStatus.enum, ORDER_STATUSES);
assert.deepEqual(customerSchemas.Tier.enum, ["shared", "dedicated"]);
assert.deepEqual(customerSchemas.Money.properties.amountMinor.maximum, SAFE_INTEGER);
assert.equal(customerSchemas.Money.properties.amountMinor.minimum, 1);
assert.equal(customerSchemas.Money.properties.currency.pattern, "^[A-Z]{3}$");
assert.equal(customerSchemas.Revision.maximum, MAX_UINT32);
assert.equal(
  "desiredRevision" in customerOrder.properties,
  false,
  "CustomerOrder hides desiredRevision",
);
assert.equal(customerSchemas.BasketRevision.minimum, 0);
assert.equal(customerSchemas.BasketRevision.maximum, MAX_UINT32);
assert.equal(customerSchemas.PutBasketRequest.additionalProperties, false);
assert.equal(customerSchemas.CreateOrderRequest.additionalProperties, false);
assert.equal(
  customerSchemas.PutBasketRequest.properties.expectedRevision.$ref.split("/").at(-1),
  "BasketRevision",
);

const customerProperties = collectPropertyNames(customerSchemas);
for (const forbidden of [
  "payment",
  "approval",
  "fulfillment",
  "audit",
  "paidThrough",
  "actorId",
  "evidenceReference",
  "failureCode",
])
  assert.equal(
    customerProperties.has(forbidden),
    false,
    `customer contract must not expose staff-only property ${forbidden}`,
  );

const adminOrder = adminSchemas.AdminOrder;
for (const required of [
  "orderId",
  "subscriptionId",
  "tier",
  "addOnCodes",
  "total",
  "status",
  "revision",
  "createdAt",
  "updatedAt",
  "payment",
  "approval",
  "fulfillment",
  "audit",
])
  assert.ok(adminOrder.required.includes(required), `AdminOrder requires ${required}`);
assert.equal("license" in adminOrder.properties, false, "AdminOrder omits the license");
assert.equal("desiredRevision" in adminOrder.properties, false, "AdminOrder hides desiredRevision");
assert.deepEqual(adminSchemas.Payment.required, ["status", "paidThrough"]);
assert.deepEqual(adminSchemas.Payment.properties.status.enum, ["pending", "paid"]);
assert.deepEqual(adminSchemas.Approval.properties.status.enum, ["pending", "approved", "rejected"]);
assert.deepEqual(adminSchemas.Fulfillment.properties.status.enum, [
  "awaitingEvidence",
  "verifying",
  "ready",
  "failed",
]);
assert.deepEqual(adminSchemas.Fulfillment.properties.failureCode.oneOf[0].enum, [
  "binding_mismatch",
  "capability_unavailable",
  "probe_unavailable",
]);
assert.deepEqual(adminSchemas.AuditEntry.properties.action.enum, [
  "approve",
  "reject",
  "recordPlacementEvidence",
]);
assert.equal(adminSchemas.DecisionRequest.additionalProperties, false);
assert.equal(adminSchemas.PlacementEvidenceRequest.additionalProperties, false);
assert.equal(adminSchemas.DecisionRequest.properties.reason.maxLength, 512);
assert.equal(adminSchemas.PlacementEvidenceRequest.properties.evidenceReference.maxLength, 512);
const adminProperties = collectPropertyNames(adminSchemas);
for (const forbidden of ["license", "checkoutUrl", "secretReference", "connectionString"])
  assert.equal(
    adminProperties.has(forbidden),
    false,
    `admin contract must not expose ${forbidden}`,
  );

const requestSchemas = {
  customer: ["PutBasketRequest", "CreateOrderRequest"],
  admin: ["DecisionRequest", "PlacementEvidenceRequest"],
};
for (const [plane, names] of Object.entries(requestSchemas)) {
  const document = plane === "customer" ? customer : admin;
  for (const name of names) {
    const schema = document.components.schemas[name];
    for (const forbidden of ["tenantId", "tenantSlug", "actorId", "price", "total", "status"])
      assert.equal(forbidden in schema.properties, false, `${name} must not accept ${forbidden}`);
    assert.equal(schema.additionalProperties, false, `${name} rejects unknown properties`);
  }
}
const createOrderProbe = schemaErrors(customerSchemas.CreateOrderRequest, {
  basketRevision: 1,
  tenantId: "3f0f0f6e-6b1a-4d1e-9f4a-1c2b3d4e5f60",
});
assert.ok(createOrderProbe.length > 0, "customer order requests must reject tenant scope input");
const decisionProbe = schemaErrors(
  adminSchemas.PlacementEvidenceRequest,
  {
    placementId: "5d6e7f80-91a2-4c3d-9e4f-506172839405",
    evidenceReference: "evidence://ops/check",
    expectedRevision: 3,
    ready: true,
  },
  "$",
  new Set(),
  admin,
);
assert.ok(decisionProbe.length > 0, "staff requests cannot supply a ready flag");

// Problem contract and error-class fixtures.
for (const [name, document] of [
  ["customer", customer],
  ["admin", admin],
]) {
  const problem = document.components.schemas.Problem;
  assert.deepEqual(new Set(problem.properties.code.enum), new Set(PROBLEM_CODES.keys()));
  assert.deepEqual(
    new Set(problem.properties.status.enum),
    new Set([...PROBLEM_CODES.values()]),
    `${name} problem statuses`,
  );
  assert.deepEqual(problem.required, ["type", "title", "status", "code"]);
}
const problemFixture = readJson("example-problems.json");
assert.equal(problemFixture.kind, "problemCatalog");
assert.equal(problemFixture.examples.length, PROBLEM_CODES.size, "one fixture per error class");
for (const problem of problemFixture.examples) {
  assertMatches(customer, customerSchemas.Problem, problem, "problem fixture");
  assert.equal(problem.status, PROBLEM_CODES.get(problem.code), `${problem.code} status mapping`);
  assert.doesNotMatch(problem.detail ?? "", /[\r\n]|Exception|\bat [A-Z][\w.]+\b/);
}
assert.deepEqual(
  new Set(problemFixture.examples.map(({ code }) => code)),
  new Set(PROBLEM_CODES.keys()),
);

// Pagination and idempotency parameters.
for (const [name, document] of [
  ["customer", customer],
  ["admin", admin],
]) {
  const limit = document.components.parameters.Limit.schema;
  assert.equal(limit.default, 25, `${name} default limit`);
  assert.equal(limit.maximum, 100, `${name} maximum limit`);
  assert.equal(limit.minimum, 1);
  assert.equal(document.components.parameters.Cursor.schema.minLength, 1);
}
const idempotencyKey = customer.components.parameters.IdempotencyKey;
assert.equal(idempotencyKey.required, true);
assert.equal(idempotencyKey.name, "Idempotency-Key");
assert.match(idempotencyKey.schema.$ref, /IdempotencyKey$/u);
assert.equal(customerSchemas.IdempotencyKey.format, "uuid");

// OpenAPI examples are typed and file-backed fixtures agree with them.
const customerExampleSchemas = {
  Catalog: "CatalogResponse",
  EmptyBasket: "Basket",
  ClearedBasket: "Basket",
  DedicatedBasket: "Basket",
  PutDedicatedBasket: "PutBasketRequest",
  CreateSharedOrder: "CreateOrderRequest",
  OrdersPage: "OrderPage",
  CreatedSharedCheckout: "CreateOrderResponse",
  CreatedDedicatedCheckout: "CreateOrderResponse",
  CreatedCheckoutPending: "CreateOrderResponse",
  PaidCheckoutReplay: "CreateOrderResponse",
  GrantPendingOrder: "CustomerOrder",
  RetryPendingOrder: "CustomerOrder",
  PendingApprovalOrder: "CustomerOrder",
  ApprovalOnlyOrder: "CustomerOrder",
  SharedActiveOrder: "CustomerOrder",
  DedicatedActiveOrder: "CustomerOrder",
  PaymentPendingOrder: "CustomerOrder",
  ReadinessFailedOrder: "CustomerOrder",
  RejectedOrder: "CustomerOrder",
  InvalidRequestProblem: "Problem",
  UnauthenticatedProblem: "Problem",
  ForbiddenProblem: "Problem",
  NotFoundProblem: "Problem",
  StaleRevisionProblem: "Problem",
  IdempotencyConflictProblem: "Problem",
  InvalidTransitionProblem: "Problem",
  UnsupportedOptionProblem: "Problem",
  UnavailableProblem: "Problem",
};
const adminExampleSchemas = {
  AdminOrdersPage: "AdminOrderPage",
  AdminOrderApproved: "AdminOrder",
  AdminOrderGrantPending: "AdminOrder",
  AdminOrderProbeRetrying: "AdminOrder",
  AdminOrderRejected: "AdminOrder",
  AdminOrderVerifying: "AdminOrder",
  AdminOrderReady: "AdminOrder",
  AdminOrderSharedReady: "AdminOrder",
  AdminOrderApprovalPending: "AdminOrder",
  AdminOrderFailed: "AdminOrder",
  ApproveDecision: "DecisionRequest",
  RejectDecision: "DecisionRequest",
  RecordPlacementEvidence: "PlacementEvidenceRequest",
  InvalidRequestProblem: "Problem",
  UnauthenticatedProblem: "Problem",
  ForbiddenProblem: "Problem",
  NotFoundProblem: "Problem",
  StaleRevisionProblem: "Problem",
  InvalidTransitionProblem: "Problem",
  UnavailableProblem: "Problem",
};
for (const [name, document, mapping] of [
  ["customer", customer, customerExampleSchemas],
  ["admin", admin, adminExampleSchemas],
]) {
  const examples = document.components.examples ?? {};
  assert.deepEqual(
    new Set(Object.keys(examples)),
    new Set(Object.keys(mapping)),
    `${name} OpenAPI examples must have explicit schema mappings`,
  );
}

const fixtureExample = new Map([
  ["example-catalog.json", [customer, "Catalog"]],
  ["example-basket-empty.json", [customer, "EmptyBasket"]],
  ["example-basket-cleared.json", [customer, "ClearedBasket"]],
  ["example-basket-dedicated.json", [customer, "DedicatedBasket"]],
  ["example-checkout-created-shared.json", [customer, "CreatedSharedCheckout"]],
  ["example-checkout-pending.json", [customer, "CreatedCheckoutPending"]],
  ["example-checkout-replay-paid.json", [customer, "PaidCheckoutReplay"]],
  ["example-orders-page.json", [customer, "OrdersPage"]],
  ["example-order-payment-pending.json", [customer, "PaymentPendingOrder"]],
  ["example-order-shared-active.json", [customer, "SharedActiveOrder"]],
  ["example-order-dedicated-active.json", [customer, "DedicatedActiveOrder"]],
  ["example-order-grant-pending.json", [customer, "GrantPendingOrder"]],
  ["example-order-probe-retrying.json", [customer, "RetryPendingOrder"]],
  ["example-order-readiness-failed.json", [customer, "ReadinessFailedOrder"]],
  ["example-order-pending-approval.json", [customer, "PendingApprovalOrder"]],
  ["example-order-approval-only.json", [customer, "ApprovalOnlyOrder"]],
  ["example-order-rejected.json", [customer, "RejectedOrder"]],
  ["example-admin-orders-page.json", [admin, "AdminOrdersPage"]],
  ["example-admin-order-approved.json", [admin, "AdminOrderApproved"]],
  ["example-admin-order-verifying.json", [admin, "AdminOrderVerifying"]],
  ["example-admin-order-ready.json", [admin, "AdminOrderReady"]],
  ["example-admin-order-shared-ready.json", [admin, "AdminOrderSharedReady"]],
  ["example-admin-order-grant-pending.json", [admin, "AdminOrderGrantPending"]],
  ["example-admin-order-probe-retrying.json", [admin, "AdminOrderProbeRetrying"]],
  ["example-admin-order-approval-pending.json", [admin, "AdminOrderApprovalPending"]],
  ["example-admin-order-failed.json", [admin, "AdminOrderFailed"]],
  ["example-admin-order-rejected.json", [admin, "AdminOrderRejected"]],
  ["example-admin-decision-approve.json", [admin, "ApproveDecision"]],
  ["example-admin-decision-reject.json", [admin, "RejectDecision"]],
  ["example-admin-placement-evidence.json", [admin, "RecordPlacementEvidence"]],
]);
const fixtureFiles = fs
  .readdirSync(root)
  .filter((file) => file.endsWith(".json") && file.startsWith("example-"));
assert.deepEqual(
  new Set(fixtureExample.keys()).size,
  fixtureExample.size,
  "file-backed fixture mappings are unique",
);
for (const [file, fixture] of fixtureFiles.map((file) => [file, readJson(file)])) {
  if (
    ["customerOrder", "orderPage", "checkoutCreated", "adminOrder", "adminOrderPage"].includes(
      fixture.kind,
    )
  )
    assert.ok(fixtureExample.has(file), `${file} has an explicit OpenAPI example mapping`);
}
const requiredFixtures = [
  "example-catalog.json",
  "example-basket-empty.json",
  "example-basket-cleared.json",
  "example-basket-dedicated.json",
  "example-checkout-created-shared.json",
  "example-checkout-pending.json",
  "example-orders-page.json",
  "example-order-payment-pending.json",
  "example-order-approval-only.json",
  "example-order-shared-active.json",
  "example-order-dedicated-active.json",
  "example-order-grant-pending.json",
  "example-order-probe-retrying.json",
  "example-checkout-replay-paid.json",
  "example-order-readiness-failed.json",
  "example-order-pending-approval.json",
  "example-order-rejected.json",
  "example-admin-orders-page.json",
  "example-admin-order-approved.json",
  "example-admin-order-verifying.json",
  "example-admin-order-ready.json",
  "example-admin-order-shared-ready.json",
  "example-admin-order-grant-pending.json",
  "example-admin-order-probe-retrying.json",
  "example-admin-order-approval-pending.json",
  "example-admin-order-failed.json",
  "example-admin-order-rejected.json",
  "example-admin-decision-approve.json",
  "example-admin-decision-reject.json",
  "example-admin-placement-evidence.json",
  "example-problems.json",
  "example-license-desired.json",
  "example-license-state-active.json",
  "example-license-state-inactive.json",
  "example-license-verification.json",
];
for (const file of requiredFixtures)
  assert.ok(fixtureFiles.includes(file), `missing required fixture ${file}`);
for (const file of fixtureFiles.filter((name) =>
  [
    "customerOrder",
    "orderPage",
    "checkoutCreated",
    "adminOrder",
    "adminOrderPage",
    "decisionRequest",
    "placementEvidenceRequest",
  ].includes(readJson(name).kind),
))
  assert.ok(fixtureExample.has(file), `${file} has an explicit OpenAPI example mapping`);

const catalogFixture = readJson("example-catalog.json");
assert.equal(catalogFixture.kind, "catalog");
const offers = catalogFixture.response.offers;
assert.equal(offers.length, 2, "catalog fixture has shared and dedicated offers");
const priceFor = (tier, addOnCodes) => {
  const offer = offers.find((candidate) => candidate.tier === tier);
  assert.ok(offer, `offer for tier ${tier}`);
  let amount = offer.basePrice.amountMinor;
  for (const code of addOnCodes) {
    const addOn = offer.addOns.find((candidate) => candidate.code === code);
    assert.ok(addOn, `add-on ${code} is offered for ${tier}`);
    amount += addOn.price.amountMinor;
  }
  return { amountMinor: amount, currency: offer.basePrice.currency };
};
assert.deepEqual(
  priceFor("shared", []),
  { amountMinor: 1000, currency: "USD" },
  "shared fixture price",
);
assert.deepEqual(
  priceFor("dedicated", ["replicaReadAccess"]),
  { amountMinor: 3500, currency: "USD" },
  "dedicated plus add-on fixture price",
);
assert.deepEqual(
  priceFor("dedicated", []),
  { amountMinor: 3000, currency: "USD" },
  "dedicated fixture price",
);

const customerSnapshots = [];
const adminSnapshots = [];
const initialCheckoutSnapshots = [];
const checkoutResponses = [];

function validateCustomerOrder(order, label) {
  assertProjection(order, CUSTOMER_FORBIDDEN, label);
  assert.ok(
    ORDER_STATUSES.includes(order.status),
    `${label} status must be one of ${ORDER_STATUSES.join(", ")}`,
  );
  assert.deepEqual(order.total, priceFor(order.tier, order.addOnCodes), `${label} order total`);
  if (order.status === "active") {
    assert.notEqual(order.license, null, `${label} active license`);
    const expiresAt = Date.parse(order.license.expiresAt);
    assert.ok(Number.isFinite(expiresAt), `${label} license expiry is valid`);
    assert.ok(expiresAt > Date.parse(order.updatedAt), `${label} active license is unexpired`);
  } else assert.equal(order.license, null, `${label} nonactive license`);
}

function validateAdminOrder(order, label) {
  assertProjection(order, ADMIN_FORBIDDEN, label);
  assert.ok(
    ORDER_STATUSES.includes(order.status),
    `${label} status must be one of ${ORDER_STATUSES.join(", ")}`,
  );
  assert.deepEqual(order.total, priceFor(order.tier, order.addOnCodes), `${label} order total`);
  const updatedAt = Date.parse(order.updatedAt);
  assert.ok(Number.isFinite(updatedAt), `${label} updatedAt is valid`);
  const payment = order.payment;
  if (payment.status === "pending")
    assert.equal(payment.paidThrough, null, `${label} pending payment has no period`);
  if (payment.status === "paid") {
    assert.notEqual(payment.paidThrough, null, `${label} paid period exists`);
    assert.ok(Date.parse(payment.paidThrough) > updatedAt, `${label} paid period is current`);
  }
  const approval = order.approval;
  if (approval.status === "pending")
    assert.deepEqual(
      [approval.actorId, approval.reason, approval.occurredAt],
      [null, null, null],
      `${label} pending approval is empty`,
    );
  else {
    assert.notEqual(approval.actorId, null, `${label} decided actor`);
    assert.notEqual(approval.reason, null, `${label} decision reason`);
    assert.notEqual(approval.occurredAt, null, `${label} decision time`);
  }
  const fulfillment = order.fulfillment;
  if (fulfillment.status === "awaitingEvidence")
    assert.deepEqual(
      [
        fulfillment.placementId,
        fulfillment.evidenceReference,
        fulfillment.observedAt,
        fulfillment.failureCode,
      ],
      [null, null, null, null],
      `${label} awaiting evidence is empty`,
    );
  if (fulfillment.status === "verifying") {
    assert.notEqual(fulfillment.placementId, null, `${label} verifying placement`);
    assert.notEqual(fulfillment.evidenceReference, null, `${label} verifying evidence`);
    assert.equal(fulfillment.observedAt, null, `${label} verifying has no observation`);
    assert.ok(
      fulfillment.failureCode === null || fulfillment.failureCode === "probe_unavailable",
      `${label} verifying only permits retryable probe failure`,
    );
  }
  if (fulfillment.status === "ready") {
    assert.notEqual(fulfillment.placementId, null, `${label} ready placement`);
    assert.notEqual(fulfillment.evidenceReference, null, `${label} ready evidence`);
    assert.equal(fulfillment.failureCode, null, `${label} ready has no failure`);
    const observedAt = Date.parse(fulfillment.observedAt);
    assert.ok(Number.isFinite(observedAt), `${label} ready proof time is valid`);
    assert.ok(
      observedAt <= updatedAt && updatedAt < observedAt + 15 * 60 * 1000,
      `${label} ready proof is fresh`,
    );
  }
  if (fulfillment.status === "failed") {
    assert.notEqual(fulfillment.placementId, null, `${label} failed placement`);
    assert.notEqual(fulfillment.evidenceReference, null, `${label} failed evidence`);
    assert.ok(
      ["binding_mismatch", "capability_unavailable"].includes(fulfillment.failureCode),
      `${label} terminal fulfillment failure code`,
    );
    assert.ok(Date.parse(fulfillment.observedAt) <= updatedAt, `${label} terminal failure time`);
  }
  if (approval.status === "rejected") {
    assert.equal(order.status, "rejected", `${label} rejection is final`);
    return;
  }
  if (order.status === "rejected") {
    assert.equal(approval.status, "rejected", `${label} rejected order has final decision`);
    return;
  }
  if (order.status === "pendingPayment") {
    assert.equal(payment.status, "pending", `${label} pending payment gate`);
  }
  if (order.status === "pendingApproval") {
    assert.equal(payment.status, "paid", `${label} pending approval gate paid`);
    assert.equal(approval.status, "pending", `${label} pending approval gate decision`);
  }
  if (order.status === "pendingReadiness") {
    assert.equal(payment.status, "paid", `${label} pending readiness gate paid`);
    assert.equal(approval.status, "approved", `${label} pending readiness gate approved`);
    assert.ok(
      ["awaitingEvidence", "verifying", "ready"].includes(fulfillment.status),
      `${label} pending readiness gate proof`,
    );
  }
  if (order.status === "failed") {
    assert.equal(payment.status, "paid", `${label} failed order is paid`);
    assert.equal(approval.status, "approved", `${label} failed order is approved`);
    assert.equal(fulfillment.status, "failed", `${label} failed status has terminal fulfillment`);
  }
  if (order.status === "active") {
    assert.equal(payment.status, "paid", `${label} active gate paid`);
    assert.equal(approval.status, "approved", `${label} active gate approved`);
    assert.equal(fulfillment.status, "ready", `${label} active gate ready`);
  }
}

function validateExample(document, schemaName, payload, label) {
  const schema = document.components.schemas[schemaName];
  assert.ok(schema, `${label} schema ${schemaName} exists`);
  assertMatches(document, { $ref: `#/components/schemas/${schemaName}` }, payload, label);
  if (schemaName === "CustomerOrder") {
    validateCustomerOrder(payload, label);
    customerSnapshots.push({ order: payload, label });
  }
  if (schemaName === "AdminOrder") {
    validateAdminOrder(payload, label);
    adminSnapshots.push({ order: payload, label });
  }
  if (schemaName === "DecisionRequest") {
    assert.equal(payload.reason, payload.reason.trim(), `${label} reason is trimmed`);
    assert.ok(payload.reason.length <= 512, `${label} reason length`);
  }
  if (schemaName === "PlacementEvidenceRequest") {
    assert.match(payload.placementId, UUID, `${label} placement UUID`);
    assert.equal(
      payload.evidenceReference,
      payload.evidenceReference.trim(),
      `${label} evidence is trimmed`,
    );
    assert.ok(payload.evidenceReference.length <= 512, `${label} evidence length`);
  }
  if (schemaName === "CreateOrderResponse") {
    assertProjection(payload, CUSTOMER_FORBIDDEN, label);
    checkoutResponses.push({ response: payload, label });
    validateCustomerOrder(payload.order, `${label}.order`);
    customerSnapshots.push({ order: payload.order, label: `${label}.order` });
    if (label === "example-checkout-created-shared.json")
      initialCheckoutSnapshots.push({ order: payload.order, label: `${label}.order` });
    assert.equal(
      payload.checkoutUrl === null || /^https:\/\//u.test(payload.checkoutUrl),
      true,
      `${label} checkout URL is HTTPS or null`,
    );
    if (payload.order.status !== "pendingPayment")
      assert.equal(payload.checkoutUrl, null, `${label} nonpayable checkout URL`);
  }
  if (schemaName === "OrderPage" || schemaName === "AdminOrderPage") {
    const customerPlane = schemaName === "OrderPage";
    const snapshots = customerPlane ? customerSnapshots : adminSnapshots;
    assertProjection(payload, customerPlane ? CUSTOMER_FORBIDDEN : ADMIN_FORBIDDEN, label);
    assert.ok(
      typeof payload.nextCursor === "string" || payload.nextCursor === null,
      `${label} cursor is string or null`,
    );
    const ids = payload.items.map(({ orderId }) => orderId);
    assert.equal(new Set(ids).size, ids.length, `${label} duplicate orderId`);
    let previous;
    for (const [index, order] of payload.items.entries()) {
      const itemLabel = `${label}.items[${index}]`;
      (customerPlane ? validateCustomerOrder : validateAdminOrder)(order, itemLabel);
      const updatedAt = Date.parse(order.updatedAt);
      assert.ok(Number.isFinite(updatedAt), `${itemLabel} updatedAt is valid`);
      if (previous)
        assert.ok(
          previous.updatedAt > updatedAt ||
            (previous.updatedAt === updatedAt && previous.orderId < order.orderId),
          `${itemLabel} page order updatedAt desc, orderId asc`,
        );
      previous = { updatedAt, orderId: order.orderId };
      snapshots.push({ order, label: itemLabel });
    }
  }
}
for (const file of fixtureFiles) {
  const fixture = readJson(file);
  const fixtureSchemaName = {
    catalog: "CatalogResponse",
    basket: "Basket",
    checkoutCreated: "CreateOrderResponse",
    customerOrder: "CustomerOrder",
    orderPage: "OrderPage",
    adminOrder: "AdminOrder",
    adminOrderPage: "AdminOrderPage",
    decisionRequest: "DecisionRequest",
    placementEvidenceRequest: "PlacementEvidenceRequest",
  }[fixture.kind];
  if (fixtureSchemaName) {
    const document =
      fixture.kind.startsWith("admin") || fixture.kind.endsWith("Request") ? admin : customer;
    const payloadKey = Object.keys(fixture).find((key) => key !== "kind");
    validateExample(document, fixtureSchemaName, fixture[payloadKey], file);
  }
  switch (fixture.kind) {
    case "catalog":
      for (const offer of fixture.response.offers) {
        assert.ok(offer.basePrice.amountMinor > 0, `${file}.${offer.tier} base price`);
        assert.ok(
          offer.basePrice.amountMinor <= SAFE_INTEGER,
          `${file}.${offer.tier} base price safe integer`,
        );
        for (const addOn of offer.addOns) {
          assert.ok(addOn.price.amountMinor > 0, `${file}.${addOn.code} add-on price`);
          assert.ok(
            addOn.price.amountMinor <= SAFE_INTEGER,
            `${file}.${addOn.code} add-on price safe integer`,
          );
        }
      }
      break;
    case "basket": {
      const { selection, revision, total } = fixture.response;
      if (selection === null) {
        assert.ok(
          Number.isInteger(revision) && revision >= 0 && revision <= MAX_UINT32,
          `${file} basket revision is uint32`,
        );
        if (file === "example-basket-empty.json")
          assert.equal(revision, 0, `${file} untouched revision`);
        else assert.ok(revision >= 1, `${file} cleared basket keeps its positive revision`);
        assert.equal(total, null, `${file} empty basket total`);
      } else {
        assert.ok(
          Number.isInteger(revision) && revision >= 1 && revision <= MAX_UINT32,
          `${file} populated basket revision is uint32`,
        );
        const offer = offers.find((candidate) => candidate.offerId === selection.offerId);
        assert.ok(offer, `${file} selected offer is offered`);
        assert.deepEqual(total, priceFor(offer.tier, selection.addOnCodes), `${file} basket total`);
      }
      break;
    }
    case "checkoutCreated":
      validateExample(customer, "CreateOrderResponse", fixture.response, file);
      break;
    case "customerOrder":
      validateExample(customer, "CustomerOrder", fixture.order, file);
      break;
    case "orderPage":
      validateExample(customer, "OrderPage", fixture.page, file);
      break;
    case "adminOrder":
      validateExample(admin, "AdminOrder", fixture.order, file);
      break;
    case "adminOrderPage":
      validateExample(admin, "AdminOrderPage", fixture.page, file);
      break;
    case "adminDecisionMutationResponse":
    case "adminEvidenceMutationResponse":
      validateExample(admin, "AdminOrder", fixture.order, file);
      break;
    case "decisionRequest": {
      validateExample(admin, "DecisionRequest", fixture.request, file);
      break;
    }
    case "placementEvidenceRequest": {
      validateExample(admin, "PlacementEvidenceRequest", fixture.request, file);
      break;
    }
    case "problemCatalog":
      break;
    case "licenseDesiredEvent":
      assertMatches(licenseDesired, licenseDesired, fixture.event, file);
      assert.equal(fixture.event.revision >= 1, true, `${file} revision`);
      break;
    case "licenseStateEvent": {
      assertMatches(licenseState, licenseState, fixture.event, file);
      assert.equal(fixture.event.revision, fixture.event.desiredRevision, `${file} revision echo`);
      if (fixture.event.state === "active")
        for (const field of ["licenseRevision", "keyId", "expiresAt"])
          assert.notEqual(fixture.event[field], null, `${file} active ${field}`);
      else
        for (const field of ["licenseRevision", "keyId", "expiresAt"])
          assert.equal(fixture.event[field], null, `${file} inactive ${field}`);
      break;
    }
    case "licenseVerification": {
      const { valid, rejections } = fixture;
      assert.equal(valid.valid, true, `${file} valid result`);
      assert.equal(valid.rejection, null, `${file} valid rejection`);
      const expectedRejections = [
        "expired",
        "invalid_signature",
        "revoked",
        "unknown_key",
        "wrong_binding",
      ];
      for (const [index, item] of rejections.entries())
        assert.ok(
          expectedRejections.includes(item.rejection),
          `${file}.rejections[${index}].rejection has unsupported code ${item.rejection}`,
        );
      assert.deepEqual(rejections.map(({ rejection }) => rejection).sort(), expectedRejections);
      for (const rejection of rejections) {
        assert.equal(rejection.valid, false, `${file} rejection valid flag`);
        assert.equal(rejection.claims, null, `${file} rejection claims`);
      }
      break;
    }
    default:
      assert.fail(`unknown fixture kind ${fixture.kind} in ${file}`);
  }
}

for (const [name, document, mapping] of [
  ["customer", customer, customerExampleSchemas],
  ["admin", admin, adminExampleSchemas],
]) {
  for (const [exampleName, schemaName] of Object.entries(mapping)) {
    const example = document.components.examples[exampleName];
    assert.ok(example, `${name} OpenAPI example ${exampleName} exists`);
    const exampleLabel = `${name} OpenAPI example ${exampleName}`;
    const mountedAsOrderResponse = ["AdminOrderApproved", "AdminOrderVerifying"].includes(
      exampleName,
    );
    const actualSchema =
      exampleName === "CreateSharedOrder"
        ? "CreateOrderRequest"
        : ["ApproveDecision", "RejectDecision", "RecordPlacementEvidence"].includes(exampleName)
          ? exampleName === "RecordPlacementEvidence"
            ? "PlacementEvidenceRequest"
            : "DecisionRequest"
          : schemaName;
    validateExample(
      document,
      actualSchema,
      example.value,
      mountedAsOrderResponse ? `admin mutation response ${exampleName}` : exampleLabel,
    );
  }
}

// Check replay intent before lifecycle joins so a foreign replay cannot mask the binding error.
const firstCreatedOrder = initialCheckoutSnapshots[0]?.order;
const replayedOrder = checkoutResponses.find(
  ({ label }) => label === "example-checkout-replay-paid.json",
)?.response.order;
assert.ok(firstCreatedOrder, "shared first-create customer projection exists");
assert.ok(replayedOrder, "shared paid replay response exists");
assert.equal(replayedOrder.orderId, firstCreatedOrder.orderId, "shared replay order binding");
assert.equal(
  replayedOrder.subscriptionId,
  firstCreatedOrder.subscriptionId,
  "shared replay subscription binding",
);

// Lifecycle cross-checks between customer and admin projections.
const adminEvidenceFields = {
  payment: ["status", "paidThrough"],
  approval: ["status", "actorId", "reason", "occurredAt"],
  fulfillment: ["status", "placementId", "evidenceReference", "observedAt", "failureCode"],
};
function assertAdminEvidenceAgreement(order, reference, label) {
  for (const [field, keys] of Object.entries(adminEvidenceFields))
    for (const key of keys)
      assert.deepEqual(
        order[field][key],
        reference[field][key],
        `${label} same-snapshot ${field} agreement (${key})`,
      );
}

for (const { order: customerOrder, label } of customerSnapshots) {
  const active = customerOrder.status === "active";
  const grantPendingReplay =
    customerOrder.status === "pendingReadiness" &&
    [
      "example-checkout-replay-paid.json.order",
      "customer OpenAPI example PaidCheckoutReplay.order",
    ].includes(label);
  if (!active && !grantPendingReplay) continue;

  const sameInstantAdmin = adminSnapshots.filter(
    ({ order }) =>
      order.orderId === customerOrder.orderId && order.updatedAt === customerOrder.updatedAt,
  );
  for (const { order, label: adminLabel } of sameInstantAdmin.slice(1))
    assertAdminEvidenceAgreement(order, sameInstantAdmin[0].order, adminLabel);
  const evidence = sameInstantAdmin.find(
    ({ order }) =>
      order.subscriptionId === customerOrder.subscriptionId &&
      order.revision === customerOrder.revision &&
      order.status === customerOrder.status &&
      order.payment.status === "paid" &&
      order.approval.status === "approved" &&
      order.fulfillment.status === "ready",
  );
  assert.ok(
    evidence,
    `${label} requires same-snapshot Admin ${active ? "active grant" : "ready grant-pending evidence"}`,
  );
  const { order } = evidence;
  assert.equal(order.tier, customerOrder.tier, `${label} tier binding`);
  assert.deepEqual(order.addOnCodes, customerOrder.addOnCodes, `${label} add-on binding`);
  assert.deepEqual(order.total, customerOrder.total, `${label} total binding`);
  const observedAt = Date.parse(order.fulfillment.observedAt);
  const updatedAt = Date.parse(customerOrder.updatedAt);
  const paidThrough = Date.parse(order.payment.paidThrough);
  assert.ok(
    order.payment.status === "paid" && paidThrough > updatedAt,
    `${label} current paid period`,
  );
  assert.ok(
    observedAt <= updatedAt && updatedAt < observedAt + 15 * 60 * 1000,
    `${label} fresh proof`,
  );
  if (!active) continue;
  const expectedExpiry = Math.min(paidThrough, observedAt + 15 * 60 * 1000);
  assert.equal(
    Date.parse(customerOrder.license.expiresAt),
    expectedExpiry,
    `${label} order license expiry clamp`,
  );
  assert.ok(Date.parse(customerOrder.license.expiresAt) > updatedAt, `${label} license unexpired`);
}
for (const orderId of new Set(adminSnapshots.map(({ order }) => order.orderId))) {
  const customerHistory = customerSnapshots.filter(({ order }) => order.orderId === orderId);
  const adminHistory = adminSnapshots.filter(({ order }) => order.orderId === orderId);
  assert.ok(customerHistory.length > 0, `${orderId} Admin fixture has a matching customer fixture`);
  const byInstant = new Map();
  for (const [plane, snapshots] of [
    ["customer", customerHistory],
    ["admin", adminHistory],
  ])
    for (const snapshot of snapshots) {
      const { order, label } = snapshot;
      const timestamp = Date.parse(order.updatedAt);
      assert.ok(Number.isFinite(timestamp), `${label} updatedAt is valid`);
      const entry = byInstant.get(timestamp) ?? { orderId, timestamp, customer: [], admin: [] };
      entry[plane].push(snapshot);
      byInstant.set(timestamp, entry);
    }
  const history = [...byInstant.values()].sort((a, b) => a.timestamp - b.timestamp);
  const immutableReference = history[0]?.customer[0]?.order ?? history[0]?.admin[0]?.order;
  assert.ok(immutableReference, `${orderId} has a historical snapshot`);
  for (const { order, label } of [...customerHistory, ...adminHistory])
    for (const field of ["subscriptionId", "tier", "addOnCodes", "total", "createdAt"])
      assert.deepEqual(
        order[field],
        immutableReference[field],
        `${label} immutable ${field} binding`,
      );
  for (let index = 0; index < history.length; index++) {
    const current = history[index];
    assert.ok(
      current.customer.length || current.admin.length,
      `${orderId} snapshot group is nonempty`,
    );
    const records = [...current.customer, ...current.admin];
    const reference = records[0].order;
    for (const { order, label } of records.slice(1)) {
      for (const field of ["orderId", "subscriptionId", "tier", "addOnCodes", "total", "createdAt"])
        assert.deepEqual(order[field], reference[field], `${label} immutable ${field} binding`);
      assert.equal(order.updatedAt, reference.updatedAt, `${label} same-instant updatedAt binding`);
      if (current.customer.length && current.admin.length) {
        assert.equal(order.status, reference.status, `${label} same-instant status`);
        assert.equal(order.revision, reference.revision, `${label} same-instant revision`);
      }
      if ("license" in order && "license" in reference)
        assert.deepEqual(order.license, reference.license, `${label} same-instant license binding`);
      if ("payment" in order && "payment" in reference)
        assertAdminEvidenceAgreement(order, reference, label);
      if ("audit" in order && "audit" in reference)
        assert.deepEqual(order.audit, reference.audit, `${label} same-instant audit binding`);
    }
    for (const plane of ["customer", "admin"])
      for (const { order, label } of current[plane].slice(1)) {
        const baseline = current[plane][0].order;
        for (const field of [
          "orderId",
          "subscriptionId",
          "tier",
          "total",
          "createdAt",
          "updatedAt",
          "status",
          "revision",
        ])
          assert.deepEqual(order[field], baseline[field], `${label} same-plane ${field}`);
        assert.deepEqual(order.addOnCodes, baseline.addOnCodes, `${label} same-plane add-ons`);
      }
    if (index > 0)
      assert.ok(
        reference.revision >
          history[index - 1].customer.concat(history[index - 1].admin)[0].order.revision,
        `${orderId} historical revision increases`,
      );
  }
}

const desiredEvent = readJson("example-license-desired.json").event;
const activeLicenseState = readJson("example-license-state-active.json").event;
assert.equal(desiredEvent.enabled, true, "active outcome has enabled desired state");
assert.match(desiredEvent.tenantId, UUID, "desired tenant is a UUID");
assert.equal(desiredEvent.tenantId, activeLicenseState.tenantId, "desired outcome tenant binding");
assert.match(activeLicenseState.orderId, UUID, "outcome order is a UUID");
assert.match(activeLicenseState.subscriptionId, UUID, "outcome subscription is a UUID");
assert.equal(desiredEvent.orderId, activeLicenseState.orderId, "desired outcome order binding");
assert.equal(
  desiredEvent.subscriptionId,
  activeLicenseState.subscriptionId,
  "desired outcome subscription binding",
);
assert.equal(desiredEvent.revision, activeLicenseState.revision, "desired outcome generation");
assert.equal(
  activeLicenseState.desiredRevision,
  desiredEvent.revision,
  "desired outcome generation",
);
assert.ok(
  Date.parse(activeLicenseState.occurredAt) >= Date.parse(desiredEvent.occurredAt),
  "active outcome follows desired event",
);
assert.equal(activeLicenseState.expiresAt, desiredEvent.notAfter, "desired outcome expiry");
const activeCustomer = customerSnapshots.find(
  ({ order }) =>
    order.orderId === desiredEvent.orderId &&
    order.subscriptionId === desiredEvent.subscriptionId &&
    order.updatedAt === activeLicenseState.occurredAt &&
    order.status === "active",
)?.order;
assert.ok(activeCustomer?.license, "active outcome binds an exact-time licensed customer order");
assert.equal(activeCustomer.tier, desiredEvent.tier, "desired customer tier binding");
assert.deepEqual(
  activeCustomer.addOnCodes,
  desiredEvent.addOnCodes,
  "desired customer add-on binding",
);
assert.equal(activeLicenseState.licenseRevision, activeCustomer.license.revision);
assert.equal(activeLicenseState.keyId, activeCustomer.license.keyId);
assert.equal(activeLicenseState.expiresAt, activeCustomer.license.expiresAt);

const licenseVerification = readJson("example-license-verification.json");
const claims = licenseVerification.valid.claims;
assert.equal(claims.tenantId, desiredEvent.tenantId, "license claims tenant binding");
assert.equal(claims.orderId, desiredEvent.orderId, "license claims order binding");
assert.equal(
  claims.subscriptionId,
  desiredEvent.subscriptionId,
  "license claims subscription binding",
);
assert.equal(claims.tier, desiredEvent.tier, "license claims desired tier binding");
assert.deepEqual(
  claims.addOnCodes,
  desiredEvent.addOnCodes,
  "license claims desired add-on binding",
);
assert.equal(claims.keyId, activeLicenseState.keyId, "license claims key binding");
assert.equal(
  claims.revision,
  activeLicenseState.licenseRevision,
  "license claims revision binding",
);
assert.equal(claims.expiresAt, activeLicenseState.expiresAt, "license claims expiry binding");
assert.equal(claims.orderId, activeCustomer.orderId, "license claims customer order binding");
assert.equal(
  claims.subscriptionId,
  activeCustomer.subscriptionId,
  "license claims customer subscription binding",
);

for (const [file, [document, exampleName]] of fixtureExample) {
  assert.ok(fixtureFiles.includes(file), `mapped fixture ${file} exists`);
  const fixture = readJson(file);
  const payloadKey = Object.keys(fixture).find((key) => key !== "kind");
  assert.deepEqual(
    fixture[payloadKey],
    document.components.examples[exampleName].value,
    `${file} matches OpenAPI example ${exampleName}`,
  );
}

// Branch coverage for the states the consumer sign-off named explicitly.
assert.equal(
  readJson("example-checkout-pending.json").response.checkoutUrl,
  null,
  "the contract keeps an example for an order whose checkout is not yet available",
);
assert.equal(
  readJson("example-admin-order-approval-pending.json").order.approval.status,
  "pending",
  "the patient-admin queue state has a pending-approval example",
);
assert.equal(
  readJson("example-order-rejected.json").order.status,
  "rejected",
  "the customer rejection state has an example",
);
const failedFulfillment = readJson("example-admin-order-failed.json").order.fulfillment;
assert.equal(failedFulfillment.status, "failed", "the failed fulfillment state has an example");
assert.equal(failedFulfillment.failureCode, "binding_mismatch", "failed fulfillment sets a code");

// Event schema metadata and shared shape.
for (const [name, schema] of [
  ["license-desired", licenseDesired],
  ["license-state", licenseState],
]) {
  assert.equal(schema.$schema, "https://json-schema.org/draft/2020-12/schema");
  assert.equal(
    schema.$id,
    `https://api.teck.cloud/contracts/commerce/v1/${name}-event.schema.json`,
  );
  for (const field of [
    "messageId",
    "occurredAt",
    "tenantId",
    "orderId",
    "subscriptionId",
    "revision",
  ])
    assert.ok(schema.required.includes(field), `${name} carries ${field}`);
  assert.equal(schema.properties.tenantId.format, "uuid");
  assert.equal(schema.properties.revision.maximum, MAX_UINT32);
  assert.equal(schema.additionalProperties, false);
  assert.equal(schema.oneOf.length, 2, `${name} distinguishes active/desired branches`);
}
assert.deepEqual(Object.keys(licenseDesired.properties).sort(), [
  "addOnCodes",
  "enabled",
  "messageId",
  "notAfter",
  "occurredAt",
  "orderId",
  "revision",
  "subscriptionId",
  "tenantId",
  "tier",
]);
assert.deepEqual(Object.keys(licenseState.properties).sort(), [
  "desiredRevision",
  "expiresAt",
  "keyId",
  "licenseRevision",
  "messageId",
  "occurredAt",
  "orderId",
  "revision",
  "state",
  "subscriptionId",
  "tenantId",
]);

// Protobuf structure.
function parseProto(file) {
  const text = fs.readFileSync(path.join(root, file), "utf8");
  assert.match(text, /^syntax = "proto3";/m, `${file} uses proto3`);
  assert.match(
    text,
    /^import "google\/protobuf\/timestamp\.proto";/m,
    `${file} imports timestamps`,
  );
  assert.doesNotMatch(text, /\brequired\b|\bextend\b|\bgroup\b/, `${file} uses no proto2 syntax`);
  const packageName = text.match(/^package ([a-z0-9_.]+);$/m)?.[1];
  const services = [...text.matchAll(/service (\w+) \{([^{}]*)\}/g)].map((match) => ({
    name: match[1],
    rpcs: [...match[2].matchAll(/rpc\s+(\w+)\s*\(\s*(\w+)\s*\)\s*returns\s*\(\s*(\w+)\s*\)/g)].map(
      (rpc) => ({ name: rpc[1], request: rpc[2], response: rpc[3] }),
    ),
  }));
  const messages = new Map();
  for (const match of text.matchAll(/message (\w+) \{([^{}]*)\}/g)) {
    const fields = [
      ...match[2].matchAll(
        /^\s*(?:(optional|repeated) )?([A-Za-z_.][\w.]*) ([a-z][a-z0-9_]*) = (\d+);$/gm,
      ),
    ].map((field) => ({
      label: field[1] ?? null,
      type: field[2],
      name: field[3],
      number: Number(field[4]),
    }));
    messages.set(match[1], fields);
  }
  const enums = new Map();
  for (const match of text.matchAll(/enum (\w+) \{([^{}]*)\}/g)) {
    const values = [...match[2].matchAll(/^\s*([A-Z][A-Z0-9_]*) = (\d+);$/gm)].map((value) => ({
      name: value[1],
      number: Number(value[2]),
    }));
    enums.set(match[1], values);
  }
  return { file, text, packageName, services, messages, enums };
}

function assertSequentialFields(proto, messageName) {
  const fields = proto.messages.get(messageName);
  assert.ok(fields, `${proto.file} defines message ${messageName}`);
  for (const [index, field] of fields.entries())
    assert.equal(
      field.number,
      index + 1,
      `${proto.file} ${messageName}.${field.name} must use field number ${index + 1}`,
    );
  for (const field of fields)
    assert.ok(
      field.number < 19000 || field.number > 19999,
      `${messageName}.${field.name} must not use reserved field numbers`,
    );
  return fields;
}

const orders = parseProto("orders.proto");
assert.equal(orders.packageName, "teck.cloud.commerce.orders.v1");
assert.deepEqual(
  orders.services.map(({ name }) => name),
  ["Orders"],
  "orders.proto defines the Orders service",
);
assert.deepEqual(
  orders.services[0].rpcs.map(({ name }) => name),
  [
    "GetCatalog",
    "GetBasket",
    "PutBasket",
    "CreateOrder",
    "ListCustomerOrders",
    "GetCustomerOrder",
    "ListAdminOrders",
    "GetAdminOrder",
    "DecideOrder",
    "RecordPlacementEvidence",
    "CheckCurrentEntitlement",
  ],
);
const orderRpc = new Map(orders.services[0].rpcs.map((rpc) => [rpc.name, rpc]));
assert.equal(orderRpc.get("GetBasket").response, "Basket");
assert.equal(orderRpc.get("PutBasket").response, "Basket");
assert.equal(orderRpc.get("CreateOrder").response, "CreateOrderResponse");
assert.equal(orderRpc.get("CheckCurrentEntitlement").request, "CheckCurrentEntitlementRequest");
assert.equal(orderRpc.get("CheckCurrentEntitlement").response, "CheckCurrentEntitlementResponse");

for (const messageName of orders.messages.keys()) assertSequentialFields(orders, messageName);
const entitlementRequest = orders.messages.get("CheckCurrentEntitlementRequest");
assert.deepEqual(
  entitlementRequest.map(({ name, number }) => [name, number]),
  [
    ["tenant_id", 1],
    ["order_id", 2],
    ["subscription_id", 3],
    ["desired_revision", 4],
  ],
);
assert.deepEqual(
  orders.messages.get("CheckCurrentEntitlementResponse").map(({ name }) => name),
  ["eligible"],
);
for (const messageName of ["CustomerOrder", "AdminOrder", "License", "Money", "Basket"])
  assert.ok(orders.messages.has(messageName), `orders.proto defines ${messageName}`);
const adminOrderFields = orders.messages.get("AdminOrder").map(({ name }) => name);
assert.deepEqual(adminOrderFields.slice(9), ["payment", "approval", "fulfillment", "audit"]);
assert.equal(
  orders.messages.get("AdminOrder").some(({ name }) => name === "license"),
  false,
);
const customerOrderFields = orders.messages.get("CustomerOrder");
assert.deepEqual(customerOrderFields.slice(-3), [
  { label: null, type: "google.protobuf.Timestamp", name: "updated_at", number: 9 },
  { label: null, type: "License", name: "license", number: 10 },
  { label: "optional", type: "uint32", name: "desired_revision", number: 11 },
]);
for (const enumName of [
  "Tier",
  "OrderStatus",
  "PaymentStatus",
  "ApprovalStatus",
  "FulfillmentStatus",
  "FulfillmentFailureCode",
  "Decision",
  "AuditAction",
]) {
  const values = orders.enums.get(enumName);
  assert.ok(values, `orders.proto defines enum ${enumName}`);
  assert.equal(values[0].number, 0, `${enumName} starts at zero`);
  assert.match(values[0].name, /_UNSPECIFIED$/, `${enumName} first value is UNSPECIFIED`);
  assert.equal(new Set(values.map(({ number }) => number)).size, values.length);
}

const licensing = parseProto("licensing.proto");
assert.equal(licensing.packageName, "teck.cloud.commerce.licensing.v1");
assert.deepEqual(
  licensing.services.map(({ name }) => name),
  ["Licensing"],
);
assert.deepEqual(
  licensing.services[0].rpcs.map(({ name }) => name),
  ["GetCurrentLicense", "VerifyLicense"],
);
for (const messageName of licensing.messages.keys()) assertSequentialFields(licensing, messageName);
assert.deepEqual(
  licensing.messages.get("GetCurrentLicenseRequest").map(({ name, number }) => [name, number]),
  [
    ["tenant_id", 1],
    ["order_id", 2],
    ["subscription_id", 3],
    ["desired_revision", 4],
  ],
);
assert.deepEqual(
  licensing.messages.get("VerifyLicenseResponse").map(({ name, number }) => [name, number]),
  [
    ["valid", 1],
    ["claims", 2],
    ["rejection", 3],
  ],
);
const rejectionValues = licensing.enums.get("RejectionCode");
assert.deepEqual(
  rejectionValues.map(({ name }) => name),
  [
    "REJECTION_CODE_UNSPECIFIED",
    "REJECTION_CODE_INVALID_SIGNATURE",
    "REJECTION_CODE_EXPIRED",
    "REJECTION_CODE_REVOKED",
    "REJECTION_CODE_WRONG_BINDING",
    "REJECTION_CODE_UNKNOWN_KEY",
  ],
);
assert.equal(rejectionValues[0].number, 0);
const claimsFields = licensing.messages.get("LicenseClaims").map(({ name }) => name);
assert.deepEqual(claimsFields, [
  "tenant_id",
  "order_id",
  "subscription_id",
  "tier",
  "add_on_codes",
  "revision",
  "key_id",
  "expires_at",
]);

// Schema-only harness project.
const projectPath = path.join(root, "Commerce.Contract.Protos.csproj");
const project = fs.readFileSync(projectPath, "utf8");
assert.match(project, /<PackageReference Include="Grpc\.Tools" PrivateAssets="all" \/>/);
assert.match(project, /<PackageReference Include="Google\.Protobuf" \/>/);
assert.doesNotMatch(
  project,
  /<ProjectReference/,
  "schema harness has no runtime project references",
);
for (const proto of ["orders.proto", "licensing.proto"])
  assert.match(
    project,
    new RegExp(`<Protobuf Include="${proto}" GrpcServices="None" />`),
    `${proto} compiles without server/client generation`,
  );
assert.ok(
  fs.existsSync(path.join(root, "packages.lock.json")),
  "protobuf harness has a checked-in package lock",
);
assert.ok(fs.existsSync(path.join(root, "README.md")), "contract README exists");

console.log(
  `Validated ${operationsOf(customer).length + operationsOf(admin).length} operations, ${fixtureFiles.length} fixtures, 2 event schemas, ${orders.messages.size + licensing.messages.size} protobuf messages and the license verification matrix.`,
);
