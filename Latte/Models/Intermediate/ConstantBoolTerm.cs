namespace Latte.Models.Intermediate;

using System.Linq.Expressions;

public class ConstantBoolTerm : Term
{
    public ConstantBoolTerm(bool value)
    {
        Value = value;
    }

    public bool Value { get; set; }

    public override string ToString() => Value.ToString();

    public override List<string> GetStringLiterals() => new();
    public override List<RegisterTerm> GetUsedRegisters() => new();

    public override void SwitchRegisters(string used, string newRegister)
    {
        
    }
}
