using System.Linq.Expressions;
using System.Reflection;

namespace Jigen.Filtering;

/// <summary>
/// Translates C# Expression<Func<T, bool>> into IFilterExpression AST.
/// This enables filters to work at the serialization level, not requiring deserialization.
/// </summary>
public class ExpressionTranslator : ExpressionVisitor
{
  private IFilterExpression _result;
  private ParameterExpression _parameterExpression;
  private Stack<IFilterExpression> _stack = new();

  public static IFilterExpression Translate<T>(Expression<Func<T, bool>> expression) where T : class, new()
  {
    var translator = new ExpressionTranslator();
    translator._parameterExpression = expression.Parameters[0];
    translator.Visit(expression.Body);
    return translator._result;
  }

  public override Expression Visit(Expression node)
  {
    if (node == null) return null;
    return base.Visit(node);
  }

  protected override Expression VisitBinary(BinaryExpression node)
  {
    if (node.NodeType == ExpressionType.AndAlso || node.NodeType == ExpressionType.OrElse)
    {
      // Handle logical operators
      Visit(node.Left);
      var left = _stack.Pop();

      Visit(node.Right);
      var right = _stack.Pop();

      IFilterExpression result = node.NodeType switch
      {
        ExpressionType.AndAlso => new AndFilter { Left = left, Right = right },
        ExpressionType.OrElse => new OrFilter { Left = left, Right = right },
        _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported")
      };

      _stack.Push(result);
      _result = result;
      return node;
    }
    else if (node.NodeType == ExpressionType.Equal)
    {
      // Handle equality comparisons
      string propertyPath = null;
      object value = null;

      if (IsParameter(node.Left) || IsPropertyAccess(node.Left))
      {
        propertyPath = ExtractPropertyPath(node.Left);
        value = ExtractValue(node.Right);
      }
      else if (IsParameter(node.Right) || IsPropertyAccess(node.Right))
      {
        propertyPath = ExtractPropertyPath(node.Right);
        value = ExtractValue(node.Left);
      }

      if (propertyPath != null)
      {
        var filter = new PropertyEqualsFilter
        {
          PropertyPath = propertyPath,
          Value = value
        };

        _stack.Push(filter);
        _result = filter;
        return node;
      }
    }

    return base.VisitBinary(node);
  }

  protected override Expression VisitMethodCall(MethodCallExpression node)
  {
    // Handle .Any() for collections
    if (node.Method.Name == "Any")
    {
      // Instance call: source.Any(x => ...)
      Expression sourceExpression = node.Object;
      LambdaExpression lambda = null;

      // Extension call: Enumerable.Any(source, x => ...)
      if (sourceExpression == null && node.Arguments.Count > 0)
      {
        sourceExpression = node.Arguments[0];
      }

      if (node.Arguments.Count > 0)
      {
        lambda = ExtractLambdaExpression(node.Arguments[^1]);
      }

      if (lambda != null && sourceExpression != null)
      {
        var propertyPath = ExtractPropertyPath(sourceExpression);
        var filterValue = ExtractComparisonValue(lambda.Body, lambda.Parameters[0]);

        if (propertyPath != null && filterValue != null)
        {
          var filter = new PropertyCollectionAnyFilter
          {
            PropertyPath = propertyPath,
            Value = filterValue
          };

          _stack.Push(filter);
          _result = filter;
          return node;
        }
      }
    }

    return base.VisitMethodCall(node);
  }

  private static LambdaExpression ExtractLambdaExpression(Expression expression)
  {
    return expression switch
    {
      LambdaExpression lambda => lambda,
      UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
      _ => null
    };
  }

  private bool IsParameter(Expression expr) => expr is ParameterExpression pe && pe == _parameterExpression;

  private bool IsPropertyAccess(Expression expr)
  {
    if (expr is MemberExpression me)
      return IsParameter(me.Expression) || IsPropertyAccess(me.Expression);

    return false;
  }

  private string ExtractPropertyPath(Expression expr)
  {
    if (expr is MemberExpression me)
    {
      var nested = ExtractPropertyPath(me.Expression);
      return nested == null ? me.Member.Name : $"{nested}.{me.Member.Name}";
    }

    if (expr is MethodCallExpression mce && mce.Object != null)
    {
      return ExtractPropertyPath(mce.Object);
    }

    return null;
  }

  private static object ExtractValue(Expression expr)
  {
    if (expr is ConstantExpression ce)
      return ce.Value;

    if (expr is MemberExpression me)
    {
      var obj = ExtractValue(me.Expression);
      if (obj != null)
      {
        var field = me.Member as FieldInfo;
        if (field != null)
          return field.GetValue(obj);

        var prop = me.Member as PropertyInfo;
        if (prop != null)
          return prop.GetValue(obj);
      }
    }

    return null;
  }

  private static object ExtractComparisonValue(Expression expr, ParameterExpression parameter)
  {
    if (expr is BinaryExpression be && be.NodeType == ExpressionType.Equal)
    {
      if (be.Left is ParameterExpression leftParameter && leftParameter == parameter)
        return ExtractValue(be.Right);

      if (be.Right is ParameterExpression rightParameter && rightParameter == parameter)
        return ExtractValue(be.Left);
    }

    return null;
  }
}
