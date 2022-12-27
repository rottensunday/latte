namespace Latte.Models.Intermediate;

public class LabelIntermediateInstruction : BaseIntermediateInstruction
{
    public LabelIntermediateInstruction(LabelTerm labelTerm, bool isJump = false)
    {
        LabelTerm = labelTerm;
        IsJump = isJump;
    }

    public LabelTerm LabelTerm { get; set; }
    
    public bool IsJump { get; set; }

    public override string ToString()
    {
        return !IsJump ? $"{LabelTerm.Label}:" : $"jmp {LabelTerm.Label}";
    }
}