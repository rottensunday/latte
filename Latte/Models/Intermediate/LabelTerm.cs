namespace Latte.Models.Intermediate;

public class LabelTerm : Term
{
    public LabelTerm(string label)
    {
        Label = label;
    }

    public string Label { get; set; }

    public override List<string> GetStringLiterals() => new();
    public override List<RegisterTerm> GetUsedRegisters() => new();
    public override void SwitchRegisters(string used, string newRegister)
    {
    }
}
