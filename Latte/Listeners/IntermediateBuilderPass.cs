namespace Latte.Listeners;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Compiler;
using Extensions;
using Models;
using Models.ConstExpression;
using Models.Intermediate;
using Scopes;

public class IntermediateBuilderPass : LatteBaseListener
{
    private readonly ParseTreeProperty<LatteParser.ExprContext> _boolExprParent = new();
    private readonly ParseTreeProperty<IConstExpression> _constantExpressions;
    private readonly GlobalScope _globals;
    private readonly ParseTreeProperty<bool> _isAndOperand = new();
    private readonly ParseTreeProperty<bool> _isOrOperand = new();
    private readonly ParseTreeProperty<bool> _negateContext = new();

    private readonly Stack<InstructionType> _opsStack = new();
    private readonly ParseTreeProperty<IScope> _scopes;
    private readonly Stack<Term> _termsStack = new();

    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toAddLabelsEnter = new();
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toAddLabelsExit = new();
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toHandleCond = new();
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toJumpAfterLabels = new();
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toJumpBodyLabels = new();
    private readonly ParseTreeProperty<LabelIntermediateInstruction> _toJumpEndElseLabels = new();
    private readonly ParseTreeProperty<string> _types;

    public readonly List<IntermediateFunction> IntermediateFunctions = new();
    private int _currentBlock;

    private IntermediateFunction _currentFunction;
    private int _currentLabel;
    private IScope _currentScope;
    private int _latestBlock;

    public IntermediateBuilderPass(
        GlobalScope globals,
        ParseTreeProperty<IScope> scopes,
        ParseTreeProperty<string> types,
        ParseTreeProperty<IConstExpression> constantExpressions)
    {
        _scopes = scopes;
        _globals = globals;
        _types = types;
        _constantExpressions = constantExpressions;
    }

    public override void EnterProgram(LatteParser.ProgramContext context) => _currentScope = _globals;

    public override void EnterTopDefFunction(LatteParser.TopDefFunctionContext context)
    {
        _currentScope = _scopes.Get(context);
        _currentFunction = new IntermediateFunction(context.ID().Symbol.Text);
        var scope = _currentScope as FunctionSymbol;
        var arguments = scope.Arguments;

        foreach (var kvp in arguments)
        {
            _currentFunction.Variables.Add(
                _currentFunction.GetNextRegister(kvp.Value.LatteType, kvp.Key, true, _currentScope));
        }

        _currentBlock++;
    }

    public override void ExitTopDefFunction(LatteParser.TopDefFunctionContext context)
    {
        _currentScope = _currentScope.GetEnclosingScope();
        var newInstructions = new List<BaseIntermediateInstruction>(_currentFunction.Instructions);
        var indexToDelete = -1;

        var changed = false;

        do
        {
            indexToDelete = -1;

            for (var i = 0; i < newInstructions.Count; i++)
            {
                if (i == 0)
                {
                    continue;
                }

                if (newInstructions[i - 1] is LabelIntermediateInstruction l1 &&
                    newInstructions[i] is LabelIntermediateInstruction l2)
                {
                    if (l1.IsJump && l1.LabelTerm.Label == l2.LabelTerm.Label)
                    {
                        indexToDelete = i - 1;
                    }
                    else if (l1.IsJump && l2.IsJump)
                    {
                        indexToDelete = i;
                    }
                }

                if (newInstructions[i] is LabelIntermediateInstruction l3)
                {
                    if (!l3.IsJump && !newInstructions.Any(
                            x => x is LabelIntermediateInstruction { IsJump: true } l4 &&
                                 l4.LabelTerm.Label == l3.LabelTerm.Label) && !newInstructions.Any(
                            x => x is IfIntermediateInstruction if1 &&
                                 (if1.JumpLabel?.LabelTerm?.Label == l3.LabelTerm.Label ||
                                  if1.IfElseEndLabel?.LabelTerm?.Label == l3.LabelTerm.Label)))
                    {
                        indexToDelete = i;
                    }
                }

                if (newInstructions[i] is LabelIntermediateInstruction { Block: > -1 } &&
                    newInstructions[i - 1] is not { Block: -2 })
                {
                    newInstructions[i].Block = -1;
                }
            }

            if (indexToDelete != -1)
            {
                newInstructions.RemoveAt(indexToDelete);
            }
        } while (indexToDelete != -1);

        foreach (var ins in newInstructions)
        {
            if (ins.Block < 0)
            {
                ins.InBoolExpr = true;
            }
        }

        _currentFunction.Instructions = newInstructions;
        FlowAnalyzer.RefreshInstructions(_currentFunction);
        IntermediateFunctions.Add(_currentFunction);
    }

    public override void EnterBlock(LatteParser.BlockContext context) => _currentScope = _scopes.Get(context);

    public override void ExitBlock(LatteParser.BlockContext context) =>
        _currentScope = _currentScope.GetEnclosingScope();

    public override void EnterEAddOp(LatteParser.EAddOpContext context)
    {
        var op = (context.addOp().GetOpType(), _types.Get(context)) switch
        {
            (AddOpType.Minus, _) => InstructionType.Subtract,
            (AddOpType.Plus, LatteType.String) => InstructionType.AddString,
            _ => InstructionType.AddInt
        };

        _opsStack.Push(op);
    }

    public override void EnterEMulOp(LatteParser.EMulOpContext context)
    {
        var op = context.mulOp().GetOpType() switch
        {
            MulOpType.Divide => InstructionType.Divide,
            MulOpType.Multiply => InstructionType.Multiply,
            MulOpType.Modulo => InstructionType.Modulo
        };

        _opsStack.Push(op);
    }

    public override void EnterERelOp(LatteParser.ERelOpContext context)
    {
        var op = context.relOp().GetOpType() switch
        {
            RelOpType.Equal => InstructionType.Equal,
            RelOpType.Greater => InstructionType.Greater,
            RelOpType.Less => InstructionType.Less,
            RelOpType.GreaterEqual => InstructionType.GreaterEqual,
            RelOpType.LessEqual => InstructionType.LessEqual,
            RelOpType.NotEqual => InstructionType.NotEqual
        };

        _opsStack.Push(op);
    }

    public override void ExitIncr(LatteParser.IncrContext context)
    {
        if (context.lhs() is LatteParser.IdLhsContext idLhsContext)
        {
            var identifier = idLhsContext.ID().Symbol.Text;

            if (!_currentFunction.TryGetVariable(identifier, _currentScope, out var registerTerm))
            {
                throw new Exception("No variable to increment");
            }

            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    registerTerm,
                    registerTerm,
                    InstructionType.Increment,
                    null,
                    _currentBlock));
        } 
        else if (context.lhs() is LatteParser.FieldAccessLHSContext fieldAccessLhsContext)
        {
            var fieldAccess = _termsStack.Pop();

            if (fieldAccess is FieldAccessTerm fieldAccessTerm)
            {
                var register = _currentFunction.GetNextRegister(LatteType.Int);
                register.FieldAccessTerm = fieldAccessTerm;
                
                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        fieldAccessTerm,
                        InstructionType.Increment,
                        null,
                        _currentBlock));
            }
        }
    }

    public override void ExitDecr(LatteParser.DecrContext context)
    {
        if (context.lhs() is LatteParser.IdLhsContext idLhsContext)
        {
            var identifier = idLhsContext.ID().Symbol.Text;

            if (!_currentFunction.TryGetVariable(identifier, _currentScope, out var registerTerm))
            {
                throw new Exception("No variable to decrement");
            }

            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    registerTerm,
                    registerTerm,
                    InstructionType.Decrement,
                    null,
                    _currentBlock));
        }
        else if (context.lhs() is LatteParser.FieldAccessLHSContext fieldAccessLhsContext)
        {
            var addressRegister = _termsStack.Pop();

            if (addressRegister is RegisterTerm registerTerm)
            {
                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        registerTerm,
                        registerTerm,
                        InstructionType.Decrement,
                        null,
                        _currentBlock));
            }
        }
    }

    public override void EnterEUnOp(LatteParser.EUnOpContext context)
    {
        PropagateLabels(context, context.expr());
        PropagateParents(context, context.expr());
        PropagateBinOpParents(context, context.expr());

        _negateContext.Put(
            context.expr(),
            !_negateContext.Get(context));
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

            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    nextRegister,
                    value,
                    InstructionType.NegateInt,
                    null,
                    _currentBlock));
        }
        else if (GetInnerContext(context) is not (LatteParser.EOrContext or LatteParser.EAndContext))
        {
            var value = _termsStack.Pop();
            var nextRegister = _currentFunction.GetNextRegister(LatteType.Boolean);
            _termsStack.Push(nextRegister);

            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    nextRegister,
                    value,
                    InstructionType.NegateBool,
                    null,
                    _currentBlock));
        }
    }

    public override void EnterEAnd(LatteParser.EAndContext context)
    {
        var left = context.expr()[0];
        var right = context.expr()[1];

        var boolExprParent = _boolExprParent.Get(context);

        if (boolExprParent != null)
        {
            _boolExprParent.Put(left, boolExprParent);
            _boolExprParent.Put(right, boolExprParent);
        }
        else
        {
            _boolExprParent.Put(context, context);
            _boolExprParent.Put(left, context);
            _boolExprParent.Put(right, context);

            _latestBlock = _currentBlock;
            _currentBlock = -2;

            _toJumpBodyLabels.Put(context, GetNextLabel());
            _toJumpAfterLabels.Put(context, GetNextLabel());
        }

        var bodyLabel = _toJumpBodyLabels.Get(context);
        var newBodyLabel = bodyLabel;
        var afterLabel = _toJumpAfterLabels.Get(context);

        if (GetInnerContext(left) is LatteParser.EOrContext or LatteParser.EAndContext)
        {
            newBodyLabel = GetNextLabel();
            _toAddLabelsEnter.Put(right, newBodyLabel);
        }

        if (_negateContext.Get(context))
        {
            _negateContext.Put(left, true);
            _negateContext.Put(right, true);

            _isOrOperand.Put(left, true);
            _isOrOperand.Put(right, true);
        }
        else
        {
            _isAndOperand.Put(left, true);
            _isAndOperand.Put(right, true);
        }

        _toJumpBodyLabels.Put(left, newBodyLabel);
        _toJumpBodyLabels.Put(right, bodyLabel);
        _toJumpAfterLabels.Put(left, afterLabel);
        _toJumpAfterLabels.Put(right, afterLabel);
    }

    public override void ExitEAnd(LatteParser.EAndContext context)
    {
        if (_negateContext.Get(context))
        {
            var afterLabel = _toJumpAfterLabels.Get(context);
            _currentFunction.Instructions.Add(
                new LabelIntermediateInstruction(afterLabel.LabelTerm, _currentBlock, true));

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
                        null,
                        _currentBlock));


                _currentFunction.Instructions.Add(
                    new LabelIntermediateInstruction(endLabel.LabelTerm, _currentBlock, true));
                _currentFunction.Instructions.Add(exitLabel);

                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null,
                        _currentBlock));

                _currentFunction.Instructions.Add(endLabel);
            }
        }
        else
        {
            var bodyLabel = _toJumpBodyLabels.Get(context);
            _currentFunction.Instructions.Add(
                new LabelIntermediateInstruction(bodyLabel.LabelTerm, _currentBlock, true));

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
                        null,
                        _currentBlock));


                _currentFunction.Instructions.Add(
                    new LabelIntermediateInstruction(endLabel.LabelTerm, _currentBlock, true));
                _currentFunction.Instructions.Add(exitLabel);
                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null,
                        _currentBlock));

                _currentFunction.Instructions.Add(endLabel);
            }
        }
    }

    public override void EnterEOr(LatteParser.EOrContext context)
    {
        var left = context.expr()[0];
        var right = context.expr()[1];

        var boolExprParent = _boolExprParent.Get(context);

        if (boolExprParent != null)
        {
            _boolExprParent.Put(left, boolExprParent);
            _boolExprParent.Put(right, boolExprParent);
        }
        else
        {
            _boolExprParent.Put(context, context);
            _boolExprParent.Put(left, context);
            _boolExprParent.Put(right, context);
            
            _latestBlock = _currentBlock;
            _currentBlock = -2;

            _toJumpBodyLabels.Put(context, GetNextLabel());
            _toJumpAfterLabels.Put(context, GetNextLabel());
        }

        var bodyLabel = _toJumpBodyLabels.Get(context);
        var afterLabel = _toJumpAfterLabels.Get(context);
        var newAfterLabel = afterLabel;

        if (GetInnerContext(left) is LatteParser.EOrContext or LatteParser.EAndContext)
        {
            newAfterLabel = GetNextLabel();
            _toAddLabelsEnter.Put(right, newAfterLabel);
        }

        if (_negateContext.Get(context))
        {
            _negateContext.Put(left, true);
            _negateContext.Put(right, true);

            _isAndOperand.Put(left, true);
            _isAndOperand.Put(right, true);
        }
        else
        {
            _isOrOperand.Put(left, true);
            _isOrOperand.Put(right, true);
        }

        _toJumpBodyLabels.Put(left, bodyLabel);
        _toJumpBodyLabels.Put(right, bodyLabel);
        _toJumpAfterLabels.Put(left, newAfterLabel);
        _toJumpAfterLabels.Put(right, afterLabel);
    }

    public override void ExitEOr(LatteParser.EOrContext context)
    {
        if (_negateContext.Get(context))
        {
            var bodyLabel = _toJumpBodyLabels.Get(context);
            _currentFunction.Instructions.Add(
                new LabelIntermediateInstruction(bodyLabel.LabelTerm, _currentBlock, true));

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
                        null,
                        _currentBlock));

                _currentFunction.Instructions.Add(
                    new LabelIntermediateInstruction(endLabel.LabelTerm, _currentBlock, true));
                _currentFunction.Instructions.Add(exitLabel);
                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null,
                        _currentBlock));

                _currentFunction.Instructions.Add(endLabel);
            }
        }
        else
        {
            var afterLabel = _toJumpAfterLabels.Get(context);
            _currentFunction.Instructions.Add(
                new LabelIntermediateInstruction(afterLabel.LabelTerm, _currentBlock, true));

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
                        null,
                        _currentBlock));


                _currentFunction.Instructions.Add(
                    new LabelIntermediateInstruction(endLabel.LabelTerm, _currentBlock, true));
                _currentFunction.Instructions.Add(exitLabel);
                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        register,
                        new ConstantBoolTerm(false),
                        InstructionType.Assignment,
                        null,
                        _currentBlock));

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
                    _currentBlock,
                    negate: _negateContext.Get(context) == false));
        }
        else if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    new ConstantBoolTerm(true),
                    _toJumpBodyLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context)));
        }

        var term = new ConstantBoolTerm(true);
        _termsStack.Push(term);
    }

    public override void EnterEFalse(LatteParser.EFalseContext context)
    {
        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    new ConstantBoolTerm(false),
                    _toJumpAfterLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context) == false));
        }
        else if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    new ConstantBoolTerm(false),
                    _toJumpBodyLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context)));
        }

        var term = new ConstantBoolTerm(false);
        _termsStack.Push(term);
    }

    public override void EnterEFieldAccessRHS(LatteParser.EFieldAccessRHSContext context)
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
        
        if (!_currentFunction.TryGetVariable(name, _currentScope, out var classInstanceRegister))
        {
            throw new Exception("Register for instance not found");
        }

        // var register = _currentFunction.GetNextRegister(_types.Get(context));
        // register.MemoryAddress = true;
        var fieldAccess = new FieldAccess();
        var fieldAccessTerm = new FieldAccessTerm(cs, name, fieldAccess, classInstanceRegister, _types.Get(context));
        // var instruction = new IntermediateInstruction(
        //     register,
        //     fieldAccessTerm,
        //     InstructionType.RhsFieldAccess,
        //     null,
        //     _currentBlock);
        //
        // _currentFunction.Instructions.Add(instruction);

        VisitInnerFieldAccess(context.fieldAccess(), cs, fieldAccess);
        
        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    fieldAccessTerm,
                    _toJumpAfterLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context) == false));
            
            _termsStack.Push(fieldAccessTerm);

            return;
        }

        if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    fieldAccessTerm,
                    _toJumpBodyLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context)));
            
            _termsStack.Push(fieldAccessTerm);

            return;
        }

        _termsStack.Push(fieldAccessTerm);
        
        // var identifierType = _types.Get(context);
        // var lhs = context.ID().Symbol.Text;
        //
        // if (_currentFunction.TryGetVariable(lhs, _currentScope, out var variableRegister))
        // {
        //     if (_isAndOperand.Get(context))
        //     {
        //         _currentFunction.Instructions.Add(
        //             new IfIntermediateInstruction(
        //                 variableRegister,
        //                 _toJumpAfterLabels.Get(context),
        //                 _currentBlock,
        //                 negate: _negateContext.Get(context) == false));
        //     }
        //     else if (_isOrOperand.Get(context))
        //     {
        //         _currentFunction.Instructions.Add(
        //             new IfIntermediateInstruction(
        //                 variableRegister,
        //                 _toJumpBodyLabels.Get(context),
        //                 _currentBlock,
        //                 negate: _negateContext.Get(context)));
        //     }
        //
        //     _termsStack.Push(variableRegister);
        //
        //     return;
        // }
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
        
        if (!_currentFunction.TryGetVariable(name, _currentScope, out var classInstanceRegister))
        {
            throw new Exception("Register for instance not found");
        }

        // var register = _currentFunction.GetNextRegister(_types.Get(context));
        // register.MemoryAddress = true;
        var fieldAccess = new FieldAccess();
        var fieldAccessTerm = new FieldAccessTerm(cs, name, fieldAccess, classInstanceRegister, _types.Get(context));
        // var instruction = new IntermediateInstruction(
        //     register,
        //     fieldAccessTerm,
        //     InstructionType.LhsFieldAccess,
        //     null,
        //     _currentBlock);
        
        // _currentFunction.Instructions.Add(instruction);

        VisitInnerFieldAccess(context.fieldAccess(), cs, fieldAccess);
        
        _termsStack.Push(fieldAccessTerm);
    }
    
    private void VisitInnerFieldAccess(
        LatteParser.FieldAccessContext context, 
        ClassSymbol cs, 
        FieldAccess currentFieldAccess)
    {
        ITerminalNode? field;
        Symbol? fieldSymbol;
        
        if (context.fieldAccess() == null)
        {
            field = context.ID();
            fieldSymbol = cs.Fields.FirstOrDefault(x => x.Name == field.GetText());

            currentFieldAccess.ClassField = fieldSymbol.Name;
            currentFieldAccess.InnerFieldAccess = null;

            return;
        }

        var innerObj = context.ID().GetText();
        var name = cs.Fields.FirstOrDefault(x => x.Name == innerObj).LatteType;
        var innerObjSymbol = _currentScope.Resolve(name);
        
        field = context.ID();
        fieldSymbol = cs.Fields.FirstOrDefault(x => x.Name == field.GetText());

        if (innerObjSymbol is not ClassSymbol innerCs)
        {
            throw new Exception($"Can't find class {innerObj} to get property");
        }

        var newFieldAccess = new FieldAccess();
        currentFieldAccess.ClassField = fieldSymbol.Name;
        currentFieldAccess.InnerFieldAccess = newFieldAccess;

        VisitInnerFieldAccess(context.fieldAccess(), innerCs, newFieldAccess);
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
                        _currentBlock,
                        negate: _negateContext.Get(context) == false));
            }
            else if (_isOrOperand.Get(context))
            {
                _currentFunction.Instructions.Add(
                    new IfIntermediateInstruction(
                        variableRegister,
                        _toJumpBodyLabels.Get(context),
                        _currentBlock,
                        negate: _negateContext.Get(context)));
            }

            _termsStack.Push(variableRegister);

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
            null,
            _currentBlock);

        _termsStack.Push(register);

        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    term,
                    _toJumpAfterLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context) == false));
        }
        else if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    term,
                    _toJumpBodyLabels.Get(context),
                    _currentBlock,
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
                second,
                _currentBlock));
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
                second,
                _currentBlock));
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
                        _currentBlock,
                        negate: _negateContext.Get(context) == false));
            }

            if (_isOrOperand.Get(context))
            {
                _currentFunction.Instructions.Add(
                    new IfIntermediateInstruction(
                        new ConstantBoolTerm(constBool.Value),
                        _toJumpBodyLabels.Get(context),
                        _currentBlock,
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
                second,
                _currentBlock));

        if (_isAndOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    nextRegister,
                    _toJumpAfterLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context) == false));
        }

        if (_isOrOperand.Get(context))
        {
            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    nextRegister,
                    _toJumpBodyLabels.Get(context),
                    _currentBlock,
                    negate: _negateContext.Get(context)));
        }
    }

    public override void ExitAss(LatteParser.AssContext context)
    {
        if (context.lhs() is LatteParser.IdLhsContext idLhsContext)
        {
            var rhsType = _types.Get(context.expr());
            var lhs = idLhsContext.ID().Symbol.Text;
            var rhs = _termsStack.Pop();

            if (_currentFunction.TryGetVariable(lhs, _currentScope, out var variableRegister))
            {
                if (rhs is RegisterTerm rt && !_currentFunction.TryGetVariable(rt.Identifier, _currentScope, out _))
                {
                    (_currentFunction.Instructions.Last() as IntermediateInstruction).LeftHandSide = variableRegister;
                }
                else
                {
                    _currentFunction.Instructions.Add(
                        new IntermediateInstruction(
                            variableRegister,
                            rhs,
                            InstructionType.Assignment,
                            null,
                            _currentBlock));
                }

                return;
            }

            var register = _currentFunction.GetNextRegister(rhsType, idLhsContext.ID().Symbol.Text, scope: _currentScope);

            _currentFunction.Instructions.Add(
                new IntermediateInstruction(
                    register,
                    rhs,
                    InstructionType.Assignment,
                    null,
                    _currentBlock));

            _currentFunction.Variables.Add(register);
        }
        else
        {
            var rhs = _termsStack.Pop();
            var lhs = _termsStack.Pop();

            if (lhs is FieldAccessTerm rt)
            {
                var nextRegister = _currentFunction.GetNextRegister(_types.Get(context.lhs()));
                nextRegister.MemoryAddress = true;
                nextRegister.FieldAccessTerm = rt;
                
                _currentFunction.Instructions.Add(
                    new IntermediateInstruction(
                        nextRegister,
                        rhs,
                        InstructionType.Assignment,
                        null,
                        _currentBlock));
            }
        }
    }

    public override void ExitAssDecl(LatteParser.AssDeclContext context)
    {
        var rhsType = _types.Get(context.expr());
        var rhs = _termsStack.Pop();

        var register = _currentFunction.GetNextRegister(rhsType, context.ID().Symbol.Text, scope: _currentScope);
        // register.FieldAccessTerm = new FieldAccessTerm(null, null, null, null);

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                register,
                rhs,
                InstructionType.Assignment,
                null,
                _currentBlock));

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

        if (type == null)
        {
            return;
        }
        
        var register = _currentFunction.GetNextRegister(type, context.ID().Symbol.Text, scope: _currentScope);
        
        Term term = type switch
        {
            LatteType.Boolean => new ConstantBoolTerm(false),
            LatteType.Int => new ConstantIntTerm(0),
            LatteType.String => new ConstantStringTerm(""),
            _ => new ConstantNullTerm(type)
        };

        _currentFunction.Instructions.Add(
            new IntermediateInstruction(
                register,
                term,
                InstructionType.Assignment,
                null,
                _currentBlock));

        _currentFunction.Variables.Add(register);
    }

    public override void ExitRet(LatteParser.RetContext context)
    {
        var term = _termsStack.Pop();
        var instruction = new IntermediateInstruction(null, term, InstructionType.Return, null, _currentBlock);

        _currentFunction.Instructions.Add(instruction);
    }

    public override void ExitVRet(LatteParser.VRetContext context)
    {
        var instruction = new IntermediateInstruction(null, null, InstructionType.Return, null, _currentBlock);
        _currentFunction.Instructions.Add(instruction);
    }

    public override void EnterCond(LatteParser.CondContext context)
    {
        var enterLabel = GetNextLabel();
        var exitLabel = GetNextLabel();

        _toAddLabelsEnter.Put(context.stmt(), enterLabel);
        _toAddLabelsExit.Put(context.stmt(), exitLabel);
        _toJumpBodyLabels.Put(context.expr(), enterLabel);
        _toJumpAfterLabels.Put(context.expr(), exitLabel);

        _toHandleCond.Put(context.stmt(), exitLabel);
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
        _toJumpEndElseLabels.Put(context.stmt(), startLabel);
        _toJumpBodyLabels.Put(context.expr(), bodyLabel);
        _toJumpAfterLabels.Put(context.expr(), exitElseLabel);

        _toHandleCond.Put(context.stmt(), exitLabel);
    }

    public override void ExitENull(LatteParser.ENullContext context)
    {
        var type = _types.Get(context);
        _termsStack.Push(new ConstantNullTerm(type));
    }

    public override void ExitENew(LatteParser.ENewContext context)
    {
        // var lhs = _termsStack.Pop();
        //
        // if (lhs is not FieldAccessTerm rt)
        // {
        //     throw new Exception();
        // }

        // var nextRegister = _currentFunction.GetNextRegister(_types.Get(context));
        // nextRegister.MemoryAddress = true;
        // nextRegister.FieldAccessTerm = rt;
                
        // _currentFunction.Instructions.Add(
        //     new IntermediateInstruction(
        //         nextRegister,
        //         null,
        //         InstructionType.New,
        //         null,
        //         _currentBlock));
            
        // var nextRegister = _currentFunction.GetNextRegister(_types.Get(context));
        // _currentFunction.Instructions.Add(new IntermediateInstruction(
        //     nextRegister, 
        //     null, 
        //     InstructionType.New, 
        //     null, 
        //     _currentBlock));
        
        _termsStack.Push(new NewTerm(_types.Get(context)));
    }

    public override void EnterEveryRule(ParserRuleContext context)
    {
        var isCond = context is LatteParser.CondContext or LatteParser.CondElseContext or LatteParser.WhileContext;
        
        if (isCond)
        {
            if (_currentBlock != -1 && _currentBlock != -2)
            {
                _latestBlock = _currentBlock;
            }
            
            _currentBlock = -1;
        }

        var condLabel = _toHandleCond.Get(context);

        if (condLabel != null)
        {
            var term = _termsStack.Pop();

            _currentFunction.Instructions.Add(
                new IfIntermediateInstruction(
                    term,
                    condLabel,
                    _currentBlock,
                    negate: true));
        }

        var addLabel = _toAddLabelsEnter.Get(context);

        if (addLabel != null)
        {
            _currentFunction.Instructions.Add(addLabel);
        }
        
        if (!isCond && context is LatteParser.StmtContext)
        {
            if (_currentBlock == -1)
            {
                _currentBlock = _latestBlock + 1;
            }

            if (_currentBlock == -2)
            {
                _currentBlock = _latestBlock;
            }
        }
    }

    public override void ExitEveryRule(ParserRuleContext context)
    {
        var isCond = context is LatteParser.CondContext or LatteParser.CondElseContext or LatteParser.WhileContext;
        
        if (isCond)
        {
            if (_currentBlock != -1)
            {
                _currentBlock++;
            }
        }
        
        var endElseLabel = _toJumpEndElseLabels.Get(context);

        if (endElseLabel != null)
        {
            if (_currentBlock != -1)
            {
                _currentBlock++;
            }

            _currentFunction.Instructions.Add(
                new LabelIntermediateInstruction(endElseLabel.LabelTerm, _currentBlock, true));
        }

        var exitLabel = _toAddLabelsExit.Get(context);

        if (exitLabel != null)
        {
            _currentFunction.Instructions.Add(exitLabel);
        }
    }

    public override void EnterEParen(LatteParser.EParenContext context)
    {
        PropagateLabels(context, context.expr());
        PropagateNegations(context, context.expr());
        PropagateParents(context, context.expr());
        PropagateBinOpParents(context, context.expr());
    }

    private void PropagateLabels(
        IParseTree baseContext,
        IParseTree child)
    {
        var bodyLabel = _toJumpBodyLabels.Get(baseContext);
        var afterLabel = _toJumpAfterLabels.Get(baseContext);

        _toJumpBodyLabels.Put(child, bodyLabel);
        _toJumpAfterLabels.Put(child, afterLabel);
    }

    private void PropagateNegations(
        IParseTree baseContext,
        IParseTree child) =>
        _negateContext.Put(child, _negateContext.Get(baseContext));

    private void PropagateParents(
        IParseTree baseContext,
        IParseTree child)
    {
        var boolExprParent = _boolExprParent.Get(baseContext);

        if (boolExprParent != null)
        {
            _boolExprParent.Put(child, boolExprParent);
        }
    }

    private void PropagateBinOpParents(
        IParseTree baseContext,
        IParseTree child)
    {
        if (_isAndOperand.Get(baseContext))
        {
            _isAndOperand.Put(child, true);
        }

        if (_isOrOperand.Get(baseContext))
        {
            _isOrOperand.Put(child, true);
        }
    }

    private LabelIntermediateInstruction GetNextLabel() => new(new LabelTerm($"l{_currentLabel++}"), _currentBlock);

    private static LatteParser.ExprContext GetInnerContext(LatteParser.ExprContext context)
    {
        var result = context;

        while (result is LatteParser.EParenContext or LatteParser.EUnOpContext)
        {
            result = result switch
            {
                LatteParser.EParenContext paren => paren.expr(),
                LatteParser.EUnOpContext unop => unop.expr(),
                _ => result
            };
        }

        return result;
    }
}