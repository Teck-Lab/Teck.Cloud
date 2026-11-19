import assert from "node:assert/strict";
import test from "node:test";
import {
  summarizeAuditOperationalError,
  summarizeBunFindings,
  summarizeNugetFindings,
} from "./security-audit-summary.mjs";

test("Bun audit summary retains sanitized advisory identity and severity", () => {
  assert.deepEqual(
    summarizeBunFindings({
      advisories: {
        picomatch: {
          id: "GHSA-TEST",
          module_name: "picomatch",
          severity: "high",
          vulnerable_versions: "<=4.0.7",
          url: "secret",
        },
      },
    }),
    [{ package: "picomatch", id: "GHSA-TEST", severity: "HIGH", vulnerableVersions: "<=4.0.7" }],
  );
});

test("NuGet audit summary includes nested advisory details without raw report contents", () => {
  assert.deepEqual(
    summarizeNugetFindings({
      projects: [
        {
          frameworks: [
            {
              transitivePackages: [
                {
                  id: "Example.Package",
                  resolvedVersion: "1.2.3",
                  vulnerabilities: [
                    {
                      advisoryUrl: "https://example/GHSA",
                      severity: "High",
                      title: "Sensitive package path",
                    },
                  ],
                },
              ],
            },
          ],
        },
      ],
    }),
    [
      {
        package: "Example.Package",
        version: "1.2.3",
        id: "https://example/GHSA",
        severity: "HIGH",
      },
    ],
  );
});

test("process failures emit stable operational codes only", () => {
  assert.deepEqual(summarizeAuditOperationalError("bun", { code: "ENOENT", message: "secret" }), {
    ecosystem: "bun",
    operationalError: "ENOENT",
  });
});
