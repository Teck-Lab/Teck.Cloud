import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import { validateServiceLayout } from "./verify-service-layout.mjs";

const serviceProjectPaths = [
  "src/services/orders/order/Order.Domain/Order.Domain.csproj",
  "src/services/orders/order/Order.Application/Order.Application.csproj",
  "src/services/orders/order/Order.Infrastructure/Order.Infrastructure.csproj",
  "src/services/orders/order/Order.Host/Order.Host.csproj",
];

function createWorkspace({ omitLayer, omitArchitectureSuite = false, missingReference } = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "teck-service-layout-"));
  const write = (relativePath, content) => {
    const file = path.join(root, relativePath);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, content);
  };

  const projects = serviceProjectPaths.filter(
    (project) => !omitLayer || !project.includes(`Order.${omitLayer}/`),
  );

  for (const project of projects) {
    write(project, '<Project Sdk="Microsoft.NET.Sdk" />');
    const layerDirectory = path.dirname(project);
    const layerName = path.basename(layerDirectory).split(".").at(-1);
    const sourcePath =
      layerName === "Domain"
        ? path.join(layerDirectory, "Aggregates", "Orders", "Order.cs")
        : layerName === "Application"
          ? path.join(
              layerDirectory,
              "Sales",
              "Features",
              "CreateOrder",
              "V1",
              "CreateOrderHandler.cs",
            )
          : path.join(
              layerDirectory,
              layerName === "Host" ? "Program.cs" : "Database/OrderStore.cs",
            );
    const source =
      layerName === "Application"
        ? "namespace Fixture;\ninternal sealed class CreateOrderHandler;\n"
        : "namespace Fixture;\ninternal sealed class OrderSource;\n";
    write(sourcePath, source);
  }

  write(
    "src/services/orders/order/Order.Domain/Common/Currency.cs",
    "namespace Fixture;\ninternal sealed class Currency;\n",
  );
  write(
    "src/services/orders/order/Order.Application/Sales/Responses/CreateOrderResponse.cs",
    "namespace Fixture;\ninternal sealed class CreateOrderResponse;\n",
  );
  write(
    "src/services/orders/order/Order.Application/Sales/ReadModels/OrderReadModel.cs",
    "namespace Fixture;\ninternal sealed class OrderReadModel;\n",
  );
  write(
    "src/services/orders/order/Order.Application/Sales/Mapping/OrderMapper.cs",
    "using Riok.Mapperly.Abstractions;\nnamespace Fixture;\n[Mapper]\ninternal sealed class OrderMapper;\n",
  );
  write(
    "src/services/orders/order/Order.Application/Sales/EventHandlers/DomainEvents/OrderCreatedHandler.cs",
    "namespace Fixture;\ninternal sealed class OrderCreatedHandler;\n",
  );
  write(
    "src/services/orders/order/Order.Application/DependencyInjection.cs",
    "namespace Fixture;\ninternal static class DependencyInjection;\n",
  );

  write(
    "tests/architecture/Teck.Cloud.ArchitectureRules/Teck.Cloud.ArchitectureRules.csproj",
    '<Project Sdk="Microsoft.NET.Sdk" />',
  );

  if (!omitArchitectureSuite) {
    const suite =
      "tests/architecture/Order.Architecture.UnitTests/Order.Architecture.UnitTests.csproj";
    const suiteDirectory = path.dirname(path.join(root, suite));
    const references = [
      ...projects.filter(
        (project) => !missingReference || !project.includes(`Order.${missingReference}/`),
      ),
      "tests/architecture/Teck.Cloud.ArchitectureRules/Teck.Cloud.ArchitectureRules.csproj",
    ];
    const referenceXml = references
      .map((project) => {
        const include = path
          .relative(suiteDirectory, path.join(root, project))
          .split(path.sep)
          .join("/");
        return `<ProjectReference Include="${include}" />`;
      })
      .join("\n");

    write(
      suite,
      '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup>' +
        referenceXml +
        "</ItemGroup></Project>",
    );
    write(
      path.join(path.dirname(suite), "OrderArchitectureTests.cs"),
      [
        "using Teck.Cloud.ArchitectureRules;",
        "namespace Fixture;",
        "internal sealed class OrderArchitectureTests",
        "{",
        "    public void RulesAreInvoked()",
        "    {",
        '        SharedArchitectureRules.AssertAll(architecture, domainAssembly, applicationAssembly, infrastructureAssembly, hostAssembly, "Orders.Domain", "Orders.Application");',
        "    }",
        "}",
      ].join("\n"),
    );
  }

  return { root, write };
}

function createWorkerWorkspace({ omitLayer } = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "teck-service-layout-worker-"));
  const write = (relativePath, content) => {
    const file = path.join(root, relativePath);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, content);
  };

  write("src/services/operations/dispatch/service-layout.json", '{"kind":"stateless-worker"}');
  for (const layer of ["Application", "Infrastructure", "Host"]) {
    if (layer === omitLayer) continue;
    const project = `src/services/operations/dispatch/Dispatch.${layer}/Dispatch.${layer}.csproj`;
    write(project, '<Project Sdk="Microsoft.NET.Sdk" />');
    const sourcePath =
      layer === "Application"
        ? "src/services/operations/dispatch/Dispatch.Application/Dispatch/Features/PollQueue/V1/PollQueueHandler.cs"
        : path.join(path.dirname(project), layer === "Host" ? "Program.cs" : "QueueStore.cs");
    write(sourcePath, "namespace Fixture;\ninternal sealed class DispatchSource;\n");
  }

  return root;
}

test("empty reserved service folders do not require manufactured projects", (context) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "teck-service-layout-empty-"));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.mkdirSync(path.join(root, "src", "services", "future", "service"), { recursive: true });

  assert.deepEqual(validateServiceLayout(root), []);
});

test("valid stateful service layout and application support folders pass", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));

  assert.deepEqual(validateServiceLayout(workspace.root), []);
});

test("mapper types in application and host require Mapperly", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/Order.Application/Sales/Mapping/OrderMapper.cs",
    "namespace Fixture;\ninternal sealed class OrderMapper;\n",
  );
  workspace.write(
    "src/services/orders/order/Order.Host/Mapping/OrderRequestMapper.cs",
    "namespace Fixture;\ninternal sealed class OrderRequestMapper;\n",
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(
    errors.some((error) => error.includes("OrderMapper") && error.includes("Mapperly [Mapper]")),
  );
  assert.ok(
    errors.some(
      (error) => error.includes("OrderRequestMapper") && error.includes("Mapperly [Mapper]"),
    ),
  );
});
test("service missing a layer project is rejected", (context) => {
  const workspace = createWorkspace({ omitLayer: "Infrastructure" });
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("Infrastructure")));
});

test("misplaced service source is rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/Order.cs",
    "namespace Fixture;\ninternal sealed class Order;\n",
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("Order.cs") && error.includes("outside")));
});

test("technical top-level Domain folders are rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/Order.Domain/ValueObjects/OrderStatus.cs",
    "namespace Fixture;\ninternal sealed class OrderStatus;\n",
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("top-level technical Domain/ValueObjects")));
});

test("Domain source outside an aggregate or Common area is rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/Order.Domain/Orders/Order.cs",
    "namespace Fixture;\ninternal sealed class Order;\n",
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("Domain/Aggregates/<Aggregate>/")));
});

test("command, query, handler, or validator outside a versioned feature is rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/Order.Application/Sales/Handlers/CreateOrderHandler.cs",
    "namespace Fixture;\ninternal sealed class CreateOrderHandler;\n",
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("outside a vertical slice")));
});

test("feature files without an explicit version are rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/Order.Application/Sales/Features/CancelOrder/CancelOrderCommand.cs",
    "namespace Fixture;\ninternal sealed class CancelOrderCommand;\n",
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("Features/<UseCase>/V<n>")));
});

test("Application root only permits named composition files", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/Order.Application/Helper.cs",
    "namespace Fixture;\ninternal sealed class Helper;\n",
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(
    errors.some((error) => error.includes("not an allowed Application root composition file")),
  );
});

test("service without its architecture test suite is rejected", (context) => {
  const workspace = createWorkspace({ omitArchitectureSuite: true });
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("Order.Architecture.UnitTests")));
});

test("architecture suite missing a layer project reference is rejected", (context) => {
  const workspace = createWorkspace({ missingReference: "Application" });
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("Order.Application.csproj")));
});

test("architecture suite without authored tests is rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  fs.rmSync(
    path.join(
      workspace.root,
      "tests/architecture/Order.Architecture.UnitTests/OrderArchitectureTests.cs",
    ),
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("authored C# architecture tests")));
});

test("architecture suite without a real SharedArchitectureRules call is rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "tests/architecture/Order.Architecture.UnitTests/OrderArchitectureTests.cs",
    [
      "// SharedArchitectureRules.AssertAll(architecture, domainAssembly, applicationAssembly, infrastructureAssembly, hostAssembly, domainNamespace, applicationNamespace);",
      "namespace Fixture;",
      "internal sealed class OrderArchitectureTests;",
    ].join("\n"),
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("must call SharedArchitectureRules.AssertAll")));
});

test("marked stateless worker may omit Domain but requires Application, Infrastructure, and Host", (context) => {
  const root = createWorkerWorkspace();
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));

  assert.deepEqual(validateServiceLayout(root), []);
});

test("marked stateless worker missing Infrastructure is rejected", (context) => {
  const root = createWorkerWorkspace({ omitLayer: "Infrastructure" });
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));

  const errors = validateServiceLayout(root);
  assert.ok(errors.some((error) => error.includes("Infrastructure")));
});

test("malformed service layout marker is rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write("src/services/orders/order/service-layout.json", "{not-json");

  const errors = validateServiceLayout(workspace.root);
  assert.ok(errors.some((error) => error.includes("malformed service-layout.json")));
});

test("unknown service layout marker kind is rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write("src/services/orders/order/service-layout.json", '{"kind":"worker"}');

  const errors = validateServiceLayout(workspace.root);
  assert.ok(
    errors.some((error) => error.includes("unknown or unsupported service-layout.json shape")),
  );
});

test("extra service layout marker fields are rejected", (context) => {
  const workspace = createWorkspace();
  context.after(() => fs.rmSync(workspace.root, { recursive: true, force: true }));
  workspace.write(
    "src/services/orders/order/service-layout.json",
    '{"kind":"stateless-worker","reason":"avoid domain"}',
  );

  const errors = validateServiceLayout(workspace.root);
  assert.ok(
    errors.some((error) => error.includes("unknown or unsupported service-layout.json shape")),
  );
});

test("gateway projects remain outside the business-service four-layer rule", (context) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "teck-service-layout-gateway-"));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const project = path.join(
    root,
    "src",
    "services",
    "gateway",
    "customer",
    "Customer.Gateway.csproj",
  );
  fs.mkdirSync(path.dirname(project), { recursive: true });
  fs.writeFileSync(project, '<Project Sdk="Microsoft.NET.Sdk.Web" />');

  assert.deepEqual(validateServiceLayout(root), []);
});
