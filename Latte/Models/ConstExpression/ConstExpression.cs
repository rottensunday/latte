namespace Latte.Models.ConstExpression;

public record ConstExpression<T>(T Value) : IConstExpression;

