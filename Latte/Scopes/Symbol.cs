namespace Latte.Scopes;

public class Symbol
{
    public Symbol(string name)
    {
        Name = name;
    }

    public Symbol(string name, LatteType latteType)
    {
        Name = name;
        LatteType = latteType;
    }

    public string Name { get; set; }

    public LatteType LatteType { get; set; }

    public IScope Scope { get; set; }

    public override string ToString() => LatteType != LatteType.Invalid ? $"[{Name}: {LatteType}]" : $"[{Name}]";
}
