namespace Latte.Models.Intermediate;

public class LabelIntermediateInstruction : BaseIntermediateInstruction
{
    public LabelIntermediateInstruction(LabelTerm labelTerm, int block, bool isJump = false)
    {
        LabelTerm = labelTerm;
        IsJump = isJump;
        Block = block;
    }

    public LabelTerm LabelTerm { get; set; }

    public bool IsJump { get; set; }

    public override string ToString() => !IsJump ? $"{LabelTerm.Label}:" + $"  block {Block}" + $"      {InBoolExpr}" : $"jmp {LabelTerm.Label}" + $"  block {Block}" + $"      {InBoolExpr}";

    public override List<string> GetStringLiterals() => new();
}
