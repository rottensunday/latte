namespace Latte.Models.Intermediate;

public abstract class BaseIntermediateInstruction
{
    public int Block { get; set; }
    
    public bool InBoolExpr { get; set; }
    
    public abstract List<string> GetStringLiterals();

    public abstract List<RegisterTerm> GetOperands();

    public abstract RegisterTerm GetTarget();

    public abstract void SwitchRegisters(string used, string newRegister);
}
