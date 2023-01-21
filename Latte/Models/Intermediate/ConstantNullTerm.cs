namespace Latte.Models.Intermediate;

public class ConstantNullTerm : Term
{
    public override List<string> GetStringLiterals() => new();
    public override List<RegisterTerm> GetUsedRegisters() => new();
    public override void SwitchRegisters(string used, string newRegister)
    {
    }

    public override string ToString() => "null";
}