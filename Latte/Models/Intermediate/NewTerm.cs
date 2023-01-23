namespace Latte.Models.Intermediate;

public class NewTerm : Term
{
    public NewTerm(string latteType)
    {
        LatteType = latteType;
    }

    public string LatteType { get; set; }
    
    public override List<string> GetStringLiterals() => new();

    public override List<RegisterTerm> GetUsedRegisters() => new();

    public override void SwitchRegisters(string used, string newRegister)
    {
        
    }
}