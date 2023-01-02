namespace Latte.Models.Intermediate;

using Scopes;

public class RegisterTerm : Term
{
    public RegisterTerm(string name, LatteType type, string identifier = null, bool isParam = false)
    {
        Name = name;
        Type = type;
        Identifier = identifier;
        IsParam = isParam;
    }

    public string Name { get; set; }
    
    public string Identifier { get; set; }
    
    public bool IsParam { get; set; }
    
    public LatteType Type { get; set; }

    public override string ToString()
    {
        return Name;
    }
}
