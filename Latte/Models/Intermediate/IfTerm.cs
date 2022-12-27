namespace Latte.Models.Intermediate;

public class IfTerm : Term
{
    public List<IntermediateInstruction> ConditionBuildup { get; set; }
    
    public IntermediateInstruction ConditionInstruction { get; set; }
    
    public List<IntermediateInstruction> IfBody { get; set; }
    
    public LabelTerm EndLabel { get; set; }
}
