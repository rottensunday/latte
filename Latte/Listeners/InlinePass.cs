namespace Latte.Listeners;

using Antlr4.Runtime.Tree;
using Extensions;
using Models;
using Models.ConstExpression;
using Scopes;

public class InlinePass : LatteBaseListener
{
    private readonly GlobalScope _globals;
    private readonly ParseTreeProperty<IScope> _scopes = new();
    public readonly ParseTreeProperty<IConstExpression> ConstantExpressions = new();
    private IScope _currentScope;

    public InlinePass(GlobalScope globals, ParseTreeProperty<IScope> scopes)
    {
        _globals = globals;
        _scopes = scopes;
    }

    public override void EnterProgram(LatteParser.ProgramContext context)
    {
        _currentScope = _globals;
    }

    public override void EnterTopDef(LatteParser.TopDefContext context)
    {
        _currentScope = _scopes.Get(context);
    }

    public override void ExitTopDef(LatteParser.TopDefContext context)
    {
        _currentScope = _currentScope.GetEnclosingScope();
    }

    public override void EnterBlock(LatteParser.BlockContext context)
    {
        _currentScope = _scopes.Get(context);
    }

    public override void ExitBlock(LatteParser.BlockContext context)
    {
        _currentScope = _currentScope.GetEnclosingScope();
    }

    public override void ExitEAnd(LatteParser.EAndContext context)
    {
        var exprLeft = context.expr()[0];
        var exprRight = context.expr()[1];
        var leftValue = ConstantExpressions.Get(exprLeft);
        var rightValue = ConstantExpressions.Get(exprRight);

        if (leftValue is ConstExpression<bool> x && rightValue is ConstExpression<bool> y)
        {
            ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value && y.Value));
        }
    }

    public override void ExitEOr(LatteParser.EOrContext context)
    {
        var exprLeft = context.expr()[0];
        var exprRight = context.expr()[1];
        var leftValue = ConstantExpressions.Get(exprLeft);
        var rightValue = ConstantExpressions.Get(exprRight);

        if (leftValue is ConstExpression<bool> x && rightValue is ConstExpression<bool> y)
        {
            ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value || y.Value));
        }
    }

    public override void ExitEInt(LatteParser.EIntContext context)
    {
        ConstantExpressions.Put(context, new ConstExpression<int>(Int32.Parse(context.GetText())));
    }

    public override void ExitETrue(LatteParser.ETrueContext context)
    {
        ConstantExpressions.Put(context, new ConstExpression<bool>(true));
    }

    public override void ExitEFalse(LatteParser.EFalseContext context)
    {
        ConstantExpressions.Put(context, new ConstExpression<bool>(false));
    }

    public override void ExitEStr(LatteParser.EStrContext context)
    {
        ConstantExpressions.Put(context, new ConstExpression<string>(context.GetText()));
    }

    public override void ExitEParen(LatteParser.EParenContext context)
    {
        var exprValue = ConstantExpressions.Get(context.expr());

        if (exprValue == null)
        {
            return;
        }

        ConstantExpressions.Put(context, exprValue);
    }

    public override void ExitEUnOp(LatteParser.EUnOpContext context)
    {
        var exprValue = ConstantExpressions.Get(context.expr());

        if (exprValue == null)
        {
            return;
        }

        switch ((context.GetOpType(), exprValue))
        {
            case (UnaryOpType.Minus, ConstExpression<int> x):
                ConstantExpressions.Put(context, new ConstExpression<int>(-x.Value));
                break;
            case (UnaryOpType.Negation, ConstExpression<bool> x):
                ConstantExpressions.Put(context, new ConstExpression<bool>(!x.Value));
                break;
        }
    }

    public override void ExitERelOp(LatteParser.ERelOpContext context)
    {
        var exprLeft = context.expr()[0];
        var exprRight = context.expr()[1];
        var leftValue = ConstantExpressions.Get(exprLeft);
        var rightValue = ConstantExpressions.Get(exprRight);

        switch ((context.relOp().GetOpType(), leftValue, rightValue))
        {
            case (RelOpType.Less, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value < y.Value));
                break;
            case (RelOpType.LessEqual, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value <= y.Value));
                break;
            case (RelOpType.Greater, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value > y.Value));
                break;
            case (RelOpType.GreaterEqual, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value >= y.Value));
                break;
            case (RelOpType.Equal, ConstExpression<object> x, ConstExpression<object> y):
                ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value == y.Value));
                break;
            case (RelOpType.NotEqual, ConstExpression<object> x, ConstExpression<object> y):
                ConstantExpressions.Put(context, new ConstExpression<bool>(x.Value != y.Value));
                break;
        }
    }

    public override void ExitEMulOp(LatteParser.EMulOpContext context)
    {
        var exprLeft = context.expr()[0];
        var exprRight = context.expr()[1];
        var leftValue = ConstantExpressions.Get(exprLeft);
        var rightValue = ConstantExpressions.Get(exprRight);

        switch ((context.mulOp().GetOpType(), leftValue, rightValue))
        {
            case (MulOpType.Multiply, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<int>(x.Value * y.Value));
                break;
            case (MulOpType.Divide, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<int>(x.Value / y.Value));
                break;
            case (MulOpType.Modulo, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<int>(x.Value % y.Value));
                break;
        }
    }

    public override void ExitEAddOp(LatteParser.EAddOpContext context)
    {
        var exprLeft = context.expr()[0];
        var exprRight = context.expr()[1];
        var leftValue = ConstantExpressions.Get(exprLeft);
        var rightValue = ConstantExpressions.Get(exprRight);

        switch ((context.addOp().GetOpType(), leftValue, rightValue))
        {
            case (AddOpType.Plus, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<int>(x.Value + y.Value));
                break;
            case (AddOpType.Plus, ConstExpression<string> x, ConstExpression<string> y):
                ConstantExpressions.Put(context, new ConstExpression<string>(x.Value + y.Value));
                break;
            case (AddOpType.Minus, ConstExpression<int> x, ConstExpression<int> y):
                ConstantExpressions.Put(context, new ConstExpression<int>(x.Value - y.Value));
                break;
        }
    }
}

