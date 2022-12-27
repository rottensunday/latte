namespace Latte.Listeners;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Extensions;
using Models;
using Models.ConstExpression;
using Models.Intermediate;
using Scopes;

public class IntermediateBuilderPass : LatteBaseListener
{
    private readonly ParseTreeProperty<IConstExpression> _constantExpressions;
    private readonly GlobalScope _globals;
    private readonly ParseTreeProperty<IScope> _scopes;
    private readonly Stack<Term> _termsStack = new();
    private readonly ParseTreeProperty<LatteType> _types;
    private IntermediateFunction _currentFunction;
    private readonly Stack<InstructionType> _opsStack = new();
    private readonly Stack<IfIntermediateInstruction> _ifsStack = new();
    private readonly Stack<LatteParser.StmtContext> _elseStmtsStack = new();
    private readonly Stack<LatteParser.ExprContext> _ifCondsStack = new();
    private readonly Stack<LabelIntermediateInstruction> _whileBeginStack = new();
    public readonly List<IntermediateFunction> IntermediateFunctions = new();

    public IntermediateBuilderPass(
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

    public override void EnterTopDef(LatteParser.TopDefContext context)
    {
        _currentFunction = new IntermediateFunction(context.ID().Symbol.Text);
        var scope = _scopes.Get(context) as FunctionSymbol;
        var arguments = scope.Arguments;

        foreach (var kvp in arguments)
        {
            _currentFunction.Variables.Add(_currentFunction.GetNextRegister(kvp.Key, true));
        }
    }

    public override void ExitTopDef(LatteParser.TopDefContext context)
    {
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
                    && current.InstructionType == InstructionType.Assignment // make sure it makes sense
                    && previous.LeftHandSide.Identifier != null
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
    }

    public override void EnterEAddOp(LatteParser.EAddOpContext context)
    {
        switch (context.addOp().GetOpType())
        {
            case AddOpType.Minus:
                _opsStack.Push(InstructionType.Subtract);
                break;
            case AddOpType.Plus:
                switch (_types.Get(context))
                {
                    case LatteType.String:
                        _opsStack.Push(InstructionType.AddString);
                        break;
                    default:
                        _opsStack.Push(InstructionType.AddInt);
                        break;
                }

                break;
        }
    }

    public override void EnterEMulOp(LatteParser.EMulOpContext context)
    {
        switch (context.mulOp().GetOpType())
        {
            case MulOpType.Divide:
                _opsStack.Push(InstructionType.Divide);
                break;
            case MulOpType.Multiply:
                _opsStack.Push(InstructionType.Multiply);
                break;
            case MulOpType.Modulo:
                _opsStack.Push(InstructionType.Modulo);
                break;
        }
    }

    public override void EnterERelOp(LatteParser.ERelOpContext context)
    {
        switch (context.relOp().GetOpType())
        {
            case RelOpType.Equal:
                _opsStack.Push(InstructionType.Equal);
                break;
            case RelOpType.Greater:
                _opsStack.Push(InstructionType.Greater);
                break;
            case RelOpType.Less:
                _opsStack.Push(InstructionType.Less);
                break;
            case RelOpType.GreaterEqual:
                _opsStack.Push(InstructionType.GreaterEqual);
                break;
            case RelOpType.LessEqual:
                _opsStack.Push(InstructionType.LessEqual);
                break;
            case RelOpType.NotEqual:
                _opsStack.Push(InstructionType.NotEqual);
                break;
        }
    }

    public override void ExitIncr(LatteParser.IncrContext context)
    {
        var identifier = context.ID().Symbol.Text;

        if (!_currentFunction.TryGetVariable(identifier, out var registerTerm))
        {
            throw new Exception("No variable to increment");
        }
        
        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                registerTerm,
                registerTerm,
                InstructionType.Increment,
                null));
    }

    public override void ExitDecr(LatteParser.DecrContext context)
    {
        var value = _termsStack.Pop();

        var nextRegister = _currentFunction.GetNextRegister();

        _termsStack.Push(nextRegister);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                value,
                InstructionType.Decrement,
                null));
    }

    public override void ExitEUnOp(LatteParser.EUnOpContext context)
    {
        var value = _termsStack.Pop();

        var nextRegister = _currentFunction.GetNextRegister();

        _termsStack.Push(nextRegister);

        var instructionType = context.GetOpType() == UnaryOpType.Minus
            ? InstructionType.NegateInt
            : InstructionType.NegateBool;

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                value,
                instructionType,
                null));
    }

    public override void ExitEAnd(LatteParser.EAndContext context)
    {
        var second = _termsStack.Pop();
        var first = _termsStack.Pop();

        var constant = _constantExpressions.Get(context);

        if (constant is ConstExpression<bool> constBool)
        {
            _termsStack.Push(new ConstantBoolTerm(constBool.Value));

            return;
        }

        var nextRegister = _currentFunction.GetNextRegister();

        _termsStack.Push(nextRegister);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                first,
                InstructionType.And,
                second));
    }

    public override void ExitEOr(LatteParser.EOrContext context)
    {
        var second = _termsStack.Pop();
        var first = _termsStack.Pop();

        var constant = _constantExpressions.Get(context);

        if (constant is ConstExpression<bool> constBool)
        {
            _termsStack.Push(new ConstantBoolTerm(constBool.Value));

            return;
        }

        var nextRegister = _currentFunction.GetNextRegister();

        _termsStack.Push(nextRegister);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                first,
                InstructionType.Or,
                second));
    }

    public override void EnterEInt(LatteParser.EIntContext context)
    {
        var term = new ConstantIntTerm(int.Parse(context.INT().Symbol.Text));
        _termsStack.Push(term);
    }

    public override void EnterEStr(LatteParser.EStrContext context)
    {
        var term = new ConstantStringTerm(context.STR().Symbol.Text);
        _termsStack.Push(term);
    }

    public override void EnterETrue(LatteParser.ETrueContext context)
    {
        var term = new ConstantBoolTerm(true);
        _termsStack.Push(term);
    }

    public override void EnterEFalse(LatteParser.EFalseContext context)
    {
        var term = new ConstantBoolTerm(false);
        _termsStack.Push(term);
    }

    public override void EnterEId(LatteParser.EIdContext context)
    {
        var lhs = context.ID().Symbol.Text;

        if (_currentFunction.TryGetVariable(lhs, out var variableRegister))
        {
            _termsStack.Push(variableRegister);

            return;
        }

        var register = _currentFunction.GetNextRegister(context.ID().Symbol.Text);
        _termsStack.Push(register);
    }

    public override void ExitEFunCall(LatteParser.EFunCallContext context)
    {
        var funName = context.ID().Symbol.Text;
        var args = context.expr();
        var values = new Stack<Term>();

        foreach (var _ in args)
        {
            values.Push(_termsStack.Pop());
        }

        var term = new FunctionCallTerm(funName, values.ToList());



        var register = _currentFunction.GetNextRegister();
        var callInstruction = new IntermediateInstruction(
            register,
            term,
            InstructionType.FunctionCall,
            null);
        
        _termsStack.Push(register);
        _currentFunction.Instructions.Add(callInstruction);
    }

    public override void ExitEAddOp(LatteParser.EAddOpContext context)
    {
        var second = _termsStack.Pop();
        var first = _termsStack.Pop();
        var op = _opsStack.Pop();

        var constant = _constantExpressions.Get(context);

        if (constant is ConstExpression<int> constInt)
        {
            _termsStack.Push(new ConstantIntTerm(constInt.Value));

            return;
        }

        if (constant is ConstExpression<string> constString)
        {
            _termsStack.Push(new ConstantStringTerm(constString.Value));

            return;
        }

        var nextRegister = _currentFunction.GetNextRegister();

        _termsStack.Push(nextRegister);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                first,
                op,
                second));
    }

    public override void ExitEMulOp(LatteParser.EMulOpContext context)
    {
        var second = _termsStack.Pop();
        var first = _termsStack.Pop();
        var op = _opsStack.Pop();

        var constant = _constantExpressions.Get(context);

        if (constant is ConstExpression<int> constInt)
        {
            _termsStack.Push(new ConstantIntTerm(constInt.Value));

            return;
        }

        var nextRegister = _currentFunction.GetNextRegister();

        _termsStack.Push(nextRegister);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                first,
                op,
                second));
    }

    public override void ExitERelOp(LatteParser.ERelOpContext context)
    {
        var second = _termsStack.Pop();
        var first = _termsStack.Pop();
        var op = _opsStack.Pop();

        var constant = _constantExpressions.Get(context);

        if (constant is ConstExpression<bool> constBool)
        {
            _termsStack.Push(new ConstantBoolTerm(constBool.Value));

            return;
        }

        var nextRegister = _currentFunction.GetNextRegister();

        _termsStack.Push(nextRegister);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                first,
                op,
                second));
    }

    public override void ExitAss(LatteParser.AssContext context)
    {
        // var rightConst = _constantExpressions.Get(context.expr());
        // Term rhs = null;
        //
        // if (rightConst != null)
        // {
        //     if (rightConst is ConstExpression<int> constInt)
        //     {
        //         rhs = new ConstantIntTerm(constInt.Value);
        //     }
        //
        //     if (rightConst is ConstExpression<string> constString)
        //     {
        //         rhs = new ConstantStringTerm(constString.Value);
        //     }
        //
        //     if (rightConst is ConstExpression<bool> constBool)
        //     {
        //         rhs = new ConstantBoolTerm(constBool.Value);
        //     }
        // }

        var lhs = context.ID().Symbol.Text;

        // if (rhs == null)
        // {
        var rhs = _termsStack.Pop();
        // }

        if (_currentFunction.TryGetVariable(lhs, out var variableRegister))
        {
            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    variableRegister,
                    rhs,
                    InstructionType.Assignment,
                    null));

            return;
        }

        var register = _currentFunction.GetNextRegister(context.ID().Symbol.Text);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                register,
                rhs,
                InstructionType.Assignment,
                null));

        _currentFunction.Variables.Add(register);
    }

    public override void ExitAssDecl(LatteParser.AssDeclContext context)
    {
        var rhs = _termsStack.Pop();

        var register = _currentFunction.GetNextRegister(context.ID().Symbol.Text);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                register,
                rhs,
                InstructionType.Assignment,
                null));

        _currentFunction.Variables.Add(register);
    }

    public override void ExitRet(LatteParser.RetContext context)
    {
        var term = _termsStack.Pop();
        var instruction = new IntermediateInstruction(null, term, InstructionType.Return, null);
        
        _currentFunction.Instructions.Add(instruction);
    }

    public override void ExitVRet(LatteParser.VRetContext context)
    {
        var instruction = new IntermediateInstruction(null, null, InstructionType.Return, null);
        _currentFunction.Instructions.Add(instruction);
    }

    public override void EnterCond(LatteParser.CondContext context)
    {
        var label = _currentFunction.GetNextLabel();
        var cond = new IntermediateInstruction(null, null, InstructionType.And, null);
        var ifInstruction = new IfIntermediateInstruction(cond, label);
        _ifsStack.Push(ifInstruction);
        _ifCondsStack.Push(context.expr());
    }

    public override void ExitCond(LatteParser.CondContext context)
    {
        var ifInstruction = _ifsStack.Pop();
        _currentFunction.Instructions.Add(ifInstruction.JumpLabel);
    }

    public override void EnterCondElse(LatteParser.CondElseContext context)
    {
        _elseStmtsStack.Push(context.stmt()[0]);
        var elseLabel = _currentFunction.GetNextLabel();
        var endLabel = _currentFunction.GetNextLabel();
        var cond = new IntermediateInstruction(null, null, InstructionType.And, null);

        var ifInstruction = new IfIntermediateInstruction(cond, elseLabel, endLabel);
        _ifsStack.Push(ifInstruction);
        _ifCondsStack.Push(context.expr());
    }

    public override void ExitCondElse(LatteParser.CondElseContext context)
    {
        var ifInstruction = _ifsStack.Pop();
        _currentFunction.Instructions.Add(ifInstruction.IfElseEndLabel);
    }

    public override void EnterWhile(LatteParser.WhileContext context)
    {
        var label = _currentFunction.GetNextLabel();
        
        _currentFunction.Instructions.Add(label);
        _whileBeginStack.Push(label);
        
        var exitLabel = _currentFunction.GetNextLabel();
        var cond = new IntermediateInstruction(null, null, InstructionType.And, null);
        var ifInstruction = new IfIntermediateInstruction(cond, exitLabel);
        _ifsStack.Push(ifInstruction);
        _ifCondsStack.Push(context.expr());
    }

    public override void ExitWhile(LatteParser.WhileContext context)
    {
        var goToBeginningLabel = _whileBeginStack.Pop();
        var labelCopy = new LabelIntermediateInstruction(goToBeginningLabel.LabelTerm, true);
        _currentFunction.Instructions.Add(labelCopy);
        var ifInstruction = _ifsStack.Pop();
        _currentFunction.Instructions.Add(ifInstruction.JumpLabel);
    }

    public override void ExitEveryRule(ParserRuleContext context)
    {
        if (_ifCondsStack.Count > 0)
        {
            var expr = _ifCondsStack.Peek();

            if (context == expr)
            {
                _ifCondsStack.Pop();
                var ifCondInstruction = _ifsStack.Peek();
                
                if (_termsStack.Count > 0 && _termsStack.Peek() is ConstantBoolTerm constBool)
                {
                    ifCondInstruction.ConditionInstruction = new IntermediateInstruction(
                        null, constBool, InstructionType.None, null);
                }
                else if (expr is LatteParser.EIdContext && _termsStack.Count > 0 &&
                         _termsStack.Peek() is RegisterTerm registerTerm)
                {
                    ifCondInstruction.ConditionInstruction = new IntermediateInstruction(
                        null, registerTerm, InstructionType.None, null);
                }
                else
                {
                    ifCondInstruction.ConditionInstruction = (IntermediateInstruction)_currentFunction.Instructions.Last();
                    _currentFunction.Instructions.RemoveAt(_currentFunction.Instructions.Count - 1);
                }
                
                _currentFunction.Instructions.Add(ifCondInstruction);
            }
        }

        if (_elseStmtsStack.Count > 0)
        {
            var stmt = _elseStmtsStack.Peek();

            if (context == stmt)
            {
                _elseStmtsStack.Pop();
                var ifCondInstruction = _ifsStack.Peek();
                
                _currentFunction.Instructions.Add(new LabelIntermediateInstruction(ifCondInstruction.IfElseEndLabel.LabelTerm, true));
                _currentFunction.Instructions.Add(ifCondInstruction.JumpLabel);
            }
        }
    }
}