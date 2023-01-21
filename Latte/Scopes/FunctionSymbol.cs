namespace Latte.Scopes;

public class FunctionSymbol : Symbol, IScope
{
    private readonly IScope _enclosingScope;
    public readonly Dictionary<string, Symbol> Arguments = new();

    public FunctionSymbol(
        string name,
        string retLatteType,
        IScope enclosingScope,
        Dictionary<string, Symbol> arguments = default) : base(name, retLatteType)
    {
        _enclosingScope = enclosingScope;
        Arguments = arguments == default ? new Dictionary<string, Symbol>() : arguments;
    }

    public List<Symbol> ArgumentsList => Arguments.Select(x => x.Value).ToList();

    public string GetScopeName() => Name;

    public IScope GetEnclosingScope() => _enclosingScope;

    public void Define(Symbol sym)
    {
        Arguments.Add(sym.Name, sym);
        sym.Scope = this;
    }

    public Symbol Resolve(string name)
    {
        if (Arguments.TryGetValue(name, out var symbol))
        {
            return symbol;
        }

        var enclosingScope = GetEnclosingScope();

        return enclosingScope != null ? enclosingScope.Resolve(name) : null;
    }

    public Symbol ResolveFlat(string name) => Arguments.TryGetValue(name, out var symbol) ? symbol : null;

    public FunctionSymbol GetEnclosingFunction()
    {
        var enclosingScope = _enclosingScope;

        while (enclosingScope is not FunctionSymbol)
        {
            enclosingScope = enclosingScope.GetEnclosingScope();
        }

        return enclosingScope as FunctionSymbol;
    }

    public override string ToString() => $"function {base.ToString()} : {string.Join(", ", Arguments.Values)}";
}
