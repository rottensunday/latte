namespace Latte.Scopes;


public class ClassSymbol : Symbol, IScope
{
    public readonly List<Symbol> Fields;

    public ClassSymbol(string name, List<Symbol> fields) : base(name)
    {
        Fields = fields;
    }

    public int Size => Fields.Count * 8;

    public int GetFieldOffset(string name)
    {
        for (int i = 0; i < Fields.Count; i++)
        {
            if (Fields[i].Name == name)
            {
                return i * 8;
            }
        }

        return -1;
    }

    public string GetFieldType(string name) => Fields.FirstOrDefault(x => x.Name == name).LatteType;

    public string GetScopeName() => Name;

    public IScope GetEnclosingScope() => throw new NotImplementedException();

    public void Define(Symbol sym) => throw new NotImplementedException();

    public Symbol Resolve(string name) => Fields.FirstOrDefault(x => x.Name == name);

    public Symbol ResolveFlat(string name) => throw new NotImplementedException();

    public FunctionSymbol GetEnclosingFunction() => throw new NotImplementedException();
}