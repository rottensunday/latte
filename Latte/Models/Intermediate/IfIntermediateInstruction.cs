namespace Latte.Models.Intermediate;

public class IfIntermediateInstruction : BaseIntermediateInstruction
{
    public IfIntermediateInstruction(
        IntermediateInstruction conditionInstruction, 
        LabelIntermediateInstruction jumpLabel, 
        LabelIntermediateInstruction ifElseEndLabel = null)
    {
        ConditionInstruction = conditionInstruction;
        JumpLabel = jumpLabel;
        IfElseEndLabel = ifElseEndLabel;
    }

    public IntermediateInstruction ConditionInstruction { get; set; }
    
    public LabelIntermediateInstruction JumpLabel { get; set; }
    
    public LabelIntermediateInstruction IfElseEndLabel { get; set; }

    public override string ToString()
    {
        return $"if !({ConditionInstruction}) then jumpto {JumpLabel.LabelTerm.Label}";
    }
}
