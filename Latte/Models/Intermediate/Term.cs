namespace Latte.Models.Intermediate;

public abstract class Term
{
    public abstract List<string> GetStringLiterals();

    public abstract List<RegisterTerm> GetUsedRegisters();

    public abstract void SwitchRegisters(string used, string newRegister);
}
