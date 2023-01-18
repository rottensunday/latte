namespace Latte.Models.Intermediate;

public abstract class BaseIntermediateInstruction
{
    public int Block { get; set; }
    
    public abstract List<string> GetStringLiterals();
}
