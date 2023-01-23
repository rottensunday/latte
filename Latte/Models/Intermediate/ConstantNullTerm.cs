namespace Latte.Models.Intermediate;

public class ConstantNullTerm : Term
{
    public ConstantNullTerm(string type)
    {
        Type = type;
    }

    public override List<string> GetStringLiterals() => new();
    public override List<RegisterTerm> GetUsedRegisters() => new();
    public override void SwitchRegisters(string used, string newRegister)
    {
    }
    
    public string Type { get; set; }

    public override string ToString() => "null";
}