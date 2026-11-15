import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const layerNames = ["Domain", "Application", "Infrastructure", "Host"];
const workerLayerNames = ["Application", "Infrastructure", "Host"];
const ignoredDirectories = new Set([".git", ".nx", "bin", "obj", "node_modules"]);
const technicalDomainFolders = new Set([
  "entities",
  "valueobjects",
  "domainevents",
  "errors",
  "services",
]);
const applicationSupportFolders = new Set(["Responses", "ReadModels", "Mapping", "EventHandlers"]);
const applicationUseCaseFolders = new Set(["Commands", "Queries", "Handlers", "Validators"]);
const applicationRootFiles = new Set([
  "DependencyInjection.cs",
  "ApplicationServiceCollectionExtensions.cs",
  "ServiceCollectionExtensions.cs",
  "ApplicationComposition.cs",
  "GlobalUsings.cs",
  "AssemblyInfo.cs",
]);
const useCaseTypeSuffix = /(?:Command|Query|Handler|Validator)$/iu;
const featureVersion = /^V[1-9]\d*$/u;

function walkFiles(directory) {
  if (!fs.existsSync(directory)) return [];

  const files = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!ignoredDirectories.has(entry.name)) {
        files.push(...walkFiles(path.join(directory, entry.name)));
      }
    } else if (entry.isFile()) {
      files.push(path.join(directory, entry.name));
    }
  }

  return files;
}

function normalizeRelative(value) {
  return value.split(path.sep).join("/");
}

function relativeRepositoryPath(repositoryRoot, file) {
  return normalizeRelative(path.relative(repositoryRoot, file));
}

function stripCSharpCommentsAndStrings(source) {
  return source
    .replace(/@"(?:""|[^"])*"/gu, '""')
    .replace(/"(?:\\.|[^"\\])*"/gu, '""')
    .replace(/'(?:\\.|[^'\\])*'/gu, "''")
    .replace(/\/\*[\s\S]*?\*\//gu, " ")
    .replace(/\/\/.*$/gmu, " ");
}

function isApplicationUseCaseSource(file) {
  const fileName = path.basename(file, ".cs");
  if (useCaseTypeSuffix.test(fileName)) return true;

  const source = stripCSharpCommentsAndStrings(fs.readFileSync(file, "utf8"));
  const namedUseCaseType =
    /\b(?:class|record|struct|interface)\s+@?[A-Za-z_]\w*(?:Command|Query|Handler|Validator)\b/iu;
  const commandOrQueryContract =
    /\b(?:class|record|struct)\s+@?[A-Za-z_]\w*(?:\s*<[^>{}]+>)?\s*:\s*[^;{}]*\bI(?:Command|Query)\b/iu;

  return namedUseCaseType.test(source) || commandOrQueryContract.test(source);
}

function readProjectReferences(repositoryRoot, projectFile) {
  const content = fs.readFileSync(projectFile, "utf8");
  const references = new Set();
  const matcher = /<ProjectReference\b[^>]*\bInclude\s*=\s*(?:"([^"]+)"|'([^']+)')[^>]*>/giu;

  for (const match of content.matchAll(matcher)) {
    const include = match[1] ?? match[2];
    const normalizedInclude = include.replace(/[\\/]+/gu, path.sep);
    references.add(
      relativeRepositoryPath(
        repositoryRoot,
        path.resolve(path.dirname(projectFile), normalizedInclude),
      ),
    );
  }

  return { content, references };
}

function validateDomainSource(repositoryRoot, serviceKey, file, segments, errors) {
  const domainFolders = segments.slice(3, -1);
  const firstFolder = domainFolders[0];
  const filePath = relativeRepositoryPath(repositoryRoot, file);

  if (technicalDomainFolders.has((firstFolder ?? "").toLowerCase())) {
    errors.push(
      filePath +
        " is in a top-level technical Domain/" +
        firstFolder +
        " folder; put aggregate-owned types under Aggregates/<Aggregate>/ or shared concepts under Common/",
    );
    return;
  }

  if (firstFolder === "Common" && domainFolders.length >= 1) return;
  if (firstFolder === "Aggregates" && domainFolders.length >= 2 && domainFolders[1]) return;

  errors.push(
    filePath +
      " must be under Domain/Aggregates/<Aggregate>/ or Domain/Common/ for service " +
      serviceKey,
  );
}

function validateMapperSource(repositoryRoot, serviceKey, file, source, errors) {
  const mapperTypeDeclaration =
    /\b(?:class|interface|struct|record(?:\s+(?:class|struct))?)\s+@?([A-Za-z_]\w*Mapper)\b/gu;
  const mapperAttribute =
    /\[\s*(?:global::)?(?:[\w.]+::)?(?:[\w.]+\.)?Mapper(?:Attribute)?\s*(?:\(|\])/u;

  for (const match of source.matchAll(mapperTypeDeclaration)) {
    const prefix = source.slice(0, match.index);
    const boundary = Math.max(
      prefix.lastIndexOf("{"),
      prefix.lastIndexOf("}"),
      prefix.lastIndexOf(";"),
    );
    const declarationPrefix = prefix
      .slice(boundary + 1)
      .replace(/\/\*[\s\S]*?\*\//gu, "")
      .replace(/\/\/.*$/gmu, "");
    if (!mapperAttribute.test(declarationPrefix)) {
      errors.push(
        relativeRepositoryPath(repositoryRoot, file) +
          " declares " +
          match[1] +
          " in service " +
          serviceKey +
          " without a Mapperly [Mapper] attribute",
      );
    }
  }
}
function isVersionedFeaturePath(applicationFolders) {
  return (
    applicationFolders.length >= 4 &&
    applicationFolders[0] &&
    applicationFolders[0] !== "Features" &&
    applicationFolders[1] === "Features" &&
    applicationFolders[2] &&
    featureVersion.test(applicationFolders[3])
  );
}

function validateApplicationSource(repositoryRoot, serviceKey, file, segments, errors) {
  const applicationFolders = segments.slice(3, -1);
  const filePath = relativeRepositoryPath(repositoryRoot, file);
  const hasFeatureSegment = applicationFolders.includes("Features");
  const isFeaturePath = isVersionedFeaturePath(applicationFolders);
  const useCaseSource = isApplicationUseCaseSource(file);

  if (hasFeatureSegment) {
    if (!isFeaturePath) {
      errors.push(
        filePath +
          " must be under Application/<Capability>/Features/<UseCase>/V<n>/ for service " +
          serviceKey,
      );
    }
    return;
  }

  const capabilityFolder = applicationFolders[0];
  const supportingFolder = applicationFolders[1];

  if (applicationFolders.length === 1 && applicationSupportFolders.has(capabilityFolder)) {
    errors.push(`${filePath} must be under a capability before Application/${capabilityFolder}/`);
    return;
  }

  if (applicationFolders.length >= 2 && applicationSupportFolders.has(supportingFolder)) {
    if (supportingFolder === "EventHandlers" || !useCaseSource) return;
  }

  if (applicationFolders.length >= 2 && applicationUseCaseFolders.has(supportingFolder)) {
    errors.push(
      filePath +
        " is outside a vertical slice; put use-case code under Application/<Capability>/Features/<UseCase>/V<n>/",
    );
    return;
  }

  if (useCaseSource) {
    errors.push(
      filePath +
        " contains a command, query, handler, or validator outside Application/<Capability>/Features/<UseCase>/V<n>/",
    );
    return;
  }

  if (applicationFolders.length === 0 && !applicationRootFiles.has(path.basename(file))) {
    errors.push(
      filePath +
        " is not an allowed Application root composition file; place it under a capability or use an approved composition filename",
    );
  }
}

function validateServiceLayout(repositoryRoot) {
  const servicesDirectory = path.join(repositoryRoot, "src", "services");
  if (!fs.existsSync(servicesDirectory)) return [];

  const serviceFiles = walkFiles(servicesDirectory).filter((file) => {
    const extension = path.extname(file).toLowerCase();
    return (
      extension === ".cs" ||
      extension === ".csproj" ||
      path.basename(file) === "service-layout.json"
    );
  });
  const errors = [];
  const services = new Map();

  for (const file of serviceFiles) {
    const relativePath = path.relative(servicesDirectory, file);
    const segments = relativePath.split(path.sep);

    // Gateways are routing/auth processes and do not own business service layers.
    if (segments[0]?.toLowerCase() === "gateway") continue;

    if (segments.length < 3) {
      errors.push(
        relativeRepositoryPath(repositoryRoot, file) +
          " must live under src/services/<domain>/<service>/<Service>.<Layer>/",
      );
      continue;
    }

    const serviceKey = segments.slice(0, 2).join("/");
    if (!services.has(serviceKey)) {
      services.set(serviceKey, {
        files: [],
        projects: new Map(),
        sources: new Map(),
        prefixes: new Set(),
      });
    }

    services.get(serviceKey).files.push({ file, segments });
  }

  const rulesProject = path.join(
    repositoryRoot,
    "tests",
    "architecture",
    "Teck.Cloud.ArchitectureRules",
    "Teck.Cloud.ArchitectureRules.csproj",
  );

  for (const [serviceKey, service] of [...services.entries()].sort(([left], [right]) =>
    left.localeCompare(right),
  )) {
    const markerFiles = service.files.filter(
      (entry) => path.basename(entry.file) === "service-layout.json",
    );
    service.kind = "stateful-business";

    if (markerFiles.length > 0) {
      if (markerFiles.length > 1 || markerFiles[0].segments.length !== 3) {
        errors.push(
          "Service src/services/" +
            serviceKey +
            " must keep service-layout.json at its service root",
        );
      }

      try {
        const marker = JSON.parse(fs.readFileSync(markerFiles[0].file, "utf8"));
        if (
          marker &&
          typeof marker === "object" &&
          !Array.isArray(marker) &&
          marker.kind === "stateless-worker" &&
          Object.keys(marker).length === 1
        ) {
          service.kind = "stateless-worker";
        } else {
          errors.push(
            "Service src/services/" +
              serviceKey +
              ' has an unknown or unsupported service-layout.json shape; expected exactly {"kind":"stateless-worker"}',
          );
        }
      } catch {
        errors.push(
          "Service src/services/" +
            serviceKey +
            ' has malformed service-layout.json; expected {"kind":"stateless-worker"}',
        );
      }
    }

    for (const entry of service.files) {
      const { file, segments } = entry;
      if (path.basename(file) === "service-layout.json") continue;

      const projectDirectory = segments[2];
      const layerMatch = /^(.+)\.(Domain|Application|Infrastructure|Host)$/u.exec(projectDirectory);

      if (!layerMatch) {
        errors.push(
          relativeRepositoryPath(repositoryRoot, file) +
            " is outside a <Service>.Domain, <Service>.Application, <Service>.Infrastructure, or <Service>.Host project",
        );
        continue;
      }

      const projectName = layerMatch[1];
      const layerName = layerMatch[2];
      service.prefixes.add(projectName);

      if (path.extname(file).toLowerCase() === ".csproj") {
        if (segments.length !== 4) {
          errors.push(
            relativeRepositoryPath(repositoryRoot, file) +
              " must be directly inside its layer project directory",
          );
          continue;
        }

        if (path.basename(file) !== `${projectDirectory}.csproj`) {
          errors.push(
            relativeRepositoryPath(repositoryRoot, file) +
              " must be named " +
              projectDirectory +
              ".csproj",
          );
          continue;
        }

        if (!service.projects.has(layerName)) service.projects.set(layerName, []);
        service.projects.get(layerName).push(file);
      } else {
        if (segments.length < 4) {
          errors.push(
            relativeRepositoryPath(repositoryRoot, file) +
              " must be inside its layer project directory",
          );
          continue;
        }

        if (!service.sources.has(layerName)) service.sources.set(layerName, []);
        service.sources.get(layerName).push({ file, segments });

        if (layerName === "Application" || layerName === "Host") {
          validateMapperSource(
            repositoryRoot,
            serviceKey,
            file,
            fs.readFileSync(file, "utf8"),
            errors,
          );
        }

        if (layerName === "Domain") {
          validateDomainSource(repositoryRoot, serviceKey, file, segments, errors);
        } else if (layerName === "Application") {
          validateApplicationSource(repositoryRoot, serviceKey, file, segments, errors);
        }
      }
    }

    const requiredLayerNames =
      service.kind === "stateless-worker" &&
      !service.projects.has("Domain") &&
      !service.sources.has("Domain")
        ? workerLayerNames
        : layerNames;

    if (service.prefixes.size !== 1) {
      errors.push(
        "Service src/services/" +
          serviceKey +
          " must use one consistent <Service> project prefix across its required layers",
      );
    }

    for (const layerName of requiredLayerNames) {
      const projects = service.projects.get(layerName) ?? [];
      const sources = service.sources.get(layerName) ?? [];

      if (projects.length !== 1) {
        errors.push(
          "Service src/services/" +
            serviceKey +
            " must contain exactly one <Service>." +
            layerName +
            "/" +
            "<Service>." +
            layerName +
            ".csproj project",
        );
      }

      if (sources.length === 0) {
        errors.push(
          "Service src/services/" +
            serviceKey +
            " has an empty " +
            layerName +
            " project; add authored C# source to each layer",
        );
      }
    }

    if (service.kind === "stateless-worker" || service.prefixes.size !== 1) continue;

    const [serviceName] = service.prefixes;
    const architectureProject = path.join(
      repositoryRoot,
      "tests",
      "architecture",
      `${serviceName}.Architecture.UnitTests`,
      `${serviceName}.Architecture.UnitTests.csproj`,
    );

    if (!fs.existsSync(architectureProject)) {
      errors.push(
        "Service " +
          serviceName +
          " requires its suite at tests/architecture/" +
          serviceName +
          ".Architecture.UnitTests/" +
          serviceName +
          ".Architecture.UnitTests.csproj",
      );
      continue;
    }

    const architectureSources = walkFiles(path.dirname(architectureProject)).filter(
      (file) => path.extname(file).toLowerCase() === ".cs",
    );
    if (architectureSources.length === 0) {
      errors.push(
        relativeRepositoryPath(repositoryRoot, architectureProject) +
          " must contain authored C# architecture tests",
      );
    }

    const hasSharedArchitectureAssertion = architectureSources.some((file) => {
      const source = stripCSharpCommentsAndStrings(fs.readFileSync(file, "utf8"));
      return /\bSharedArchitectureRules\s*\.\s*AssertAll\s*\(/u.test(source);
    });
    if (!hasSharedArchitectureAssertion) {
      errors.push(
        relativeRepositoryPath(repositoryRoot, architectureProject) +
          " must call SharedArchitectureRules.AssertAll(...)",
      );
    }

    const { content, references } = readProjectReferences(repositoryRoot, architectureProject);
    if (!/<IsTestProject>\s*true\s*<\/IsTestProject>/iu.test(content)) {
      errors.push(
        relativeRepositoryPath(repositoryRoot, architectureProject) +
          " must set IsTestProject to true",
      );
    }

    const requiredReferences = [
      ...layerNames.flatMap((layerName) => service.projects.get(layerName) ?? []),
      rulesProject,
    ];

    for (const requiredProject of requiredReferences) {
      const expectedReference = relativeRepositoryPath(repositoryRoot, requiredProject);
      if (!references.has(expectedReference)) {
        errors.push(
          relativeRepositoryPath(repositoryRoot, architectureProject) +
            " must ProjectReference " +
            expectedReference,
        );
      }
    }
  }

  return errors;
}

function main() {
  const errors = validateServiceLayout(process.cwd());
  if (errors.length > 0) {
    console.error("Service layout validation failed:");
    for (const error of errors) console.error(`  - ${error}`);
    process.exitCode = 1;
    return;
  }

  console.log("Service layout validation passed.");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}

export { validateServiceLayout };
