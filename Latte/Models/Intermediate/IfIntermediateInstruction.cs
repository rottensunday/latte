namespace Latte.Models.Intermediate;

public class IfIntermediateInstruction : BaseIntermediateInstruction
{
    public IfIntermediateInstruction(
        Term term,
        LabelIntermediateInstruction jumpLabel,
        int block,
        LabelIntermediateInstruction ifElseEndLabel = null,
        bool negate = false)
    {
        Condition = term;
        JumpLabel = jumpLabel;
        Block = block;
        IfElseEndLabel = ifElseEndLabel;
        Negate = negate;
    }

    public Term Condition { get; set; }

    public LabelIntermediateInstruction JumpLabel { get; set; }

    public LabelIntermediateInstruction IfElseEndLabel { get; set; }

    public bool Negate { get; set; }

    public override string ToString() =>
        $"if {(Negate ? "!" : "")}({Condition}) then jumpto {JumpLabel.LabelTerm.Label}" + $"  block {Block}" + $"      {InBoolExpr}";

    public override List<string> GetStringLiterals() => Condition.GetStringLiterals().ToList();
}
