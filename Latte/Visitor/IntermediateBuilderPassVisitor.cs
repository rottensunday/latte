namespace Latte;

using Antlr4.Runtime.Tree;
using Models.ConstExpression;
using Models.Intermediate;
using Scopes;

public class IntermediateBuilderPassVisitor : LatteBaseVisitor<int>
{
    private readonly GlobalScope _globals;
    private readonly ParseTreeProperty<IScope> _scopes;
    private readonly ParseTreeProperty<LatteType> _types;
    private readonly ParseTreeProperty<IConstExpression> _constantExpressions;
    private IntermediateFunction _currentFunction;
    public readonly List<IntermediateFunction> IntermediateFunctions = new();

    public IntermediateBuilderPassVisitor(
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

    public override int VisitTopDef(LatteParser.TopDefContext context)
    {
        _currentFunction = new IntermediateFunction(context.ID().Symbol.Text);
        var scope = _scopes.Get(context) as FunctionSymbol;
        var arguments = scope.Arguments;

        foreach (var kvp in arguments)
        {
            _currentFunction.Variables.Add(_currentFunction.GetNextRegister(kvp.Value.LatteType, kvp.Key));
        }

        VisitChildren(context);
        
        var instructions = _currentFunction.Instructions;
        var newInstructions = new List<BaseIntermediateInstruction>();

        for (var i = 0; i < instructions.Count; i++)
        {
            if (i > 0
                && instructions[i - 1] is IntermediateInstruction previous
                && instructions[i] is IntermediateInstruction current)
            {
                if (current.FirstOperand is RegisterTerm register
                    && current.SecondOperand == null
                    && previous.LeftHandSide.Identifier == register.Identifier)
                {
                    previous.LeftHandSide = current.LeftHandSide;
                }
                else
                {
                    newInstructions.Add(instructions[i]);
                }
            }
            else
            {
                newInstructions.Add(instructions[i]);
            }
        }

        _currentFunction.Instructions = newInstructions;

        IntermediateFunctions.Add(_currentFunction);

        return 0;
    }

    // public override int VisitEAddOp(LatteParser.EAddOpContext context)
    // {
    //     context.expr
    //     
    //     
    // }

    public override int VisitCond(LatteParser.CondContext context)
    {
        Visit(context.expr());
        // 1. if context.expr() is constant: only handle body properly
        // 2. if context.expr() is not constant: evaluate it using Visit(context.expr())
            // 2.1 inside 'if' we should have: boolean variable OR boolean expression
            // 2.2 
            // a || b
            // !a && !b

        return 0;
    }
}
