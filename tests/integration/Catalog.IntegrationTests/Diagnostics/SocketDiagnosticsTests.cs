#pragma warning disable IDE0005
using System;
using System.IO;
using Xunit;

namespace Catalog.IntegrationTests.Diagnostics
{
    public class SocketDiagnosticsTests
    {
        [Fact]
        public void PrintSocketDiagnostics()
        {
            var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
            var overrideVar = Environment.GetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE");

            Console.WriteLine($"[SocketDiagnostics] DOCKER_HOST={dockerHost ?? "(not set)"}");
            Console.WriteLine($"[SocketDiagnostics] TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE={overrideVar ?? "(not set)"}");

            string[] possiblePaths = new[]
            {
                "/run/podman/podman.sock",
                "/var/run/docker.sock"
            };

            foreach (var path in possiblePaths)
            {
                Console.WriteLine($"[SocketDiagnostics] Exists {path}: {File.Exists(path)}");
            }

            // Always pass; this test is only for logging diagnostics when run explicitly
            Assert.True(true);
        }
    }
}
