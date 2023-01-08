namespace Latte.Listeners;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Models;
using Scopes;

public class SymbolTablePass : LatteBaseListener
{
    public IScope CurrentScope;
    public List<CompilationError> Errors = new();
    public GlobalScope Globals;
    public ParseTreeProperty<IScope> Scopes = new();
    public ParseTreeProperty<LatteType> Types = new();

    public override void EnterProgram(LatteParser.ProgramContext context)
    {
        Globals = new GlobalScope(null);
        CurrentScope = Globals;

        CurrentScope.Define(
            new FunctionSymbol(
                "printInt",
                LatteType.Void,
                CurrentScope,
                new Dictionary<string, Symbol> { [""] = new("", LatteType.Int) }));

        CurrentScope.Define(
            new FunctionSymbol(
                "printString",
                LatteType.Void,
                CurrentScope,
                new Dictionary<string, Symbol> { [""] = new("", LatteType.String) }));

        CurrentScope.Define(
            new FunctionSymbol("error", LatteType.Void, CurrentScope));

        CurrentScope.Define(
            new FunctionSymbol("readInt", LatteType.Int, CurrentScope));

        CurrentScope.Define(
            new FunctionSymbol("readString", LatteType.String, CurrentScope));
    }

    public override void EnterTopDef(LatteParser.TopDefContext context)
    {
        var name = context.ID().GetText();
        var typeParsed = context.type_().GetText();
        var type = TypesHelper.TryGetLatteType(typeParsed);

        var functionSymbol = new FunctionSymbol(name, type, CurrentScope);
        CurrentScope.Define(functionSymbol);
        SaveScope(context, functionSymbol);
        CurrentScope = functionSymbol;
    }

    public override void ExitTopDef(LatteParser.TopDefContext context) =>
        CurrentScope = CurrentScope.GetEnclosingScope();

    public override void EnterBlock(LatteParser.BlockContext context)
    {
        CurrentScope = new LocalScope(CurrentScope);
        SaveScope(context, CurrentScope);
    }

    public override void ExitBlock(LatteParser.BlockContext context) => CurrentScope = CurrentScope.GetEnclosingScope();

    public override void ExitArg(LatteParser.ArgContext context)
    {
        var ids = context.ID();
        var types = context.type_();

        if (ids == null)
        {
            return;
        }

        HashSet<string> args = new();

        for (var i = 0; i < ids.Length; i++)
        {
            var name = ids[i].GetText();

            if (args.Contains(name))
            {
                Errors.Add(
                    new CompilationError(
                        CompilationErrorType.DuplicateParameterName,
                        ids[i].Symbol));

                continue;
            }

            args.Add(name);

            var type = TypesHelper.TryGetLatteType(types[i].GetText());

            DefineVar(name, type, ids[i].Symbol);
        }
    }

    public override void ExitDecl(LatteParser.DeclContext context)
    {
        var type = TypesHelper.TryGetLatteType(context.type_().GetText());
        var itemIds = context.item().Select(
            x => x switch
            {
                LatteParser.SimpleDeclContext simpleDeclContext => simpleDeclContext.ID().GetText(),
                LatteParser.AssDeclContext assDeclContext => assDeclContext.ID().GetText(),
                _ => null
            });

        foreach (var name in itemIds)
        {
            DefineVar(name, type, context.Start);
        }
    }

    private void DefineVar(string name, LatteType type, IToken token)
    {
        if (CurrentScope.ResolveFlat(name) != null)
        {
            Errors.Add(
                new CompilationError(
                    CompilationErrorType.VariableAlreadyDeclared,
                    token,
                    name));

            return;
        }

        var variableSymbol = new VariableSymbol(name, type);
        CurrentScope.Define(variableSymbol);
    }

    private void SaveScope(IParseTree ctx, IScope scope) => Scopes.Put(ctx, scope);
}
