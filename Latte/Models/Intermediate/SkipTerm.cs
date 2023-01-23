namespace Latte.Models.Intermediate;

public class SkipTerm : Term
{
    public override List<string> GetStringLiterals() => throw new NotImplementedException();

    public override List<RegisterTerm> GetUsedRegisters() => throw new NotImplementedException();

    public override void SwitchRegisters(string used, string newRegister) => throw new NotImplementedException();
}