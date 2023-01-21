namespace Latte.Listeners;

using Scopes;

public static class TypesHelper
{
    public static string TryGetLatteType(string type) =>
        type switch
        {
            "boolean" => LatteType.Boolean,
            "int" => LatteType.Int,
            "string" => LatteType.String,
            "void" => LatteType.Void,
            _ => type
        };

    public static bool IsBasicType(string type)
    {
        return type is LatteType.Boolean or LatteType.Int or LatteType.String;
    }
}
