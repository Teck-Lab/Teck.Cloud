# Teck.Cloud

A .NET solution following modern best practices and coding standards.

## Project Structure

```
Teck.Cloud/
├── .config/
│   └── dotnet-tools.json          # Local dotnet tool manifest
├── src/
│   └── Teck.Cloud.Core/            # Core library project
│       ├── Models/                 # Domain models and result types
│       └── ValueObjects/           # Value objects (e.g., EntityId)
├── tests/
│   └── Teck.Cloud.Core.Tests/      # Unit tests for Core library
│       ├── Models/                 # Tests for models
│       └── ValueObjects/           # Tests for value objects
├── Directory.Build.props           # Shared build properties
├── Directory.Packages.props        # Centralized package management
├── global.json                     # SDK version control
├── nuget.config                    # NuGet package source configuration
└── Teck.Cloud.sln                  # Solution file
```

## Configuration Files

### Solution-Level Configuration

- **`global.json`**: Locks the .NET SDK version to ensure consistent builds across environments
- **`Directory.Build.props`**: Defines shared metadata, code quality settings, and build properties for all projects
- **`Directory.Packages.props`**: Centralizes NuGet package version management across all projects
- **`nuget.config`**: Configures package sources and package source mapping

### Tool Manifest

- **`.config/dotnet-tools.json`**: Manages local dotnet tool dependencies for reproducible builds

## Key Features

### Code Quality
- ✅ Nullable reference types enabled
- ✅ Warnings treated as errors
- ✅ Deterministic builds
- ✅ Centralized package management

### Testing
- ✅ xUnit test framework
- ✅ Code coverage with Coverlet
- ✅ Test isolation and proper fixtures
- ✅ 100% code coverage target

### Coding Standards
- ✅ Records for immutable data types
- ✅ Value objects to avoid primitive obsession
- ✅ Result types for explicit error handling
- ✅ Functional programming patterns where appropriate
- ✅ Sealed classes by default

## Getting Started

### Prerequisites
- .NET SDK 10.0.100 or later
- Visual Studio 2022 or VS Code with C# extension

### Building

```bash
dotnet restore
dotnet build
```

### Testing

```bash
dotnet test
```

### Running Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Adding New Projects

1. Create the project:
   ```bash
   dotnet new classlib -n YourProject -o src/YourProject
   ```

2. Add to solution:
   ```bash
   dotnet sln add src/YourProject/YourProject.csproj
   ```

3. Create corresponding test project:
   ```bash
   dotnet new xunit -n YourProject.Tests -o tests/YourProject.Tests
   dotnet sln add tests/YourProject.Tests/YourProject.Tests.csproj
   dotnet add tests/YourProject.Tests/YourProject.Tests.csproj reference src/YourProject/YourProject.csproj
   ```

## Adding NuGet Packages

1. Add package version to `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="PackageName" Version="1.0.0" />
   ```

2. Add package reference to your project (without version):
   ```xml
   <PackageReference Include="PackageName" />
   ```

3. Restore packages:
   ```bash
   dotnet restore
   ```

## Development Guidelines

See the `.cursor/rules/` directory for detailed coding standards and best practices:
- **C# Coding Style**: Functional patterns, immutable data, value objects
- **Testing Guidelines**: xUnit patterns, test isolation, TestContainers for integration tests
- **Solution Management**: SDK versioning, build properties, package management
- **Dependency Management**: Security scanning, license compliance, version management