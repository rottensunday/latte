namespace Latte.Listeners;

using Scopes;

public static class TypesHelper
{
    public static LatteType TryGetLatteType(string type)
    {
        return type switch
        {
            "boolean" => LatteType.Boolean,
            "int" => LatteType.Int,
            "string" => LatteType.String,
            "void" => LatteType.Void,
            _ => LatteType.Invalid
        };
    }
}




