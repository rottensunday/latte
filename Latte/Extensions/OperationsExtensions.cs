namespace Latte.Extensions;

using Models;

public static class OperationsExtensions
{
    public static AddOpType GetOpType(this LatteParser.AddOpContext context)
    {
        return context.GetText() switch
        {
            "+" => AddOpType.Plus,
            "-" => AddOpType.Minus
        };
    }

    public static MulOpType GetOpType(this LatteParser.MulOpContext context)
    {
        return context.GetText() switch
        {
            "*" => MulOpType.Multiply,
            "/" => MulOpType.Divide,
            "%" => MulOpType.Modulo
        };
    }

    public static RelOpType GetOpType(this LatteParser.RelOpContext context)
    {
        return context.GetText() switch
        {
            "<" => RelOpType.Less,
            "<=" => RelOpType.LessEqual,
            ">" => RelOpType.Greater,
            ">=" => RelOpType.GreaterEqual,
            "==" => RelOpType.Equal,
            "!=" => RelOpType.NotEqual
        };
    }

    public static UnaryOpType GetOpType(this LatteParser.EUnOpContext context)
    {
        return context.Start.Text switch
        {
            "-" => UnaryOpType.Minus,
            "!" => UnaryOpType.Negation
        };
    }
}



