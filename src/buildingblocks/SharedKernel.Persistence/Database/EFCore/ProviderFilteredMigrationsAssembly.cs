using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedKernel.Persistence.Database.EFCore;

internal sealed class ProviderFilteredMigrationsAssembly : IMigrationsAssembly
{
    private readonly DbContext _currentContext;
    private readonly IMigrationsIdGenerator _migrationsIdGenerator;
    private readonly Assembly _assembly;
    private readonly string? _providerToken;

    private IReadOnlyDictionary<string, TypeInfo>? _migrations;
    private ModelSnapshot? _modelSnapshot;

    public ProviderFilteredMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator migrationsIdGenerator)
    {
        _currentContext = currentContext.Context;
        _migrationsIdGenerator = migrationsIdGenerator;

        RelationalOptionsExtension relationalOptions = options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Relational options are required for migrations.");

        string assemblyName = relationalOptions.MigrationsAssembly
            ?? _currentContext.GetType().Assembly.GetName().Name
            ?? throw new InvalidOperationException("Unable to resolve migrations assembly name.");

        _assembly = Assembly.Load(new AssemblyName(assemblyName));
        _providerToken = ResolveProviderToken(_currentContext.Database.ProviderName);
    }

    public IReadOnlyDictionary<string, TypeInfo> Migrations => _migrations ??= CreateMigrations();

    public ModelSnapshot? ModelSnapshot => _modelSnapshot ??= CreateModelSnapshot();

    public Assembly Assembly => _assembly;

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        _ = activeProvider;
        return (Migration)Activator.CreateInstance(migrationClass.AsType())!;
    }

    public string? FindMigrationId(string nameOrId)
    {
        if (string.IsNullOrWhiteSpace(nameOrId))
        {
            return null;
        }

        if (Migrations.ContainsKey(nameOrId))
        {
            return nameOrId;
        }

        foreach (string migrationId in Migrations.Keys)
        {
            string migrationName = _migrationsIdGenerator.GetName(migrationId);
            if (string.Equals(migrationName, nameOrId, StringComparison.OrdinalIgnoreCase))
            {
                return migrationId;
            }
        }

        return null;
    }

    private IReadOnlyDictionary<string, TypeInfo> CreateMigrations()
    {
        Type contextType = _currentContext.GetType();
        Dictionary<string, TypeInfo> migrations = [];

        foreach (TypeInfo typeInfo in _assembly.DefinedTypes)
        {
            if (!typeInfo.IsClass || typeInfo.IsAbstract || !typeof(Migration).IsAssignableFrom(typeInfo.AsType()))
            {
                continue;
            }

            if (!MatchesDbContext(typeInfo, contextType) || !MatchesProvider(typeInfo))
            {
                continue;
            }

            MigrationAttribute? migrationAttribute = typeInfo.GetCustomAttribute<MigrationAttribute>();
            if (migrationAttribute is null || string.IsNullOrWhiteSpace(migrationAttribute.Id))
            {
                continue;
            }

            migrations[migrationAttribute.Id] = typeInfo;
        }

        return migrations;
    }

    private ModelSnapshot? CreateModelSnapshot()
    {
        Type contextType = _currentContext.GetType();

        foreach (TypeInfo typeInfo in _assembly.DefinedTypes)
        {
            if (!typeInfo.IsClass || typeInfo.IsAbstract || !typeof(ModelSnapshot).IsAssignableFrom(typeInfo.AsType()))
            {
                continue;
            }

            if (!MatchesDbContext(typeInfo, contextType) || !MatchesProvider(typeInfo))
            {
                continue;
            }

            return (ModelSnapshot?)Activator.CreateInstance(typeInfo.AsType());
        }

        return null;
    }

    private bool MatchesProvider(MemberInfo typeInfo)
    {
        if (string.IsNullOrWhiteSpace(_providerToken))
        {
            return true;
        }

        string? fullName = typeInfo.DeclaringType?.FullName ?? typeInfo.ReflectedType?.FullName ?? typeInfo.Name;
        return fullName.Contains($".Migrations.{_providerToken}.", StringComparison.OrdinalIgnoreCase)
            || fullName.EndsWith($".Migrations.{_providerToken}", StringComparison.OrdinalIgnoreCase)
            || fullName.Contains(_providerToken, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDbContext(MemberInfo typeInfo, Type contextType)
    {
        DbContextAttribute? dbContextAttribute = typeInfo.GetCustomAttribute<DbContextAttribute>();
        return dbContextAttribute is not null && dbContextAttribute.ContextType == contextType;
    }

    private static string? ResolveProviderToken(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return "PostgreSQL";
        }

        if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return "SqlServer";
        }

        if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase) ||
            providerName.Contains("Pomelo", StringComparison.OrdinalIgnoreCase))
        {
            return "MySql";
        }

        return null;
    }
}
