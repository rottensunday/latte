namespace Latte.Models.Intermediate;

public class ConstantBoolTerm : Term
{
    public ConstantBoolTerm(bool value)
    {
        Value = value;
    }

    public bool Value { get; set; }

    public override string ToString() => Value.ToString();

    public override List<string> GetStringLiterals() => new();
}
