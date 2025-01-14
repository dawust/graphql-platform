using System;
using System.Linq.Expressions;
using HotChocolate.Language;
using HotChocolate.Language.Visitors;

namespace HotChocolate.Types.Filters.Expressions;

[Obsolete("Use HotChocolate.Data.")]
public class ArrayFieldHandler
    : IExpressionFieldHandler
{
    public bool Enter(
        FilterOperationField field,
        ObjectFieldNode node,
        IQueryableFilterVisitorContext context,
        out ISyntaxVisitorAction action)
    {
        if (field.Operation.Kind == FilterOperationKind.ArraySome
            || field.Operation.Kind == FilterOperationKind.ArrayNone
            || field.Operation.Kind == FilterOperationKind.ArrayAll)
        {
            var nestedProperty = Expression.Property(
                context.GetInstance(),
                field.Operation.Property);

            context.PushInstance(nestedProperty);

            var closureType = GetTypeFor(field.Operation);

            context.AddClosure(closureType);
            action = SyntaxVisitor.Continue;
            return true;
        }
        action = SyntaxVisitor.SkipAndLeave;
        return false;
    }

    public void Leave(
        FilterOperationField field,
        ObjectFieldNode node,
        IQueryableFilterVisitorContext context)
    {

        if (field.Operation.Kind == FilterOperationKind.ArraySome
            || field.Operation.Kind == FilterOperationKind.ArrayNone
            || field.Operation.Kind == FilterOperationKind.ArrayAll)
        {
            var nestedClosure = context.PopClosure();
            var lambda = nestedClosure.CreateLambda();
            var closureType = GetTypeFor(field.Operation);

            Expression expression;
            switch (field.Operation.Kind)
            {
                case FilterOperationKind.ArraySome:
                    expression = FilterExpressionBuilder.Any(
                        closureType,
                        context.GetInstance(),
                        lambda
                    );
                    break;

                case FilterOperationKind.ArrayNone:
                    expression = FilterExpressionBuilder.Not(
                        FilterExpressionBuilder.Any(
                            closureType,
                            context.GetInstance(),
                            lambda
                        )
                    );
                    break;

                case FilterOperationKind.ArrayAll:
                    expression = FilterExpressionBuilder.All(
                        closureType,
                        context.GetInstance(),
                        lambda
                    );
                    break;

                default:
                    throw new NotSupportedException();
            }

            if (context.InMemory)
            {
                expression = FilterExpressionBuilder.NotNullAndAlso(
                    context.GetInstance(), expression);
            }

            context.GetLevel().Enqueue(expression);
            context.PopInstance();
        }
    }

    private static Type GetTypeFor(FilterOperation operation)
    {
        if (operation.TryGetSimpleFilterBaseType(out var baseType))
        {
            return baseType;
        }
        return operation.Type;
    }
}