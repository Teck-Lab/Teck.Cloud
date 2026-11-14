# Query specifications

Use Ardalis.Specification directly. SharedKernel supplies guarded repository
execution; each service owns the query definitions alongside its Application
vertical slices (for example, Features/Orders/GetOrder and Features/Orders/CancelOrder).
Do not move service entities, DTOs or business filters into SharedKernel or create a
second specification DSL. Query specifications shape database access; domain methods
still enforce business invariants.

## Choose the operation deliberately

| Need                                             | Repository operation                                                                            |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------- |
| Read one unique entity or DTO                    | SingleOrDefaultAsync with SingleResultSpecification<T> or SingleResultSpecification<T, TResult> |
| Read the first ordered match                     | FirstOrDefaultAsync with an explicitly ordered specification                                    |
| List entities or DTOs                            | ListAsync with Specification<T> or Specification<T, TResult>                                    |
| Count matching source entities / check existence | CountAsync / AnyAsync, using criteria without paging                                            |
| Change one aggregate and its children            | Write repository LoadForUpdateAsync with SingleResultSpecification<T>                           |

Single-result methods return no match or one match and throw if multiple rows match.
Skip and Take are rejected on these methods, including Take(1), because they would
conceal duplicates. This detects ambiguous queries; database unique constraints are
still required to enforce uniqueness under concurrent writes. Projected value types
return their default value when absent; use a reference DTO or nullable projection
when absence must be distinguishable.

## Load aggregates for commands

An illustrative service-owned specification:

```csharp
public sealed class OrderForCancellation : SingleResultSpecification<Order>
{
    public OrderForCancellation(Guid id)
    {
        Query.Where(order => order.Id == id)
             .Include(order => order.Lines);
    }
}
```

The handler loads through IGenericWriteRepository<Order, Guid>, calls the root's
business method, then IUnitOfWork.SaveChangesAsync. The repository tracks the root
and included children in the same write context. No separate child write repository
or Update call is needed for tracked changes. Wolverine retains transaction and
inbox/outbox ownership. LoadForUpdateAsync means EF tracking; it does **not** acquire
a database row lock. Services still define optimistic concurrency and retry behavior.

Specify the relationships the business operation needs. Avoid filtered includes
when invariants require the complete collection. AsSplitQuery is supported when
appropriate; it is not automatically enabled for every query. Multiple SQL statements
may require stronger transaction isolation when a consistent snapshot is essential.

## Project reads in SQL

Use Specification<TEntity, TResult> and Query.Select to fetch the DTO columns the
query needs, instead of loading an aggregate and mapping it in memory. Include and
ThenInclude are useful for entity graphs; projections normally select their own
navigation data. Keep sorting strongly typed and use a unique tie-breaker for pages.

For reusable list filters and alternate DTO shapes, use Ardalis WithProjectionOf:

```csharp
var page = new OrdersPage(filter, pageNumber, pageSize);
var summary = page.WithProjectionOf(new OrderSummaryProjection());
var items = await reads.ListAsync(summary, cancellationToken);
var total = await reads.CountAsync(page, cancellationToken);
```

This reuses query criteria/order/paging and replaces the projection without mutating
the input specifications. Do not invent general AND/OR specification composition;
Ardalis supports projection reuse specifically. CountAsync counts source entities,
not the cardinality of a flattened SelectMany projection or distinct DTO results.
Count and page calls run sequentially on the scoped context. They are separate
queries, not a guaranteed snapshot if data changes between them.

## Execution guarantees

- Read operations remain untracked. AsNoTrackingWithIdentityResolution is allowed
  for read graphs; AsTracking is rejected on read operations.
- Write loads require an aggregate root and a writable scoped context. No-tracking
  specifications (including identity-resolution mode) are rejected for write loads.
- IgnoreQueryFilters is rejected on all specification paths. Tenant and soft-delete
  isolation comes from the scoped context, not an optional filter in a specification.
- Client-side PostProcessingAction is unsupported and rejected on entity and projected
  materialization. Use SQL projections, or explicitly transform the bounded results
  after querying. Criteria-only count/existence operations do not apply projection
  transformations.
- Lists retain the existing 1,000-result default bound and paging behavior. This limits
  returned root/results, not the total size of included child collections. Use bounded
  projections/pages for large graphs. No IQueryable escapes the repository API.
- Specification cache metadata does not enable automatic caching. Use the existing
  tenant-scoped cache deliberately, with explicit invalidation and actor-aware keys
  when authorization changes the result. Never cache tracked write aggregates.

## Verification

Real PostgreSQL tests exercise unique-match/null/duplicate behavior for entities and
projections; tracked child updates; read-only-context rejection; tenant and soft-delete
filters on included graphs; unsafe tracking/filter/paging flags; rejected post-processing;
and projection reuse with paging and criteria-only counts. Unit evaluation of a
specification can test simple predicates but does not prove SQL translation or tenant
isolation. Add service-specific Testcontainers coverage for query translation and
aggregate mutations.

References: [Ardalis single-result and projection specifications](https://specification.ardalis.com/usage/create-specifications.html),
[WithProjectionOf](https://specification.ardalis.com/features/withprojectionof.html).
