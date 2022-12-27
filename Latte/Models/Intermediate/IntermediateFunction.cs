namespace Latte.Models.Intermediate;

public class IntermediateFunction
{
    private int _currentRegister;
    private int _currentLabel;

    public IntermediateFunction(string name)
    {
        Name = name;
        Variables = new List<RegisterTerm>();
        Instructions = new List<BaseIntermediateInstruction>();
    }

    public List<RegisterTerm> Variables { get; set; }
    
    public List<BaseIntermediateInstruction> Instructions { get; set; }
    
    public string Name { get; set; }
    
    public RegisterTerm GetNextRegister(string identifier = null, bool isParam = false) => new($"t{_currentRegister++}", identifier, isParam);

    public LabelIntermediateInstruction GetNextLabel() => new LabelIntermediateInstruction(new LabelTerm($"l{_currentLabel++}"));
    
    public bool TryGetVariable(string name, out RegisterTerm register)
    {
        register = Variables.Find(x => x.Identifier == name);

        return register != null;
    }
}
