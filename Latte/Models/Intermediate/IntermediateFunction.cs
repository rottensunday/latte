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
    
    public RegisterTerm GetNextRegister(
        LatteType type, 
        string identifier = null,
        bool isParam = false,
        IScope scope = null) => new($"t{_currentRegister++}", type, identifier, isParam, scope);

    
    public bool TryGetVariable(string name, IScope scope, out RegisterTerm register)
    {
        if (scope == null)
        {
            register = null;
            return false;
        }
        
        register = Variables.Find(x => x.Identifier == name && x.Scope == scope);

        return register != null || TryGetVariable(name, scope.GetEnclosingScope(), out register);
    }
}
