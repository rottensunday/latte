namespace Latte.Scopes;

public class Symbol
{
    public Symbol(string name)
    {
        Name = name;
    }

    public Symbol(string name, string latteType)
    {
        Name = name;
        LatteType = latteType;
    }

    public string Name { get; set; }

    public string LatteType { get; set; }

    public IScope Scope { get; set; }

    public override string ToString() => LatteType != "Invalid" ? $"[{Name}: {LatteType}]" : $"[{Name}]";
}
