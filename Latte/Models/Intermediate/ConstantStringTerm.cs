namespace Latte.Models.Intermediate;

public class ConstantStringTerm : Term
{
    public ConstantStringTerm(string value)
    {
        Value = value;
    }

    public string Value { get; set; }

    public override string ToString()
    {
        return $"\"{Value}\"";
    }

    public override List<string> GetStringLiterals()
    {
        return new List<string> { Value };
    }
}
