namespace Latte.Models.Intermediate;

using Scopes;

public class IntermediateFunction
{
    private int _currentRegister;

    public IntermediateFunction(string name)
    {
        Name = name;
        Variables = new List<RegisterTerm>();
        Instructions = new List<BaseIntermediateInstruction>();
    }

    public List<RegisterTerm> Variables { get; set; }
    
    public List<BaseIntermediateInstruction> Instructions { get; set; }
    
    public string Name { get; set; }
    
    public RegisterTerm GetNextRegister(LatteType type, string identifier = null, bool isParam = false) => new($"t{_currentRegister++}", type, identifier, isParam);

    
    public bool TryGetVariable(string name, out RegisterTerm register)
    {
        register = Variables.Find(x => x.Identifier == name);

        return register != null;
    }
}
