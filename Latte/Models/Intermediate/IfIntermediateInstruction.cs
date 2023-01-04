namespace Latte.Models.Intermediate;

public class IfIntermediateInstruction : BaseIntermediateInstruction
{
    // public IfIntermediateInstruction(
    //     IntermediateInstruction condition, 
    //     LabelIntermediateInstruction jumpLabel, 
    //     LabelIntermediateInstruction ifElseEndLabel = null,
    //     bool negate = false)
    // {
    //     Condition = condition;
    //     JumpLabel = jumpLabel;
    //     IfElseEndLabel = ifElseEndLabel;
    //     Negate = negate;
    // }

    public IfIntermediateInstruction(
        Term term,
        LabelIntermediateInstruction jumpLabel,
        LabelIntermediateInstruction ifElseEndLabel = null,
        bool negate = false)
    {
        Condition = term;
        JumpLabel = jumpLabel;
        IfElseEndLabel = ifElseEndLabel;
        Negate = negate;
    }

    public Term Condition { get; set; }
    
    public LabelIntermediateInstruction JumpLabel { get; set; }
    
    public LabelIntermediateInstruction IfElseEndLabel { get; set; }
    
    public bool Negate { get; set; }

    public override string ToString()
    {
        return $"if {(Negate ? "!" : "")}({Condition}) then jumpto {JumpLabel.LabelTerm.Label}";
    }

    public override List<string> GetStringLiterals()
    {
        return Condition.GetStringLiterals().ToList();
    }
}
