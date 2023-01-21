namespace Latte.Listeners;

using Antlr4.Runtime.Tree;
using Scopes;

public class TypesPass : LatteBaseListener
{
    private readonly GlobalScope _globals;
    private readonly ParseTreeProperty<IScope> _scopes = new();
    private IScope _currentScope;
    public ParseTreeProperty<string> Types = new();

    public TypesPass(GlobalScope globals, ParseTreeProperty<IScope> scopes)
    {
        _globals = globals;
        _scopes = scopes;
    }

    public override void EnterProgram(LatteParser.ProgramContext context) => _currentScope = _globals;

    public override void EnterTopDefFunction(LatteParser.TopDefFunctionContext context) => _currentScope = _scopes.Get(context);

    public override void ExitTopDefFunction(LatteParser.TopDefFunctionContext context) =>
        _currentScope = _currentScope.GetEnclosingScope();

    public override void EnterBlock(LatteParser.BlockContext context) => _currentScope = _scopes.Get(context);

    public override void ExitBlock(LatteParser.BlockContext context) =>
        _currentScope = _currentScope.GetEnclosingScope();

    public override void ExitEMulOp(LatteParser.EMulOpContext context) => Types.Put(context, LatteType.Int);

    public override void ExitEInt(LatteParser.EIntContext context) => Types.Put(context, LatteType.Int);

    public override void ExitETrue(LatteParser.ETrueContext context) => Types.Put(context, LatteType.Boolean);

    public override void ExitEFalse(LatteParser.EFalseContext context) => Types.Put(context, LatteType.Boolean);

    public override void ExitEId(LatteParser.EIdContext context)
    {
        var symbol = _currentScope.Resolve(context.ID().GetText());

        if (symbol is null or FunctionSymbol)
        {
            Types.Put(context, LatteType.Invalid);
        }

        Types.Put(context, symbol.LatteType);
    }

    public override void ExitEAnd(LatteParser.EAndContext context) => Types.Put(context, LatteType.Boolean);

    public override void ExitEOr(LatteParser.EOrContext context) => Types.Put(context, LatteType.Boolean);

    public override void ExitEParen(LatteParser.EParenContext context) => Types.Put(context, Types.Get(context.expr()));

    public override void ExitEStr(LatteParser.EStrContext context) => Types.Put(context, LatteType.String);

    public override void ExitEAddOp(LatteParser.EAddOpContext context)
    {
        var leftType = Types.Get(context.expr()[0]);
        var rightType = Types.Get(context.expr()[1]);

        switch (context.addOp().GetText())
        {
            case "+":
                switch (leftType)
                {
                    case LatteType.String when rightType == LatteType.String:
                        Types.Put(context, LatteType.String);
                        break;
                    case LatteType.Int when rightType == LatteType.Int:
                        Types.Put(context, LatteType.Int);
                        break;
                    default:
                        Types.Put(context, LatteType.Invalid);
                        break;
                }

                break;
            case "-":
                Types.Put(context, LatteType.Int);
                break;
        }
    }

    public override void ExitERelOp(LatteParser.ERelOpContext context) => Types.Put(context, LatteType.Boolean);

    public override void ExitEFunCall(LatteParser.EFunCallContext context)
    {
        var symbol = _currentScope.Resolve(context.ID().GetText());

        if (symbol is null or VariableSymbol)
        {
            Types.Put(context, LatteType.Invalid);

            return;
        }

        var type = symbol.LatteType;

        Types.Put(context, type);
    }

    public override void ExitEUnOp(LatteParser.EUnOpContext context)
    {
        switch (context.Start.Text)
        {
            case "-":
                Types.Put(context, LatteType.Int);
                break;
            case "!":
                Types.Put(context, LatteType.Boolean);
                break;
        }
    }

    public override void ExitENew(LatteParser.ENewContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        if (symbol is not ClassSymbol cs)
        {
            throw new Exception("Can't find class to create");
        }
        
        Types.Put(context, cs.Name);
    }

    public override void ExitENull(LatteParser.ENullContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        if (symbol is not ClassSymbol cs)
        {
            throw new Exception("Can't find class to cast to null");
        }
        
        Types.Put(context, cs.Name);
    }

    public override void ExitFieldAccessLHS(LatteParser.FieldAccessLHSContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        if (symbol is not VariableSymbol vs)
        {
            throw new Exception($"Can't find class instance {name} to get property");
        }

        var classSymbol = _currentScope.Resolve(symbol.LatteType);

        if (classSymbol is not ClassSymbol cs)
        {
            throw new Exception($"Can't find class {symbol.LatteType} to get property");
        }
        
        Types.Put(context, GetFieldAccessType(context.fieldAccess(), cs));
    }

    public override void ExitEFieldAccessRHS(LatteParser.EFieldAccessRHSContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        if (symbol is not VariableSymbol vs)
        {
            throw new Exception($"Can't find class instance {name} to get property");
        }

        var classSymbol = _currentScope.Resolve(symbol.LatteType);

        if (classSymbol is not ClassSymbol cs)
        {
            throw new Exception($"Can't find class {symbol.LatteType} to get property");
        }
        
        Types.Put(context, GetFieldAccessType(context.fieldAccess(), cs));
    }

    private string GetFieldAccessType(LatteParser.FieldAccessContext context, ClassSymbol cs)
    {
        if (context.fieldAccess() == null)
        {
            var field = context.ID();
            var fieldSymbol = cs.Fields.FirstOrDefault(x => x.Name == field.GetText());

            return fieldSymbol.LatteType ?? fieldSymbol.Name;
        }

        var innerObj = context.ID().GetText();
        var name = cs.Fields.FirstOrDefault(x => x.Name == innerObj).LatteType;
        var innerObjSymbol = _currentScope.Resolve(name);

        if (innerObjSymbol is not ClassSymbol innerCs)
        {
            throw new Exception($"Can't find class {innerObj} to get property");
        }

        return GetFieldAccessType(context.fieldAccess(), innerCs);
    }
}
