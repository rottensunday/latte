namespace Latte.Models.Intermediate;

public class ConstantIntTerm : Term
{
    public ConstantIntTerm(int value)
    {
        Value = value;
    }

    public int Value { get; set; }

    public override string ToString() => Value.ToString();

    public override List<string> GetStringLiterals() => new();
    public override List<RegisterTerm> GetUsedRegisters() => new();
    public override void SwitchRegisters(string used, string newRegister)
    {
        
    }
}
