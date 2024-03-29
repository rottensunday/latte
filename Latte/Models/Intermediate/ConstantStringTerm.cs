namespace Latte.Models.Intermediate;

public class ConstantStringTerm : Term
{
    public ConstantStringTerm(string value)
    {
        Value = value;
    }

    public string Value { get; set; }

    public override string ToString() => $"\"{Value}\"";

    public override List<string> GetStringLiterals() => new() { Value };
    public override List<RegisterTerm> GetUsedRegisters() => new();
    public override void SwitchRegisters(string used, string newRegister)
    {
    }
}
