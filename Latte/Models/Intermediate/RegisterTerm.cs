namespace Latte.Models.Intermediate;

using Scopes;

public class RegisterTerm : Term
{
    public RegisterTerm(
        string name,
        LatteType type,
        string identifier = null,
        bool isParam = false,
        IScope scope = null)
    {
        Name = name;
        Type = type;
        Identifier = identifier;
        IsParam = isParam;
        Scope = scope;
    }

    public string Name { get; set; }

    public string Identifier { get; set; }

    public bool IsParam { get; set; }

    public LatteType Type { get; set; }

    public IScope Scope { get; set; }

    public override string ToString() => Name;

    public override List<string> GetStringLiterals() => new();
}
