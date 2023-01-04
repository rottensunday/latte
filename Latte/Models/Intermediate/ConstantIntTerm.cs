namespace Latte.Models.Intermediate;

public class ConstantIntTerm : Term
{
    public ConstantIntTerm(int value)
    {
        Value = value;
    }

    public int Value { get; set; }

    public override string ToString()
    {
        return Value.ToString();
    }

    public override List<string> GetStringLiterals()
    {
        return new List<string>();
    }
}
