using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Persistence.Database.EFCore
{
    internal static class ModelBuilderExtensions
    {
        private const string SoftDeleteFilterName = "SoftDeleteFilter";

        public static ModelBuilder AppendGlobalQueryFilter<TInterface>(this ModelBuilder modelBuilder, Expression<Func<TInterface, bool>> filter)
        {
            // get root, non-owned entities that implement the interface TInterface
            var entities = modelBuilder.Model.GetEntityTypes()
                .Where(entity =>
                    entity.BaseType is null &&
                    !entity.IsOwned() &&
                    entity.ClrType.GetInterface(typeof(TInterface).Name) is not null)
                .ToArray();

            foreach (Type entity in entities.Select(entityType => entityType.ClrType))
            {
                ParameterExpression parameterType = Expression.Parameter(modelBuilder.Entity(entity).Metadata.ClrType);
                Expression filterBody = ReplaceParameter(filter.Body, filter.Parameters[0], parameterType);

                // Use named query filters (EF Core 10) so this can coexist with
                // Finbuckle's named tenant filter without anonymous/named conflicts.
                modelBuilder.Entity(entity).HasQueryFilter(SoftDeleteFilterName, Expression.Lambda(filterBody, parameterType));
            }

            return modelBuilder;
        }

        private sealed class ParameterReplaceVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplaceVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParameter ? _newParameter : base.VisitParameter(node);
            }
        }

        private static Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            var visitor = new ParameterReplaceVisitor(oldParameter, newParameter);
            return visitor.Visit(expression) ?? expression;
        }
    }
}
