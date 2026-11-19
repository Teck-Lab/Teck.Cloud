export function summarizeNugetFindings(report) {
  const packages = (report?.projects ?? []).flatMap((project) =>
    (project.frameworks ?? []).flatMap((framework) => [
      ...(framework.topLevelPackages ?? []),
      ...(framework.transitivePackages ?? []),
    ]),
  );
  return packages.flatMap((pkg) =>
    (pkg.vulnerabilities ?? []).map((finding) => ({
      package: String(pkg.id ?? "unknown"),
      version: String(pkg.resolvedVersion ?? pkg.requestedVersion ?? "unknown"),
      id: String(finding.advisoryUrl ?? finding.id ?? "unknown"),
      severity: String(finding.severity ?? "unknown").toUpperCase(),
    })),
  );
}

export function summarizeBunFindings(report) {
  const advisories = Array.isArray(report?.advisories)
    ? report.advisories
    : report?.advisories && typeof report.advisories === "object"
      ? Object.values(report.advisories)
      : [];
  return advisories.map((finding) => ({
    package: String(finding.module_name ?? "unknown"),
    id: String(finding.id ?? "unknown"),
    severity: String(finding.severity ?? "unknown").toUpperCase(),
    vulnerableVersions: String(finding.vulnerable_versions ?? "unknown"),
  }));
}

export function summarizeAuditOperationalError(ecosystem, error) {
  return {
    ecosystem,
    operationalError: String(error?.code ?? error?.name ?? "audit-process-error"),
  };
}
