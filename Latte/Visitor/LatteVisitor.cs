namespace Latte;

using Antlr4.Runtime.Tree;
using Extensions;
using Listeners;
using Models;
using Models.ConstExpression;
using Scopes;

public class LatteVisitor : LatteBaseVisitor<CompilationResult>
{
    private readonly ParseTreeProperty<IConstExpression> _constantExpressions;
    private readonly List<CompilationError> _errors = new();
    private readonly GlobalScope _globals;
    private readonly ParseTreeProperty<IScope> _scopes;
    private readonly ParseTreeProperty<LatteType> _types;
    private IScope _currentScope;

    public LatteVisitor(
        GlobalScope globals,
        ParseTreeProperty<IScope> scopes,
        ParseTreeProperty<LatteType> types,
        ParseTreeProperty<IConstExpression> constantExpressions)
    {
        _scopes = scopes;
        _globals = globals;
        _types = types;
        _constantExpressions = constantExpressions;
    }

    public override CompilationResult VisitProgram(LatteParser.ProgramContext context)
    {
        _currentScope = _globals;
        Console.WriteLine($"we're in program! {context.GetType()}");
        var result = VisitChildren(context);

        result.Errors = _errors;

        return result;
    }

    public override CompilationResult VisitTopDef(LatteParser.TopDefContext context)
    {
        _currentScope = _scopes.Get(context);
        var parametersNodes = context.arg()?.ID();
        var metParameters = new HashSet<string>();
        var functionSymbol = _currentScope.Resolve(context.ID().GetText());

        if (parametersNodes != null)
        {
            foreach (var parameterNode in parametersNodes)
            {
                if (metParameters.Contains(parameterNode.Symbol.Text))
                {
                    _errors.Add(
                        new CompilationError(
                            CompilationErrorType.DuplicateParameterName, parameterNode.Symbol.Line,
                            parameterNode.Symbol.Column));
                }

                metParameters.Add(parameterNode.Symbol.Text);
            }
        }

        if (functionSymbol is FunctionSymbol x && x.LatteType != LatteType.Void)
        {
            if (!CodeReturns(context.block()))
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.FunctionDoesntReturn,
                        context.block().Start,
                        context.ID().GetText()));
            }
        }

        var result = VisitChildren(context);
        _currentScope = _currentScope.GetEnclosingScope();

        return result;
    }

    public override CompilationResult VisitBlock(LatteParser.BlockContext context)
    {
        _currentScope = _scopes.Get(context);

        var result = VisitChildren(context);
        _currentScope = _currentScope.GetEnclosingScope();

        return result;
    }

    public override CompilationResult VisitAss(LatteParser.AssContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);
        var expr = context.expr();
        var exprType = _types.Get(expr);

        switch (symbol)
        {
            case null:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.UndefinedReference,
                        context.ID().Symbol,
                        $"{name} not found"));
                break;
            case FunctionSymbol:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.NotAVariable,
                        context.ID().Symbol,
                        $"{name} is a function"));
                break;
        }

        if (symbol != null && symbol.LatteType != exprType)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr.Start,
                    $"Expected rvalue of type {symbol.LatteType}, found: {exprType}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitDecl(LatteParser.DeclContext context)
    {
        foreach (var x in context.item())
        {
            var expr = x.expr();

            if (expr == null)
            {
                continue;
            }

            var exprType = _types.Get(expr);
            var rvalueType = TypesHelper.TryGetLatteType(context.type_().GetText());

            if (exprType != rvalueType)
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        expr.Start,
                        $"Expected {exprType} in declaration, found {rvalueType}"));
            }
        }

        return VisitChildren(context);
    }

    public override CompilationResult VisitIncr(LatteParser.IncrContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        switch (symbol)
        {
            case null:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.UndefinedReference,
                        context.ID().Symbol,
                        $"{name} not found"));
                break;
            case FunctionSymbol:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.NotAVariable,
                        context.ID().Symbol,
                        $"{name} is a function"));
                break;
            case VariableSymbol x when x.LatteType != LatteType.Int:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        context.ID().Symbol,
                        $"Expected identifier of type {LatteType.Int}, found: {x.LatteType}"));
                break;
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitDecr(LatteParser.DecrContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        switch (symbol)
        {
            case null:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.UndefinedReference,
                        context.ID().Symbol,
                        $"{name} not found"));
                break;
            case FunctionSymbol:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.NotAVariable,
                        context.ID().Symbol,
                        $"{name} is a function"));
                break;
            case VariableSymbol x when x.LatteType != LatteType.Int:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        context.ID().Symbol,
                        $"Expected identifier of type {LatteType.Int}, found: {x.LatteType}"));
                break;
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitEId(LatteParser.EIdContext context)
    {
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        switch (symbol)
        {
            case null:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.UndefinedReference,
                        context.ID().Symbol,
                        $"{name} not found"));
                break;
            case FunctionSymbol:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.NotAVariable,
                        context.ID().Symbol,
                        $"{name} is a function"));
                break;
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitEFunCall(LatteParser.EFunCallContext context)
    {
        var args = context.expr();
        var name = context.ID().GetText();
        var symbol = _currentScope.Resolve(name);

        switch (symbol)
        {
            case null:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.UndefinedReference,
                        context.ID().Symbol,
                        $"{name} not found"));
                break;
            case VariableSymbol:
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.NotAFunction,
                        context.ID().Symbol,
                        $"{name} is a variable"));
                break;
        }

        var symbolArgs = (symbol as FunctionSymbol).ArgumentsList;

        if (symbolArgs.Count != args.Length)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.WrongArgumentsLength,
                    context.Start,
                    $"Expected {symbolArgs.Count} arguments, found {args.Length}"));
        }
        else
        {
            for (var i = 0; i < args.Length; i++)
            {
                var argExpr = args[i];
                var correspondingFormalArg = symbolArgs[i];
                var argExprType = _types.Get(argExpr);

                if (argExprType != correspondingFormalArg.LatteType)
                {
                    _errors.Add(
                        new CompilationError(
                            CompilationErrorType.TypeMismatch,
                            argExpr.Start,
                            $"Expected argument of type {correspondingFormalArg.LatteType}, found {argExprType}"));
                }
            }
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitEMulOp(LatteParser.EMulOpContext context)
    {
        var expr1 = context.expr()[0];
        var expr2 = context.expr()[1];
        var expr1Type = _types.Get(expr1);
        var expr2Type = _types.Get(expr2);

        if (expr1Type != LatteType.Int)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr1.Start,
                    $"Expected expression to be of type {LatteType.Int}, was {expr1Type}"));
        }

        if (expr2Type != LatteType.Int)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr2.Start,
                    $"Expected expression to be of type {LatteType.Int}, was {expr2Type}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitEAddOp(LatteParser.EAddOpContext context)
    {
        var expr1 = context.expr()[0];
        var expr2 = context.expr()[1];
        var expr1Type = _types.Get(expr1);
        var expr2Type = _types.Get(expr2);
        var operation = context.addOp().GetOpType();

        if (operation == AddOpType.Plus)
        {
            if ((expr1Type == LatteType.Int && expr2Type == LatteType.Int)
                || (expr1Type == LatteType.String && expr2Type == LatteType.String))
            {
            }
            else
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        expr1.Start,
                        $"Expected both expressions to be either of type {LatteType.Int} or {LatteType.String}"));
            }
        }

        if (operation == AddOpType.Minus)
        {
            if (expr1Type != LatteType.Int)
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        expr1.Start,
                        $"Expected expression to be of type {LatteType.Int}, was {expr1Type}"));
            }

            if (expr2Type != LatteType.Int)
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        expr2.Start,
                        $"Expected expression to be of type {LatteType.Int}, was {expr2Type}"));
            }
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitERelOp(LatteParser.ERelOpContext context)
    {
        var expr1 = context.expr()[0];
        var expr2 = context.expr()[1];
        var expr1Type = _types.Get(expr1);
        var expr2Type = _types.Get(expr2);
        var operation = context.relOp().GetOpType();

        if (operation is RelOpType.Equal or RelOpType.NotEqual)
        {
            if (expr1Type != expr2Type)
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        expr1.Start,
                        $"Expected both expression to be of same type, but one is {expr1Type}, another is {expr2Type}"));
            }
        }
        else
        {
            if (expr1Type != LatteType.Int)
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        expr1.Start,
                        $"Expected expression to be of type {LatteType.Int}, was {expr1Type}"));
            }

            if (expr2Type != LatteType.Int)
            {
                _errors.Add(
                    new CompilationError(
                        CompilationErrorType.TypeMismatch,
                        expr2.Start,
                        $"Expected expression to be of type {LatteType.Int}, was {expr2Type}"));
            }
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitEUnOp(LatteParser.EUnOpContext context)
    {
        var expr = context.expr();
        var exprType = _types.Get(expr);
        var operation = context.GetOpType();

        if (exprType != LatteType.Int && operation == UnaryOpType.Minus)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr.Start,
                    $"Expected expression to be of type {LatteType.Int}, found {exprType}"));
        }

        if (exprType != LatteType.Boolean && operation == UnaryOpType.Negation)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr.Start,
                    $"Expected expression to be of type {LatteType.Boolean}, found {exprType}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitEAnd(LatteParser.EAndContext context)
    {
        var expr1 = context.expr()[0];
        var expr2 = context.expr()[1];
        var expr1Type = _types.Get(expr1);
        var expr2Type = _types.Get(expr2);

        if (expr1Type != LatteType.Boolean)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr1.Start,
                    $"Expected expression to be {LatteType.Boolean}, found {expr1Type}"));
        }

        if (expr2Type != LatteType.Boolean)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr2.Start,
                    $"Expected expression to be {LatteType.Boolean}, found {expr2Type}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitEOr(LatteParser.EOrContext context)
    {
        var expr1 = context.expr()[0];
        var expr2 = context.expr()[1];
        var expr1Type = _types.Get(expr1);
        var expr2Type = _types.Get(expr2);

        if (expr1Type != LatteType.Boolean)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr1.Start,
                    $"Expected expression to be {LatteType.Boolean}, found {expr1Type}"));
        }

        if (expr2Type != LatteType.Boolean)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr2.Start,
                    $"Expected expression to be {LatteType.Boolean}, found {expr2Type}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitCond(LatteParser.CondContext context)
    {
        var expr = context.expr();
        var exprType = _types.Get(expr);

        if (exprType != LatteType.Boolean)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr.Start,
                    $"Expected expression to be {LatteType.Boolean}, found {exprType}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitCondElse(LatteParser.CondElseContext context)
    {
        var expr = context.expr();
        var exprType = _types.Get(expr);

        if (exprType != LatteType.Boolean)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr.Start,
                    $"Expected expression to be {LatteType.Boolean}, found {exprType}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitWhile(LatteParser.WhileContext context)
    {
        var expr = context.expr();
        var exprType = _types.Get(expr);

        if (exprType != LatteType.Boolean)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    expr.Start,
                    $"Expected expression to be {LatteType.Boolean}, found {exprType}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitVRet(LatteParser.VRetContext context)
    {
        var enclosingFunction = _currentScope.GetEnclosingFunction();

        if (enclosingFunction.LatteType != LatteType.Void)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    context.Start,
                    $"Expected return type {enclosingFunction.LatteType}, found {LatteType.Void}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    public override CompilationResult VisitRet(LatteParser.RetContext context)
    {
        var enclosingFunction = _currentScope.GetEnclosingFunction();
        var expr = context.expr();
        var exprType = _types.Get(expr);

        if (enclosingFunction.LatteType != exprType)
        {
            _errors.Add(
                new CompilationError(
                    CompilationErrorType.TypeMismatch,
                    context.Start,
                    $"Expected return type {enclosingFunction.LatteType}, found {exprType}"));
        }

        var result = VisitChildren(context);

        return result;
    }

    private bool CodeReturns(LatteParser.CondContext context)
    {
        var stmt = context.stmt();
        var value = _constantExpressions.Get(context.expr());

        return value is ConstExpression<bool> { Value: true } && CodeReturns(stmt);
    }

    private bool CodeReturns(LatteParser.CondElseContext context)
    {
        var ifStmt = context.stmt()[0];
        var elseStmt = context.stmt()[1];

        var ifValue = _constantExpressions.Get(context.expr());

        if (ifValue is ConstExpression<bool> { Value: true })
        {
            if (CodeReturns(ifStmt))
            {
                return true;
            }
        }

        if (ifValue is ConstExpression<bool> { Value: false })
        {
            if (CodeReturns(elseStmt))
            {
                return true;
            }
        }

        return CodeReturns(ifStmt) && CodeReturns(elseStmt);
    }

    private bool CodeReturns(LatteParser.WhileContext context)
    {
        var whileValue = _constantExpressions.Get(context.expr());
        var stmt = context.stmt();

        return whileValue is ConstExpression<bool> { Value: true } && CodeReturns(stmt);
    }

    private bool CodeReturns(LatteParser.BlockContext context)
    {
        var stmts = context.stmt();

        return stmts.Any(x => x is LatteParser.RetContext or LatteParser.VRetContext) ||
               stmts.OfType<LatteParser.BlockStmtContext>().Any(x => CodeReturns(x.block())) ||
               stmts.OfType<LatteParser.CondContext>().Any(CodeReturns) ||
               stmts.OfType<LatteParser.CondElseContext>().Any(CodeReturns) ||
               stmts.OfType<LatteParser.WhileContext>().Any(CodeReturns);
    }

    private bool CodeReturns(ITree context)
    {
        return context switch
        {
            LatteParser.RetContext => true,
            LatteParser.VRetContext => true,
            LatteParser.BlockStmtContext x => CodeReturns(x.block()),
            LatteParser.CondContext x => CodeReturns(x),
            LatteParser.CondElseContext x => CodeReturns(x),
            LatteParser.WhileContext x => CodeReturns(x)
        };
    }

    protected override CompilationResult AggregateResult(CompilationResult aggregate, CompilationResult nextResult)
    {
        var leftErrors = aggregate?.Errors;
        var rightErrors = nextResult?.Errors;
        List<CompilationError> errors;

        if (leftErrors == null)
        {
            errors = rightErrors;
        }
        else if (rightErrors == null)
        {
            errors = leftErrors;
        }
        else
        {
            errors = leftErrors.Concat(rightErrors).ToList();
        }

        return new CompilationResult(errors);
    }
}
