namespace Latte.Scopes;

public class GlobalScope : BaseScope
{
    public GlobalScope(IScope enclosingScope) : base(enclosingScope)
    {
    }

    public override string GetScopeName()
    {
        return "globals";
    }
}
