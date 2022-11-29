namespace Latte.Scopes;

public abstract class BaseScope : IScope
{
    private readonly IScope _enclosingScope;

    private readonly Dictionary<string, Symbol> _symbols = new();

    public BaseScope(IScope enclosingScope)
    {
        _enclosingScope = enclosingScope;
    }

    public virtual string GetScopeName()
    {
        return "base scope??? idk";
    }

    public IScope GetEnclosingScope()
    {
        return _enclosingScope;
    }

    public void Define(Symbol sym)
    {
        _symbols.Add(sym.Name, sym);
        sym.Scope = this;
    }

    public Symbol Resolve(string name)
    {
        if (_symbols.TryGetValue(name, out var symbol))
        {
            return symbol;
        }

        var enclosingScope = GetEnclosingScope();

        return enclosingScope != null ? enclosingScope.Resolve(name) : null;
    }

    public Symbol ResolveFlat(string name)
    {
        return _symbols.TryGetValue(name, out var symbol) ? symbol : null;
    }

    public FunctionSymbol GetEnclosingFunction()
    {
        var enclosingScope = _enclosingScope;

        while (enclosingScope is not FunctionSymbol)
        {
            enclosingScope = enclosingScope.GetEnclosingScope();
        }

        return enclosingScope as FunctionSymbol;
    }

    public override string ToString()
    {
        return $"{GetScopeName()} : {string.Join(", ", _symbols.Keys)}";
    }
}
