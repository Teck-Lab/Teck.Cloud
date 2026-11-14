import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import fs from "node:fs";
import http from "node:http";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import { setTimeout as delay } from "node:timers/promises";
import { fileURLToPath } from "node:url";

const binary = process.env.ALLOY_BINARY;
assert.ok(binary, "Set ALLOY_BINARY to an official Alloy executable (verified with 1.22.0).");
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const temporary = fs.mkdtempSync(path.join(os.tmpdir(), "teck-alloy-redaction-"));
const socket = net.createServer();
await new Promise((resolve) => socket.listen(0, "127.0.0.1", resolve));
const port = socket.address().port;
const adminSocket = net.createServer();
await new Promise((resolve) => adminSocket.listen(0, "127.0.0.1", resolve));
const adminPort = adminSocket.address().port;
await new Promise((resolve) => adminSocket.close(resolve));
await new Promise((resolve) => socket.close(resolve));
fs.copyFileSync(
  path.join(root, "docs/examples/observability/redaction.alloy"),
  path.join(temporary, "redaction.alloy"),
);
const received = [];
const receiver = http.createServer(async (request, response) => {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  received.push({ path: request.url, body: Buffer.concat(chunks).toString() });
  response.writeHead(200, { "Content-Type": "application/json" });
  response.end("{}");
});
await new Promise((resolve) => receiver.listen(0, "127.0.0.1", resolve));
const config = path.join(temporary, "test.alloy");
fs.writeFileSync(
  config,
  `
import.file "policy" { filename = "${path.join(temporary, "redaction.alloy").replaceAll("\\", "/")}" }
policy.teck_redaction "test" {
  logs = [otelcol.exporter.otlphttp.test.input]
  traces = [otelcol.exporter.otlphttp.test.input]
  metrics = [otelcol.exporter.otlphttp.test.input]
}
otelcol.receiver.otlp "test" {
  http { endpoint = "127.0.0.1:${port}" }
  output {
    logs = [policy.teck_redaction.test.input]
    traces = [policy.teck_redaction.test.input]
    metrics = [policy.teck_redaction.test.input]
  }
}
otelcol.exporter.otlphttp "test" {
  encoding = "json"
  client {
    endpoint = "http://127.0.0.1:${receiver.address().port}"
    compression = "none"
  }
}
`,
);
const validation = spawnSync(binary, ["validate", config], { encoding: "utf8", windowsHide: true });
if (validation.error || validation.status !== 0) {
  receiver.close();
  cleanup();
}
assert.ifError(validation.error);
assert.equal(validation.status, 0, validation.stderr);
const child = spawn(
  binary,
  [
    "run",
    "--disable-reporting",
    `--server.http.listen-addr=127.0.0.1:${adminPort}`,
    `--storage.path=${temporary}/data`,
    config,
  ],
  { windowsHide: true },
);
let output = "";
let launchError;
child.on("error", (error) => {
  launchError = error;
});
child.stdout.on("data", (data) => {
  output += data;
});
child.stderr.on("data", (data) => {
  output += data;
});
const exited = new Promise((resolve) => child.once("close", resolve));
const value = (key, text) => ({ key, value: { stringValue: text } });
const attributes = [
  value("Password", "secret-password-probe"),
  value("Contact", "secret-email@example.test"),
  value("Description", "Bearer secret-bearer-probe"),
  value("safe", "keep-safe"),
  {
    key: "Nested",
    value: {
      kvlistValue: {
        values: [value("Authorization", "secret-nested-probe"), value("safe", "keep-nested")],
      },
    },
  },
];
const resource = { attributes: [value("service.name", "keep-service"), ...attributes] };
const traceId = "0123456789abcdef0123456789abcdef";
const spanId = "0123456789abcdef";
const now = `${BigInt(Date.now()) * 1000000n}`;
const requests = {
  logs: {
    resourceLogs: [
      {
        resource,
        scopeLogs: [
          {
            logRecords: [
              {
                timeUnixNano: now,
                traceId,
                spanId,
                body: { stringValue: "keep-log secret-body@example.test Bearer secret-body-token" },
                attributes,
              },
              { timeUnixNano: now, body: { kvlistValue: { values: attributes } } },
            ],
          },
        ],
      },
    ],
  },
  traces: {
    resourceSpans: [
      {
        resource,
        scopeSpans: [
          {
            spans: [
              {
                traceId,
                spanId,
                name: "keep-span secret-name@example.test",
                startTimeUnixNano: now,
                endTimeUnixNano: `${BigInt(now) + 1000000n}`,
                attributes: [...attributes, value("pyroscope.profile.id", spanId)],
                status: { code: 2, message: "secret-status@example.test" },
                events: [{ timeUnixNano: now, name: "keep-event", attributes }],
              },
            ],
          },
        ],
      },
    ],
  },
  metrics: {
    resourceMetrics: [
      {
        resource,
        scopeMetrics: [
          {
            metrics: [
              {
                name: "keep-metric",
                gauge: { dataPoints: [{ timeUnixNano: now, asInt: "42", attributes }] },
              },
            ],
          },
        ],
      },
    ],
  },
};
try {
  for (let attempt = 0; ; attempt++) {
    assert.ifError(launchError);
    assert.equal(child.exitCode, null, `Alloy exited: ${output}`);
    try {
      const response = await fetch(`http://127.0.0.1:${port}/v1/logs`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: "{}",
        signal: AbortSignal.timeout(1000),
      });
      assert.ok(response.ok);
      break;
    } catch (error) {
      if (attempt >= 60) throw error;
      await delay(250);
    }
  }
  for (const [signal, body] of Object.entries(requests)) {
    const response = await fetch(`http://127.0.0.1:${port}/v1/${signal}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(5000),
    });
    assert.equal(response.status, 200, await response.text());
  }
  for (
    let attempt = 0;
    !received.some((request) => request.path === "/v1/metrics") && attempt < 40;
    attempt++
  )
    await delay(100);
  const logPayload = JSON.parse(
    received.find((request) => request.path === "/v1/logs" && request.body.includes("keep-log"))
      .body,
  );
  const log = logPayload.resourceLogs[0].scopeLogs[0].logRecords[0];
  assert.equal(log.traceId, traceId);
  assert.equal(log.spanId, spanId);
  const tracePayload = JSON.parse(received.find((request) => request.path === "/v1/traces").body);
  const span = tracePayload.resourceSpans[0].scopeSpans[0].spans[0];
  assert.equal(span.traceId, traceId);
  assert.equal(
    span.attributes.find((attribute) => attribute.key === "pyroscope.profile.id").value.stringValue,
    spanId,
  );
  const metricPayload = JSON.parse(received.find((request) => request.path === "/v1/metrics").body);
  assert.equal(
    metricPayload.resourceMetrics[0].scopeMetrics[0].metrics[0].gauge.dataPoints[0].asInt,
    "42",
  );
  const exported = received.map((request) => request.body).join("\n");
  for (const signal of ["logs", "traces", "metrics"])
    assert.ok(received.some((request) => request.path === `/v1/${signal}`));
  for (const marker of [
    "keep-service",
    "keep-log",
    "keep-span",
    "keep-event",
    "keep-metric",
    "keep-safe",
    "keep-nested",
    traceId,
    spanId,
    "pyroscope.profile.id",
    "[REDACTED]",
  ])
    assert.ok(exported.includes(marker), `Missing ${marker}`);
  assert.ok(!exported.includes("secret-"), "A synthetic secret reached the Alloy exporter.");
  console.log(
    "PASS: Alloy redacts logs, spans/events, metrics and resources; preserves correlation and profile IDs.",
  );
} finally {
  child.kill();
  await exited;
  await new Promise((resolve) => receiver.close(resolve));
  cleanup();
}

function cleanup() {
  const resolved = path.resolve(temporary);
  assert.equal(path.dirname(resolved), path.resolve(os.tmpdir()));
  assert.ok(path.basename(resolved).startsWith("teck-alloy-redaction-"));
  fs.rmSync(resolved, { recursive: true, force: true });
}
