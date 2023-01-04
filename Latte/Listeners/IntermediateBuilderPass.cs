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
    private IScope _currentScope;
    private readonly Stack<LatteParser.StmtContext> _elseStmtsStack = new();
    private readonly GlobalScope _globals;
    private readonly Stack<LatteParser.ExprContext> _ifCondsStack = new();
    private readonly Stack<IfIntermediateInstruction> _ifsStack = new();
    private readonly ParseTreeProperty<bool> _isAndOperand = new();
    private readonly ParseTreeProperty<bool> _isOrOperand = new();
    private readonly Stack<InstructionType> _opsStack = new();
    private readonly ParseTreeProperty<IScope> _scopes;
    private readonly Stack<Term> _termsStack = new();
    private readonly ParseTreeProperty<LatteType> _types;
    private readonly Stack<LabelIntermediateInstruction> _whileBeginStack = new();
    public readonly List<IntermediateFunction> IntermediateFunctions = new();
    private Stack<LabelIntermediateInstruction> _andFailureCases = new();
    private LabelIntermediateInstruction _andJump;

    private Stack<LabelIntermediateInstruction> _andSuccessCases = new();
    private readonly ParseTreeProperty<LatteParser.ExprContext> _boolExprParent = new();

    private readonly ParseTreeProperty<LatteParser.ExprContext> _condParent = new();
    private IntermediateFunction _currentFunction;
    private int _currentLabel;

    private readonly ParseTreeProperty<bool> _negateContext = new();
    private LabelIntermediateInstruction _orFailJump;
    private Stack<LabelIntermediateInstruction> _orFailureCases = new();
    private Stack<LabelIntermediateInstruction> _orSuccessCases = new();
    private LabelIntermediateInstruction _orSuccessJump;
    private ParseTreeProperty<LabelIntermediateInstruction> _toAddJumpsEnter = new();
    private ParseTreeProperty<LabelIntermediateInstruction> _toAddJumpsExit = new();

    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toAddLabelsEnter = new();
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toAddLabelsExit = new();

    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toJumpAfterLabels = new();
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toJumpBodyLabels = new();
    
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toJumpEndElseLabels = new();
    
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toHandleCond = new();


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

    public override void EnterProgram(LatteParser.ProgramContext context)
    {
        _currentScope = _globals;
    }

    public override void EnterTopDef(LatteParser.TopDefContext context)
    {
        _currentScope = _scopes.Get(context);
        _currentFunction = new IntermediateFunction(context.ID().Symbol.Text);
        var scope = _currentScope as FunctionSymbol;
        var arguments = scope.Arguments;

        foreach (var kvp in arguments)
        {
            _currentFunction.Variables.Add(_currentFunction.GetNextRegister(kvp.Value.LatteType, kvp.Key, true, _currentScope));
        }
    }

    public override void ExitTopDef(LatteParser.TopDefContext context)
    {
        _currentScope = _currentScope.GetEnclosingScope();
        var instructions = _currentFunction.Instructions;
        var newInstructions = new List<BaseIntermediateInstruction>();
        var remove = false;

        for (var i = 0; i < instructions.Count; i++)
        {
            // if (i > 0
            //     && instructions[i - 1] is IntermediateInstruction previous
            //     && instructions[i] is IntermediateInstruction current)
            // {
            //     if (current.FirstOperand is RegisterTerm register
            //         && current.SecondOperand == null
            //         && current.InstructionType == InstructionType.Assignment // make sure it makes sense
            //         && previous.LeftHandSide.Identifier != null
            //         && previous.LeftHandSide.Identifier == register.Identifier)
            //     {
            //         previous.LeftHandSide = current.LeftHandSide;
            //     }
            //     else
            //     {
            //         newInstructions.Add(instructions[i]);
            //     }
            // }
            // else
            // {
            //     newInstructions.Add(instructions[i]);
            // }
            
            newInstructions.Add(instructions[i]);
        }

        // bool conditionVal = false;
        // string jumpLabel = "";
        // string ifElseEndJumpLabel = "";
        //
        // BaseIntermediateInstruction constIf;
        //
        // do
        // {
        //     constIf = newInstructions.Find(
        //         x =>
        //         {
        //             var result = x is IfIntermediateInstruction
        //             {
        //                 Condition: ConstantBoolTerm
        //             };
        //
        //             if (result)
        //             {
        //                 var ifIntermediateInstruction = x as IfIntermediateInstruction;
        //                 conditionVal = (ifIntermediateInstruction.Condition as ConstantBoolTerm)
        //                     .Value;
        //                 jumpLabel = ifIntermediateInstruction.JumpLabel.LabelTerm.Label;
        //                 ifElseEndJumpLabel = ifIntermediateInstruction.IfElseEndLabel.LabelTerm.Label;
        //             }
        //
        //             return result;
        //         });
        //
        //     if (constIf != null)
        //     {
        //         var arr = newInstructions.ToArray();
        //         
        //         var fromIndex = newInstructions.FindIndex(x => x == constIf);
        //         var jumpIndex = newInstructions.FindIndex(
        //             x => x is LabelIntermediateInstruction xy && xy.LabelTerm.Label == jumpLabel);
        //         var elseEndIndex = newInstructions.FindIndex(
        //             x => x is LabelIntermediateInstruction xy && xy.LabelTerm.Label == ifElseEndJumpLabel && !xy.IsJump);
        //
        //         if (conditionVal)
        //         {
        //             if (elseEndIndex != -1)
        //             {
        //                 newInstructions = arr[..fromIndex]
        //                     .Concat(arr[(fromIndex + 1)..(jumpIndex - 1)])
        //                     .Concat(arr[(elseEndIndex + 1)..])
        //                     .ToList();
        //             }
        //             else
        //             {
        //                 newInstructions = arr[..fromIndex]
        //                     .Concat(arr[(fromIndex + 1)..jumpIndex])
        //                     .Concat(arr[(jumpIndex + 1)..])
        //                     .ToList();
        //             }
        //         }
        //         else
        //         {
        //             if (elseEndIndex != -1)
        //             {
        //                 newInstructions = arr[..fromIndex]
        //                     .Concat(arr[(jumpIndex + 1)..elseEndIndex])
        //                     .Concat(arr[(elseEndIndex + 1)..])
        //                     .ToList();
        //             }
        //             else
        //             {
        //                 newInstructions = arr[..fromIndex]
        //                     .Concat(arr[(jumpIndex + 1)..])
        //                     .ToList();
        //             }
        //         }
        //     }
        // } while (constIf != null);

        _currentFunction.Instructions = newInstructions;

        IntermediateFunctions.Add(_currentFunction);
    }
    
    public override void EnterBlock(LatteParser.BlockContext context)
    {
        _currentScope = _scopes.Get(context);
    }

    public override void ExitBlock(LatteParser.BlockContext context)
    {
        _currentScope = _currentScope.GetEnclosingScope();
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

        if (!_currentFunction.TryGetVariable(identifier, _currentScope ,out var registerTerm))
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
        var identifier = context.ID().Symbol.Text;

        if (!_currentFunction.TryGetVariable(identifier, _currentScope ,out var registerTerm))
        {
            throw new Exception("No variable to decrement");
        }

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                registerTerm,
                registerTerm,
                InstructionType.Decrement,
                null));
    }

    public override void EnterEUnOp(LatteParser.EUnOpContext context)
    {
        var bodyLabel = _toJumpBodyLabels.Get(context);
        var afterLabel = _toJumpAfterLabels.Get(context);

        _toJumpBodyLabels.Put(context.expr(), bodyLabel);
        _toJumpAfterLabels.Put(context.expr(), afterLabel);

        if (!_negateContext.Get(context))
        {
            _negateContext.Put(context.expr(), true);
        }
        else
        {
            _negateContext.Put(context.expr(), false);
        }

        var condParent = _condParent.Get(context);

        if (condParent != null)
        {
            _condParent.Put(context.expr(), condParent);
        }

        var boolExprParent = _boolExprParent.Get(context);

        if (boolExprParent != null)
        {
            _boolExprParent.Put(context.expr(), boolExprParent);
        }

        if (_isAndOperand.Get(context))
        {
            _isAndOperand.Put(context.expr(), true);
        }

        if (_isOrOperand.Get(context))
        {
            _isOrOperand.Put(context.expr(), true);
        }
    }

    public override void ExitEUnOp(LatteParser.EUnOpContext context)
    {
        var isMinus = context.GetOpType() == UnaryOpType.Minus;

        if (isMinus)
        {
            var value = _termsStack.Pop();
            
            if (value is ConstantIntTerm x)
            {
                _termsStack.Push(new ConstantIntTerm(-x.Value));

                return;
            }

            var nextRegister = _currentFunction.GetNextRegister(LatteType.Int);

            _termsStack.Push(nextRegister);

            var instructionType = InstructionType.NegateInt;

            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    nextRegister,
                    value,
                    instructionType,
                    null));
        }
        else if (!_isOrOperand.Get(context) && !_isAndOperand.Get(context) &&
                 GetInnerContext(context) is not (LatteParser.EOrContext or LatteParser.EAndContext))
        {
            var value = _termsStack.Pop();

            var nextRegister = _currentFunction.GetNextRegister(LatteType.Boolean);

            _termsStack.Push(nextRegister);

            var instructionType = InstructionType.NegateBool;

            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    nextRegister,
                    value,
                    instructionType,
                    null));
        }
        // else
        // {
        //     if (_isAndOperand.Get(context))
        //     {
        //         _currentFunction.Instructions.Add(new IfIntermediateInstruction(
        //             value,
        //             _toJumpAfterLabels.Get(context)));
        //     }
        //
        //     if (_isOrOperand.Get(context))
        //     {
        //         _currentFunction.Instructions.Add(new IfIntermediateInstruction(
        //             value,
        //             _toJumpBodyLabels.Get(context),
        //             negate: true));
        //     }
        // }
    }

    public override void EnterEAnd(LatteParser.EAndContext context)
    {
        var left = context.expr()[0];
        var right = context.expr()[1];

        var condParent = _condParent.Get(context);

        if (condParent != null)
        {
            _condParent.Put(context.expr()[0], condParent);
            _condParent.Put(context.expr()[1], condParent);
        }

        var boolExprParent = _boolExprParent.Get(context);

        if (boolExprParent != null)
        {
            _boolExprParent.Put(context.expr()[0], boolExprParent);
            _boolExprParent.Put(context.expr()[1], boolExprParent);
        }
        else
        {
            _boolExprParent.Put(context, context);
            _boolExprParent.Put(context.expr()[0], context);
            _boolExprParent.Put(context.expr()[1], context);

            _toJumpBodyLabels.Put(context, GetNextLabel());
            _toJumpAfterLabels.Put(context, GetNextLabel());

            // _toAddLabelsEnter.Put(context.stmt(), enterLabel);
            // _toAddLabelsExit.Put(context.stmt(), exitLabel);
        }

        var bodyLabel = _toJumpBodyLabels.Get(context);
        var newBodyLabel = bodyLabel;
        var afterLabel = _toJumpAfterLabels.Get(context);

        if (left is LatteParser.EOrContext or LatteParser.EAndContext or LatteParser.EParenContext
            or LatteParser.EUnOpContext)
        {
            newBodyLabel = GetNextLabel();
            _toAddLabelsEnter.Put(right, newBodyLabel);
        }

        if (_negateContext.Get(context))
        {
            _negateContext.Put(context.expr()[0], true);
            _negateContext.Put(context.expr()[1], true);

            _isOrOperand.Put(left, true);
            _isOrOperand.Put(right, true);

            _toJumpBodyLabels.Put(context.expr()[0], newBodyLabel);
            _toJumpBodyLabels.Put(context.expr()[1], bodyLabel);
            _toJumpAfterLabels.Put(context.expr()[0], afterLabel);
            _toJumpAfterLabels.Put(context.expr()[1], afterLabel);
        }
        else
        {
            _isAndOperand.Put(left, true);
            _isAndOperand.Put(right, true);

            _toJumpBodyLabels.Put(context.expr()[0], newBodyLabel);
            _toJumpBodyLabels.Put(context.expr()[1], bodyLabel);
            _toJumpAfterLabels.Put(context.expr()[0], afterLabel);
            _toJumpAfterLabels.Put(context.expr()[1], afterLabel);
        }
    }

    public override void ExitEAnd(LatteParser.EAndContext context)
    {
        if (_negateContext.Get(context))
        {
            var afterLabel = _toJumpAfterLabels.Get(context);
            _currentFunction.Instructions.Add(new LabelIntermediateInstruction(afterLabel.LabelTerm, true));

            if (_boolExprParent.Get(context) == context)
            {
                var bodyLabel = _toJumpBodyLabels.Get(context);
                _currentFunction.Instructions.Add(bodyLabel);

                var exitLabel = _toJumpAfterLabels.Get(context);
                var endLabel = GetNextLabel();

                var register = _currentFunction.GetNextRegister(LatteType.Boolean);
                _termsStack.Push(register);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(true),
                        InstructionType.Assignment,
                        null));


                _currentFunction.Instructions.Add(new LabelIntermediateInstruction(endLabel.LabelTerm, true));

                _currentFunction.Instructions.Add(exitLabel);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null));

                _currentFunction.Instructions.Add(endLabel);
            }
        }
        else
        {
            var bodyLabel = _toJumpBodyLabels.Get(context);
            _currentFunction.Instructions.Add(new LabelIntermediateInstruction(bodyLabel.LabelTerm, true));

            if (_boolExprParent.Get(context) == context)
            {
                _currentFunction.Instructions.Add(bodyLabel);

                var exitLabel = _toJumpAfterLabels.Get(context);
                var endLabel = GetNextLabel();

                var register = _currentFunction.GetNextRegister(LatteType.Boolean);
                _termsStack.Push(register);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(true),
                        InstructionType.Assignment,
                        null));


                _currentFunction.Instructions.Add(new LabelIntermediateInstruction(endLabel.LabelTerm, true));

                _currentFunction.Instructions.Add(exitLabel);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null));

                _currentFunction.Instructions.Add(endLabel);
            }
        }
    }

    public override void EnterEOr(LatteParser.EOrContext context)
    {
        var left = context.expr()[0];
        var right = context.expr()[1];

        var condParent = _condParent.Get(context);

        if (condParent != null)
        {
            _condParent.Put(context.expr()[0], condParent);
            _condParent.Put(context.expr()[1], condParent);
        }

        var boolExprParent = _boolExprParent.Get(context);

        if (boolExprParent != null)
        {
            _boolExprParent.Put(context.expr()[0], boolExprParent);
            _boolExprParent.Put(context.expr()[1], boolExprParent);
        }
        else
        {
            _boolExprParent.Put(context, context);
            _boolExprParent.Put(context.expr()[0], context);
            _boolExprParent.Put(context.expr()[1], context);

            _toJumpBodyLabels.Put(context, GetNextLabel());
            _toJumpAfterLabels.Put(context, GetNextLabel());

            // _toAddLabelsEnter.Put(context.stmt(), enterLabel);
            // _toAddLabelsExit.Put(context.stmt(), exitLabel);
        }

        var bodyLabel = _toJumpBodyLabels.Get(context);
        var afterLabel = _toJumpAfterLabels.Get(context);
        var newAfterLabel = afterLabel;

        if (left is LatteParser.EOrContext or LatteParser.EAndContext or LatteParser.EParenContext
            or LatteParser.EUnOpContext)
        {
            newAfterLabel = GetNextLabel();
            _toAddLabelsEnter.Put(right, newAfterLabel);
        }

        // _toJumpBodyLabels.Put(left, bodyLabel);
        // _toJumpBodyLabels.Put(right, bodyLabel);
        // _toJumpAfterLabels.Put(left, newAfterLabel);
        // _toJumpAfterLabels.Put(right, afterLabel);

        if (_negateContext.Get(context))
        {
            _negateContext.Put(context.expr()[0], true);
            _negateContext.Put(context.expr()[1], true);

            _isAndOperand.Put(left, true);
            _isAndOperand.Put(right, true);

            _toJumpBodyLabels.Put(left, bodyLabel);
            _toJumpBodyLabels.Put(right, bodyLabel);
            _toJumpAfterLabels.Put(left, newAfterLabel);
            _toJumpAfterLabels.Put(right, afterLabel);
        }
        else
        {
            _isOrOperand.Put(left, true);
            _isOrOperand.Put(right, true);

            _toJumpBodyLabels.Put(left, bodyLabel);
            _toJumpBodyLabels.Put(right, bodyLabel);
            _toJumpAfterLabels.Put(left, newAfterLabel);
            _toJumpAfterLabels.Put(right, afterLabel);
        }
    }

    public override void ExitEOr(LatteParser.EOrContext context)
    {
        if (_negateContext.Get(context))
        {
            var bodyLabel = _toJumpBodyLabels.Get(context);
            _currentFunction.Instructions.Add(new LabelIntermediateInstruction(bodyLabel.LabelTerm, true));

            if (_boolExprParent.Get(context) == context)
            {
                _currentFunction.Instructions.Add(bodyLabel);

                var exitLabel = _toJumpAfterLabels.Get(context);
                var endLabel = GetNextLabel();

                var register = _currentFunction.GetNextRegister(LatteType.Boolean);
                _termsStack.Push(register);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(true),
                        InstructionType.Assignment,
                        null));


                _currentFunction.Instructions.Add(new LabelIntermediateInstruction(endLabel.LabelTerm, true));

                _currentFunction.Instructions.Add(exitLabel);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null));

                _currentFunction.Instructions.Add(endLabel);
            }
        }
        else
        {
            var afterLabel = _toJumpAfterLabels.Get(context);
            _currentFunction.Instructions.Add(new LabelIntermediateInstruction(afterLabel.LabelTerm, true));

            if (_boolExprParent.Get(context) == context)
            {
                var bodyLabel = _toJumpBodyLabels.Get(context);
                _currentFunction.Instructions.Add(bodyLabel);

                var exitLabel = _toJumpAfterLabels.Get(context);
                var endLabel = GetNextLabel();

                var register = _currentFunction.GetNextRegister(LatteType.Boolean);
                _termsStack.Push(register);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(true),
                        InstructionType.Assignment,
                        null));


                _currentFunction.Instructions.Add(new LabelIntermediateInstruction(endLabel.LabelTerm, true));

                _currentFunction.Instructions.Add(exitLabel);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null));

                _currentFunction.Instructions.Add(endLabel);
            }
        }
    }

    public override void EnterEInt(LatteParser.EIntContext context)
    {
        var term = new ConstantIntTerm(int.Parse(context.INT().Symbol.Text));
        _termsStack.Push(term);
    }

    public override void EnterEStr(LatteParser.EStrContext context)
    {
        var term = new ConstantStringTerm(context.STR().Symbol.Text[1..^1]);
        _termsStack.Push(term);
    }

    public override void EnterETrue(LatteParser.ETrueContext context)
    {
        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    new ConstantBoolTerm(true),
                    _toJumpAfterLabels.Get(context),
                    negate: _negateContext.Get(context) == false));
        }
        else if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    new ConstantBoolTerm(true),
                    _toJumpBodyLabels.Get(context),
                    negate: _negateContext.Get(context)));
        }

        // else
        // {
        var term = new ConstantBoolTerm(true);
        _termsStack.Push(term);
        // }
    }

    public override void EnterEFalse(LatteParser.EFalseContext context)
    {
        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    new ConstantBoolTerm(false),
                    _toJumpAfterLabels.Get(context),
                    negate: _negateContext.Get(context) == false));
        }
        else if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    new ConstantBoolTerm(false),
                    _toJumpBodyLabels.Get(context),
                    negate: _negateContext.Get(context)));
        }

        // else
        // {
        var term = new ConstantBoolTerm(false);
        _termsStack.Push(term);
        // }
    }

    public override void EnterEId(LatteParser.EIdContext context)
    {
        var identifierType = _types.Get(context);
        var lhs = context.ID().Symbol.Text;

        if (_currentFunction.TryGetVariable(lhs, _currentScope, out var variableRegister))
        {
            if (_isAndOperand.Get(context))
            {
                _currentFunction.Instructions.Add(
                    new IfIntermediateInstruction(
                        variableRegister,
                        _toJumpAfterLabels.Get(context),
                        negate: _negateContext.Get(context) == false));
            }
            else if (_isOrOperand.Get(context))
            {
                _currentFunction.Instructions.Add(
                    new IfIntermediateInstruction(
                        variableRegister,
                        _toJumpBodyLabels.Get(context),
                        negate: _negateContext.Get(context)));
            }

            // else
            // {
            _termsStack.Push(variableRegister);
            // }

            return;
        }

        var register = _currentFunction.GetNextRegister(identifierType, context.ID().Symbol.Text);
        _termsStack.Push(register);
    }

    public override void ExitEFunCall(LatteParser.EFunCallContext context)
    {
        var functionReturnType = _types.Get(context);
        var funName = context.ID().Symbol.Text;
        var args = context.expr();
        var values = new Stack<Term>();

        foreach (var _ in args)
        {
            values.Push(_termsStack.Pop());
        }

        var term = new FunctionCallTerm(funName, values.ToList());

        var register = functionReturnType is LatteType.Void or LatteType.Invalid
            ? null
            : _currentFunction.GetNextRegister(functionReturnType);

        var callInstruction = new IntermediateInstruction(
            register,
            term,
            InstructionType.FunctionCall,
            null);

        _termsStack.Push(register);

        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    term,
                    _toJumpAfterLabels.Get(context),
                    negate: _negateContext.Get(context) == false));
        }
        else if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    term,
                    _toJumpBodyLabels.Get(context),
                    negate: _negateContext.Get(context)));
        }
        else
        {
            _currentFunction.Instructions.Add(callInstruction);
        }
    }

    public override void ExitEAddOp(LatteParser.EAddOpContext context)
    {
        var resultType = _types.Get(context);
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

        var nextRegister = _currentFunction.GetNextRegister(resultType);

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

        var nextRegister = _currentFunction.GetNextRegister(LatteType.Int);

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

            if (_isAndOperand.Get(context))
            {
                _currentFunction.Instructions.Add(
                    new IfIntermediateInstruction(
                        new ConstantBoolTerm(constBool.Value),
                        _toJumpAfterLabels.Get(context),
                        negate: _negateContext.Get(context) == false));
            }

            if (_isOrOperand.Get(context))
            {
                _currentFunction.Instructions.Add(
                    new IfIntermediateInstruction(
                        new ConstantBoolTerm(constBool.Value),
                        _toJumpBodyLabels.Get(context),
                        negate: _negateContext.Get(context)));
            }

            return;
        }

        var nextRegister = _currentFunction.GetNextRegister(LatteType.Boolean);

        _termsStack.Push(nextRegister);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                nextRegister,
                first,
                op,
                second));

        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    nextRegister,
                    _toJumpAfterLabels.Get(context),
                    negate: _negateContext.Get(context) == false));
        }

        if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    nextRegister,
                    _toJumpBodyLabels.Get(context),
                    negate: _negateContext.Get(context)));
        }
    }

    public override void ExitAss(LatteParser.AssContext context)
    {
        var rhsType = _types.Get(context.expr());
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

        if (_currentFunction.TryGetVariable(lhs, _currentScope, out var variableRegister))
        {
            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    variableRegister,
                    rhs,
                    InstructionType.Assignment,
                    null));

            return;
        }

        var register = _currentFunction.GetNextRegister(rhsType, context.ID().Symbol.Text, scope: _currentScope);

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
        var rhsType = _types.Get(context.expr());
        var rhs = _termsStack.Pop();

        var register = _currentFunction.GetNextRegister(rhsType, context.ID().Symbol.Text, scope: _currentScope);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                register,
                rhs,
                InstructionType.Assignment,
                null));

        _currentFunction.Variables.Add(register);
    }

    public override void EnterDecl(LatteParser.DeclContext context)
    {
        foreach (var item in context.item())
        {
            _types.Put(item, TypesHelper.TryGetLatteType(context.type_().GetText()));
        }
    }

    public override void ExitSimpleDecl(LatteParser.SimpleDeclContext context)
    {
        var type = _types.Get(context);
        var register = _currentFunction.GetNextRegister(type, context.ID().Symbol.Text, scope: _currentScope);
        Term term = type switch
        {
            LatteType.Boolean => new ConstantBoolTerm(false),
            LatteType.Int => new ConstantIntTerm(0),
            LatteType.String => new ConstantStringTerm("")
        };


        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                register,
                term,
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
        // var label = GetNextLabel();
        // var cond = new IntermediateInstruction(null, null, InstructionType.And, null);
        // var ifInstruction = new IfIntermediateInstruction(cond, label);
        // _ifsStack.Push(ifInstruction);
        // _ifCondsStack.Push(context.expr());
        var enterLabel = GetNextLabel();
        var exitLabel = GetNextLabel();

        // if (GetInnerContext(context.expr()) is LatteParser.EAndContext or LatteParser.EOrContext)
        // {
        _toAddLabelsEnter.Put(context.stmt(), enterLabel);
        _toAddLabelsExit.Put(context.stmt(), exitLabel);
        // _toAddJumpsEnter
        _toJumpBodyLabels.Put(context.expr(), enterLabel);
        _toJumpAfterLabels.Put(context.expr(), exitLabel);
        // }

        _toHandleCond.Put(context.stmt(), exitLabel);
        _condParent.Put(context.expr(), context.expr());
    }

    public override void ExitCond(LatteParser.CondContext context)
    {
        // var ifInstruction = _ifsStack.Pop();
        // _currentFunction.Instructions.Add(ifInstruction.JumpLabel);
        // _currentFunction.Instructions.Add(_andJump);
        // _currentFunction.Instructions.Add(_orFailJump);
    }

    public override void EnterCondElse(LatteParser.CondElseContext context)
    {
        var enterLabel = GetNextLabel();
        var exitLabel = GetNextLabel();
        var exitElseLabel = GetNextLabel();

        _toAddLabelsEnter.Put(context.stmt()[0], enterLabel);
        _toAddLabelsExit.Put(context.stmt()[0], exitLabel);
        _toAddLabelsExit.Put(context.stmt()[1], exitElseLabel);
        _toJumpEndElseLabels.Put(context.stmt()[0], exitElseLabel);
        _toJumpBodyLabels.Put(context.expr(), enterLabel);
        _toJumpAfterLabels.Put(context.expr(), exitLabel);

        _toHandleCond.Put(context.stmt()[0], exitLabel);
        _condParent.Put(context.expr(), context.expr());
        
        // _elseStmtsStack.Push(context.stmt()[0]);
        // var elseLabel = GetNextLabel();
        // var endLabel = GetNextLabel();
        // var cond = new IntermediateInstruction(null, null, InstructionType.And, null);
        //
        // var ifInstruction = new IfIntermediateInstruction(cond, elseLabel, endLabel);
        // _ifsStack.Push(ifInstruction);
        // _ifCondsStack.Push(context.expr());
    }

    public override void ExitCondElse(LatteParser.CondElseContext context)
    {
        // var ifInstruction = _ifsStack.Pop();
        // _currentFunction.Instructions.Add(ifInstruction.IfElseEndLabel);
        // _currentFunction.Instructions.Add(_andJump);
    }

    public override void EnterWhile(LatteParser.WhileContext context)
    {
        var startLabel = GetNextLabel();
        var bodyLabel = GetNextLabel();
        var exitLabel = GetNextLabel();
        var exitElseLabel = GetNextLabel();

        _toAddLabelsEnter.Put(context.expr(), startLabel);
        _toAddLabelsEnter.Put(context.stmt(), bodyLabel);
        _toAddLabelsExit.Put(context.stmt(), exitLabel);
        // _toAddLabelsExit.Put(context.stmt()[1], exitElseLabel);
        _toJumpEndElseLabels.Put(context.stmt(), startLabel);
        _toJumpBodyLabels.Put(context.expr(), bodyLabel);
        _toJumpAfterLabels.Put(context.expr(), exitElseLabel);

        _toHandleCond.Put(context.stmt(), exitLabel);
        _condParent.Put(context.expr(), context.expr());
        
        // var label = GetNextLabel();
        //
        // _currentFunction.Instructions.Add(label);
        // _whileBeginStack.Push(label);
        //
        // var exitLabel = GetNextLabel();
        // var cond = new IntermediateInstruction(null, null, InstructionType.And, null);
        // var ifInstruction = new IfIntermediateInstruction(cond, exitLabel);
        // _ifsStack.Push(ifInstruction);
        // _ifCondsStack.Push(context.expr());
    }
    //
    // public override void ExitWhile(LatteParser.WhileContext context)
    // {
    //     var goToBeginningLabel = _whileBeginStack.Pop();
    //     var labelCopy = new LabelIntermediateInstruction(goToBeginningLabel.LabelTerm, true);
    //     _currentFunction.Instructions.Add(labelCopy);
    //     var ifInstruction = _ifsStack.Pop();
    //     _currentFunction.Instructions.Add(ifInstruction.JumpLabel);
    // }

    public override void EnterEveryRule(ParserRuleContext context)
    {
        var condLabel = _toHandleCond.Get(context);

        if (condLabel != null)
        {
            var term = _termsStack.Pop();

            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    term,
                    condLabel,
                    negate: true));
        }

        var addLabel = _toAddLabelsEnter.Get(context);

        if (addLabel != null)
        {
            _currentFunction.Instructions.Add(addLabel);
        }
    }

    public override void ExitEveryRule(ParserRuleContext context)
    {
        var endElseLabel = _toJumpEndElseLabels.Get(context);

        if (endElseLabel != null)
        {
            _currentFunction.Instructions.Add(new LabelIntermediateInstruction(endElseLabel.LabelTerm, true));
        }
        
        var exitLabel = _toAddLabelsExit.Get(context);

        if (exitLabel != null)
        {
            _currentFunction.Instructions.Add(exitLabel);
        }

        // if (_ifCondsStack.Count > 0)
        // {
        //     // if (context == _ifCondsStack.Peek())
        //     // {
        //     //     _ifCondsStack.Pop();
        //     //     _currentFunction.Instructions.Add(_orSuccessJump);
        //     // }
        //     
        // }
        // if (_ifCondsStack.Count > 0)
        // {
        //     var expr = _ifCondsStack.Peek();
        //
        //     if (context == expr)
        //     {
        //         _ifCondsStack.Pop();
        //         var ifCondInstruction = _ifsStack.Peek();
        //
        //         if (_termsStack.Count > 0 && _termsStack.Peek() is ConstantBoolTerm constBool)
        //         {
        //             ifCondInstruction.Condition = new IntermediateInstruction(
        //                 null, constBool, InstructionType.None, null);
        //         }
        //         else if (expr is LatteParser.EIdContext && _termsStack.Count > 0 &&
        //                  _termsStack.Peek() is RegisterTerm registerTerm)
        //         {
        //             ifCondInstruction.Condition = new IntermediateInstruction(
        //                 null, registerTerm, InstructionType.None, null);
        //         }
        //         else if (expr is LatteParser.EFunCallContext && _termsStack.Count > 0 &&
        //                  _termsStack.Peek() is RegisterTerm registerTermFunc)
        //         {
        //             ifCondInstruction.Condition = new IntermediateInstruction(
        //                 null, registerTermFunc, InstructionType.None, null);
        //         }
        //         else
        //         {
        //             ifCondInstruction.Condition =
        //                 (IntermediateInstruction)_currentFunction.Instructions.Last();
        //             _currentFunction.Instructions.RemoveAt(_currentFunction.Instructions.Count - 1);
        //         }
        //
        //         _currentFunction.Instructions.Add(ifCondInstruction);
        //     }
        // }

        // if (_elseStmtsStack.Count > 0)
        // {
        //     var stmt = _elseStmtsStack.Peek();
        //
        //     if (context == stmt)
        //     {
        //         _elseStmtsStack.Pop();
        //         var ifCondInstruction = _ifsStack.Peek();
        //
        //         _currentFunction.Instructions.Add(
        //             new LabelIntermediateInstruction(ifCondInstruction.IfElseEndLabel.LabelTerm, true));
        //         _currentFunction.Instructions.Add(ifCondInstruction.JumpLabel);
        //     }
        // }
    }

    public override void EnterEParen(LatteParser.EParenContext context)
    {
        var bodyLabel = _toJumpBodyLabels.Get(context);
        var afterLabel = _toJumpAfterLabels.Get(context);

        _toJumpBodyLabels.Put(context.expr(), bodyLabel);
        _toJumpAfterLabels.Put(context.expr(), afterLabel);

        if (_negateContext.Get(context))
        {
            _negateContext.Put(context.expr(), true);
        }

        var condParent = _condParent.Get(context);

        if (condParent != null)
        {
            _condParent.Put(context.expr(), condParent);
        }

        var boolExprParent = _boolExprParent.Get(context);

        if (boolExprParent != null)
        {
            _boolExprParent.Put(context.expr(), boolExprParent);
        }

        if (_isAndOperand.Get(context))
        {
            _isAndOperand.Put(context.expr(), true);
        }

        if (_isOrOperand.Get(context))
        {
            _isOrOperand.Put(context.expr(), true);
        }
    }

    private LabelIntermediateInstruction GetNextLabel()
    {
        return new LabelIntermediateInstruction(new LabelTerm($"l{_currentLabel++}"));
    }

    private LatteParser.ExprContext GetInnerContext(LatteParser.ExprContext context)
    {
        var result = context;

        while (result is LatteParser.EParenContext or LatteParser.EUnOpContext)
        {
            if (result is LatteParser.EParenContext paren)
            {
                result = paren.expr();
            }
            else if (result is LatteParser.EUnOpContext unop)
            {
                result = unop.expr();
            }
        }

        return result;
    }
}
