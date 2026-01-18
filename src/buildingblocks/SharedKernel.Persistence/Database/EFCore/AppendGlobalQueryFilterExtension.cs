using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Persistence.Database.EFCore
{
    internal static class ModelBuilderExtensions
    {
        public static ModelBuilder AppendGlobalQueryFilter<TInterface>(this ModelBuilder modelBuilder, Expression<Func<TInterface, bool>> filter)
        {
            // get a list of entities without a baseType that implement the interface TInterface
            IEnumerable<Type> entities = modelBuilder.Model.GetEntityTypes()
                .Where(entity => entity.BaseType is null && entity.ClrType.GetInterface(typeof(TInterface).Name) is not null)
                .Select(entity => entity.ClrType);

            foreach (Type? entity in entities)
            {
                ParameterExpression parameterType = Expression.Parameter(modelBuilder.Entity(entity).Metadata.ClrType);
                Expression filterBody = ReplaceParameter(filter.Body, filter.Parameters[0], parameterType);

                // prefer declared query filters (EF Core newer API)
                var declaredFilters = modelBuilder.Entity(entity).Metadata.GetDeclaredQueryFilters();
                if (declaredFilters != null && declaredFilters.Cast<object>().Any())
                {
                    foreach (var lambdaObj in declaredFilters)
                    {
                        if (lambdaObj is LambdaExpression lambda)
                        {
                            Expression existingFilterBody = ReplaceParameter(lambda.Body, lambda.Parameters[0], parameterType);
                            filterBody = Expression.AndAlso(existingFilterBody, filterBody);
                        }
                    }
                }
                else
                {
                    // fallback to older EF API GetQueryFilter() if present (suppress obsolete warning)
#pragma warning disable CS0618
                    var existing = modelBuilder.Entity(entity).Metadata.GetQueryFilter();
#pragma warning restore CS0618

                    if (existing is LambdaExpression existingLambda)
                    {
                        Expression existingFilterBody = ReplaceParameter(existingLambda.Body, existingLambda.Parameters[0], parameterType);
                        filterBody = Expression.AndAlso(existingFilterBody, filterBody);
                    }
                }

                // apply the new query filter
                modelBuilder.Entity(entity).HasQueryFilter(Expression.Lambda(filterBody, parameterType));
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
