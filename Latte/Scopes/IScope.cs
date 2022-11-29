namespace Latte.Scopes;

public interface IScope
{
    string GetScopeName();

    IScope GetEnclosingScope();

    void Define(Symbol sym);

    Symbol Resolve(string name);

    Symbol ResolveFlat(string name);

    FunctionSymbol GetEnclosingFunction();
}




