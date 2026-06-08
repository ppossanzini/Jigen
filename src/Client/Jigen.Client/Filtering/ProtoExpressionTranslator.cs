using System.Linq.Expressions;
using System.Reflection;
using Jigen.Proto;

namespace Jigen.Client.Filtering;

internal class ProtoExpressionTranslator : ExpressionVisitor
{
  private FilterNode _result;
  private ParameterExpression _parameterExpression;
  private Stack<FilterNode> _stack = new();

  public static FilterNode Translate<T>(Expression<Func<T, bool>> expression) where T : class, new()
  {
    var translator = new ProtoExpressionTranslator
    {
      _parameterExpression = expression.Parameters[0]
    };

    translator.Visit(expression.Body);
    return translator._result;
  }

  protected override Expression VisitBinary(BinaryExpression node)
  {
    if (node.NodeType == ExpressionType.AndAlso || node.NodeType == ExpressionType.OrElse)
    {
      Visit(node.Left);
      var left = _stack.Pop();

      Visit(node.Right);
      var right = _stack.Pop();

      var logical = new LogicalCondition
      {
        Left = left,
        Right = right
      };

      var filter = new FilterNode();
      if (node.NodeType == ExpressionType.AndAlso)
        filter.And = logical;
      else
        filter.Or = logical;

      _stack.Push(filter);
      _result = filter;
      return node;
    }

    if (node.NodeType == ExpressionType.Equal)
    {
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
        var filter = new FilterNode
        {
          Equals_ = new PropertyEqualsCondition
          {
            PropertyPath = propertyPath,
            Value = ToProtoValue(value)
          }
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
    if (node.Method.Name == "Any")
    {
      Expression sourceExpression = node.Object;
      LambdaExpression lambda = null;

      if (sourceExpression == null && node.Arguments.Count > 0)
        sourceExpression = node.Arguments[0];

      if (node.Arguments.Count > 0)
        lambda = ExtractLambdaExpression(node.Arguments[^1]);

      if (lambda != null && sourceExpression != null)
      {
        var propertyPath = ExtractPropertyPath(sourceExpression);
        var filterValue = ExtractComparisonValue(lambda.Body, lambda.Parameters[0]);

        if (propertyPath != null)
        {
          var filter = new FilterNode
          {
            CollectionAny = new PropertyCollectionAnyCondition
            {
              PropertyPath = propertyPath,
              Value = ToProtoValue(filterValue)
            }
          };

          _stack.Push(filter);
          _result = filter;
          return node;
        }
      }
    }

    return base.VisitMethodCall(node);
  }

  private static FilterValue ToProtoValue(object value)
  {
    var filterValue = new FilterValue();

    if (value == null)
    {
      filterValue.NullValue = true;
      return filterValue;
    }

    switch (value)
    {
      case string s:
        filterValue.StringValue = s;
        break;
      case int i:
        filterValue.IntValue = i;
        break;
      case long l:
        filterValue.LongValue = l;
        break;
      case double d:
        filterValue.DoubleValue = d;
        break;
      case float f:
        filterValue.DoubleValue = f;
        break;
      case bool b:
        filterValue.BoolValue = b;
        break;
      default:
        filterValue.StringValue = value.ToString();
        break;
    }

    return filterValue;
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
      return ExtractPropertyPath(mce.Object);

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
        if (me.Member is FieldInfo field)
          return field.GetValue(obj);

        if (me.Member is PropertyInfo prop)
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
