using Microsoft.EntityFrameworkCore;

namespace SharedKernel.ArchitectureTests;

internal static class BadDbQuery
{
    public static int Handle(GoodQuery query, DbContext database)
    {
        _ = query;
        _ = database;
        return 1;
    }
}
