namespace Latte.Scopes;

public class LocalScope : BaseScope
{
    public LocalScope(IScope parent) : base(parent)
    {
    }

    public override string GetScopeName()
    {
        return "locals";
    }
}




