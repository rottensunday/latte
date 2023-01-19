namespace Latte.Compiler;

using Latte.Models.Intermediate;
using Scopes;

public class IntermediateToX86Compiler
{
    private readonly Dictionary<string, string> _literalsMap = new();
    private readonly Dictionary<string, int> _registerOffsetMap = new();
    private readonly List<string> _result = new();

    private int _currentBlock = -3;
    private Dictionary<Register, bool> _marked = new();
    private Stack<Register> _physicalRegistersStack = new();
    private int _previousBlock = -3;
    private Dictionary<Register, int> _prNu = new();
    private Dictionary<Register, string> _prToVr = new();

    private int _stackAlignedTo;

    private Dictionary<string, Register?> _vrToPr = new();
    private Dictionary<string, string> _vrToSr = new();

    public IEnumerable<string> Compile(List<IntermediateFunction> functions)
    {
        var literals = functions
            .SelectMany(x => x.Instructions)
            .SelectMany(x => x.GetStringLiterals());

        foreach (var literal in literals)
        {
            AddStringLiteral(literal);
        }

        foreach (var function in functions)
        {
            Reset();
            CompileFunction(function);
        }

        return _result;
    }

    private void CompileFunction(IntermediateFunction function)
    {
        AddInstruction(GasSymbols.GenerateFunctionSymbol(function.Name));
        CompileInstructions(function.Instructions, function.Variables);

        if (_result.Last().Contains("RET"))
        {
            return;
        }

        AddFnEpilog();
    }

    private void Reset() => _registerOffsetMap.Clear();

    private void CompileInstructions(
        List<BaseIntermediateInstruction> instructions,
        List<RegisterTerm> variables)
    {
        AddFnProlog();

        var parameters = variables.Where(x => x.IsParam).ToList();

        // Parameters with stack parameters reversed
        parameters = parameters
            .Take(GasSymbols.ParamRegisters.Count)
            .Concat(parameters.Skip(GasSymbols.ParamRegisters.Count).Reverse())
            .ToList();

        var tempRegisters = new List<Register>(GasSymbols.ParamRegisters);

        if (parameters.Count > GasSymbols.ParamRegisters.Count)
        {
            tempRegisters.AddRange(
                new List<Register>(new Register[parameters.Count - GasSymbols.ParamRegisters.Count]));
        }

        var minusOffset = 0;
        var plusOffset = 8;

        foreach (var (parameter, register) in parameters.Zip(tempRegisters))
        {
            if (register == Register.None)
            {
                // Setup stack parameter
                plusOffset += 8;
                _registerOffsetMap[parameter.Name] = plusOffset;
            }
            else
            {
                minusOffset -= 8;

                // For boolean registers we load one byte from register (from low byte)
                AddInstruction(GasSymbols.GenerateMovToOffset(minusOffset, register));

                _registerOffsetMap[parameter.Name] = minusOffset;
            }
        }

        // Beside parameters we now handle all variables occuring in intermediate code
        // and add them on stack
        var registersNeeded = instructions
            .OfType<IntermediateInstruction>()
            .SelectMany(x => new[] { x.LeftHandSide, x.FirstOperand as RegisterTerm, x.SecondOperand as RegisterTerm })
            .Where(x => x != null)
            .Where(x => !parameters.Any(y => y.Name == x.Name))
            .Where(x => !string.IsNullOrEmpty(x.Identifier))
            .DistinctBy(x => x.Name);

        foreach (var registerNeeded in registersNeeded)
        {
            minusOffset -= 8;

            _registerOffsetMap[registerNeeded.Name] = minusOffset;
        }

        minusOffset = -minusOffset;
        // We need to round up offset to closest number divisible by 8
        minusOffset += (8 - (minusOffset % 8)) % 8;

        AddInstruction(GasSymbols.GenerateSubtract(Register.RSP, minusOffset));

        _stackAlignedTo = (plusOffset - 8 + minusOffset) % 16;

        foreach (var instruction in instructions)
        {
            if (instruction.Block >= 0 && _currentBlock != instruction.Block && _previousBlock != instruction.Block)
            {
                _previousBlock = _currentBlock;
                _currentBlock = instruction.Block;

                PreprocessBlock(
                    instructions.Where(x => x.Block == _currentBlock).OfType<IntermediateInstruction>().ToList());
            }

            CompileInstruction(instruction);
        }
    }

    private void PreprocessBlock(List<IntermediateInstruction> instructions)
    {
        _vrToPr = new Dictionary<string, Register?>();
        _prToVr = new Dictionary<Register, string>();
        _prNu = new Dictionary<Register, int>();
        _physicalRegistersStack = new Stack<Register>();
        _marked = new Dictionary<Register, bool>();
        _vrToSr = new Dictionary<string, string>();

        var registersUsed = instructions
            .SelectMany(x => new[] { x.LeftHandSide, x.FirstOperand as RegisterTerm, x.SecondOperand as RegisterTerm })
            .Where(x => x != null)
            .Distinct();

        foreach (var registerUsed in registersUsed)
        {
            _vrToPr[registerUsed.VirtualRegister] = null;
        }

        foreach (var physicalRegister in GasSymbols.PreservedRegisters)
        {
            _prToVr[physicalRegister] = null;
            _prNu[physicalRegister] = -1;
            _physicalRegistersStack.Push(physicalRegister);
            _marked[physicalRegister] = false;
        }
    }

    private void CompileInstruction(BaseIntermediateInstruction instruction)
    {
        switch (instruction)
        {
            case LabelIntermediateInstruction labelIntermediateInstruction:
                CompileLabelInstruction(labelIntermediateInstruction);
                break;
            case IntermediateInstruction intermediateInstruction:
                CompileIntermediateInstruction(intermediateInstruction);
                break;
            case IfIntermediateInstruction ifIntermediateInstruction:
                CompileIfIntermediateInstruction(ifIntermediateInstruction);
                break;
        }
    }

    private void CompileLabelInstruction(LabelIntermediateInstruction labelIntermediateInstruction) =>
        AddInstruction(
            labelIntermediateInstruction.IsJump
                ? GasSymbols.GenerateUnconditionalJump(labelIntermediateInstruction.LabelTerm.Label)
                : GasSymbols.GenerateLabel(labelIntermediateInstruction.LabelTerm.Label));

    private void CompileIntermediateInstruction(IntermediateInstruction intermediateInstruction)
    {
        foreach (var kvp in _marked)
        {
            _marked[kvp.Key] = false;
        }

        if (intermediateInstruction.FirstOperand is RegisterTerm firstOperand)
        {
            _vrToSr[firstOperand.VirtualRegister] = firstOperand.Name;

            var pr = _vrToPr[firstOperand.VirtualRegister];

            if (pr == null)
            {
                pr = GetAPr(
                    firstOperand.VirtualRegister,
                    firstOperand.NextUse);

                firstOperand.PhysicalRegister = pr.Value;

                AddInstruction(
                    GasSymbols.GenerateMovFromOffset(
                        _registerOffsetMap[firstOperand.Name],
                        firstOperand.PhysicalRegister));
            }
            else
            {
                firstOperand.PhysicalRegister = pr.Value;
            }

            _marked[pr.Value] = true;
        }

        if (intermediateInstruction.SecondOperand is RegisterTerm secondOperand)
        {
            _vrToSr[secondOperand.VirtualRegister] = secondOperand.Name;

            var pr = _vrToPr[secondOperand.VirtualRegister];

            if (pr == null)
            {
                pr = GetAPr(
                    secondOperand.VirtualRegister,
                    secondOperand.NextUse);

                secondOperand.PhysicalRegister = pr.Value;

                AddInstruction(
                    GasSymbols.GenerateMovFromOffset(
                        _registerOffsetMap[secondOperand.Name],
                        secondOperand.PhysicalRegister));
            }
            else
            {
                secondOperand.PhysicalRegister = pr.Value;
            }

            _marked[pr.Value] = true;
        }

        if (intermediateInstruction.FirstOperand is RegisterTerm firstOperandNew)
        {
            if (firstOperandNew.NextUse == -1 && _prToVr[firstOperandNew.PhysicalRegister] != null)
            {
                FreeAPr(firstOperandNew.PhysicalRegister);
            }
        }

        if (intermediateInstruction.FirstOperand is RegisterTerm secondOperandNew)
        {
            if (secondOperandNew.NextUse == -1 && _prToVr[secondOperandNew.PhysicalRegister] != null)
            {
                FreeAPr(secondOperandNew.PhysicalRegister);
            }
        }

        foreach (var kvp in _marked)
        {
            _marked[kvp.Key] = false;
        }

        var lhs = intermediateInstruction.LeftHandSide;

        if (lhs != null)
        {
            // if (!string.IsNullOrEmpty(lhs.Identifier))
            // {
            //     var prev = _vrToSr
            //         .Where(x => x.Value == lhs.Name);
            //     
            //     foreach (var x in prev.Select(x => x.Key))
            //     {
            //         var pr = _vrToPr.GetValueOrDefault(x);
            //
            //         if (pr != null)
            //         {
            //             _vrToPr.Remove(x);
            //             _prToVr.Remove(pr.Value);
            //             _physicalRegistersStack.Push(pr.Value);
            //         }
            //     }
            //
            //     // if (prev.Key != null)
            //     // {
            //     //     var pr = _vrToPr[prev.Key];
            //     //
            //     //     if (pr != null)
            //     //     {
            //     //         _prToVr[pr.Value] = lhs.VirtualRegister;
            //     //         _vrToPr.Remove(prev.Key);
            //     //         _vrToPr[lhs.VirtualRegister] = pr;
            //     //     }
            //     // }
            // }

            _vrToSr[lhs.VirtualRegister] = lhs.Name;

            lhs.PhysicalRegister = GetAPr(
                lhs.VirtualRegister,
                lhs.NextUse);

            _marked[lhs.PhysicalRegister] = true;
        }


        switch (intermediateInstruction.InstructionType)
        {
            case InstructionType.Assignment:
                GenerateAssignment(intermediateInstruction);
                break;
            case InstructionType.Increment:
                GenerateIncrement(intermediateInstruction.FirstOperand);
                break;
            case InstructionType.Decrement:
                GenerateDecrement(intermediateInstruction.FirstOperand);
                break;
            case InstructionType.FunctionCall:
                GenerateFunctionCall(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Return:
                GenerateReturn(intermediateInstruction.FirstOperand);
                break;
            case InstructionType.AddInt:
                GenerateAdd(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.AddString:
                GenerateAddStrings(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Subtract:
                GenerateSubtract(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Multiply:
                GenerateMultiply(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Divide:
                GenerateDivide(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Modulo:
                GenerateModulo(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Equal:
                GenerateEqual(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.NotEqual:
                GenerateNotEqual(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Greater:
                GenerateRelOp(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide,
                    GasSymbols.GenerateSetGreaterToOffset,
                    GasSymbols.GenerateSetLessToOffset);
                break;
            case InstructionType.GreaterEqual:
                GenerateRelOp(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide,
                    GasSymbols.GenerateSetGreaterEqualToOffset,
                    GasSymbols.GenerateSetLessEqualToOffset);
                break;
            case InstructionType.Less:
                GenerateRelOp(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide,
                    GasSymbols.GenerateSetLessToOffset,
                    GasSymbols.GenerateSetGreaterToOffset);
                break;
            case InstructionType.LessEqual:
                GenerateRelOp(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide,
                    GasSymbols.GenerateSetLessEqualToOffset,
                    GasSymbols.GenerateSetGreaterEqualToOffset);
                break;
            case InstructionType.And:
                GenerateAnd(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.Or:
                GenerateOr(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.NegateInt:
                GenerateNegate(intermediateInstruction.FirstOperand, intermediateInstruction.LeftHandSide);
                break;
            case InstructionType.NegateBool:
                GenerateNot(intermediateInstruction.FirstOperand, intermediateInstruction.LeftHandSide);
                break;
        }
    }

    private void CompileIfIntermediateInstruction(IfIntermediateInstruction ifIntermediateInstruction)
    {
        var cmpInstruction = ifIntermediateInstruction.Condition;
        var opRegister = cmpInstruction as RegisterTerm;
        var opBool = cmpInstruction as ConstantBoolTerm;
        var opFunCall = cmpInstruction as FunctionCallTerm;
        var offset =
            opRegister != null ? _registerOffsetMap.GetValueOrDefault(opRegister.Name) : 0;

        if (opRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovzxFromOffset(offset, Register.RAX));
        }

        if (opBool != null)
        {
            AddInstruction(GasSymbols.GenerateMov(Convert.ToInt32(opBool.Value), Register.RAX));
        }

        if (opFunCall != null)
        {
            GenerateFunctionCall(opFunCall);
        }

        AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));

        AddInstruction(
            ifIntermediateInstruction.Negate
                ? GasSymbols.GenerateJumpEqual(ifIntermediateInstruction.JumpLabel.LabelTerm.Label)
                : GasSymbols.GenerateJumpNotEqual(ifIntermediateInstruction.JumpLabel.LabelTerm.Label));
    }

    private void GenerateAssignment(IntermediateInstruction intermediateInstruction)
    {
        if (intermediateInstruction.FirstOperand is ConstantIntTerm intTerm)
        {
            // SaveToVariable(intermediateInstruction.LeftHandSide, intTerm.Value);
            AddInstruction(
                GasSymbols.GenerateMov(intTerm.Value, intermediateInstruction.LeftHandSide.PhysicalRegister));
        }

        if (intermediateInstruction.FirstOperand is ConstantBoolTerm boolTerm)
        {
            // SaveToVariable(intermediateInstruction.LeftHandSide, Convert.ToInt32(boolTerm.Value), true);
            AddInstruction(
                GasSymbols.GenerateMov(
                    Convert.ToInt32(boolTerm.Value),
                    intermediateInstruction.LeftHandSide.PhysicalRegister));
        }

        if (intermediateInstruction.FirstOperand is ConstantStringTerm stringTerm)
        {
            var label = _literalsMap[stringTerm.Value];
            var targetOffset = _registerOffsetMap[intermediateInstruction.LeftHandSide.Name];
            AddInstruction(GasSymbols.GenerateLeaForLiteral(label, Register.RAX));
            AddInstruction(GasSymbols.GenerateMovToOffset(targetOffset, Register.RAX));
        }

        if (intermediateInstruction.FirstOperand is RegisterTerm registerTerm)
        {
            // var register = registerTerm.Type == LatteType.Boolean ? Register.AL : Register.RAX;
            // var sourceOffset = _registerOffsetMap[registerTerm.Name];
            // var targetOffset = _registerOffsetMap[intermediateInstruction.LeftHandSide.Name];
            // AddInstruction(GasSymbols.GenerateMovFromOffset(sourceOffset, register));
            // AddInstruction(GasSymbols.GenerateMovToOffset(targetOffset, register));

            AddInstruction(
                GasSymbols.GenerateMov(
                    registerTerm.PhysicalRegister,
                    intermediateInstruction.LeftHandSide.PhysicalRegister));
        }
    }

    private void SaveToVariable(RegisterTerm variable, int value, bool isBool = false)
    {
        var offset = _registerOffsetMap[variable.Name];

        AddInstruction(GasSymbols.GenerateConstantMovToMemory(offset, value, isBool));
    }

    private void GenerateIncrement(Term operand)
    {
        if (operand is not RegisterTerm registerTerm)
        {
            throw new Exception();
        }

        var offset = _registerOffsetMap[registerTerm.Name];
        AddInstruction(GasSymbols.GenerateIncrementToOffset(offset));
    }

    private void GenerateDecrement(Term operand)
    {
        if (operand is not RegisterTerm registerTerm)
        {
            throw new Exception();
        }

        var offset = _registerOffsetMap[registerTerm.Name];
        AddInstruction(GasSymbols.GenerateDecrementToOffset(offset));
    }

    private void GenerateFunctionCall(Term term, RegisterTerm target = null)
    {
        if (term is not FunctionCallTerm functionCallTerm)
        {
            throw new Exception();
        }

        // From actual registers in which params should be stored
        var tempRegisters = new List<Register>(GasSymbols.ParamRegisters);

        if (functionCallTerm.Arguments.Count > GasSymbols.ParamRegisters.Count)
        {
            // We fill with dummy registers which symbolize memory
            tempRegisters.AddRange(
                new List<Register>(
                    new Register[functionCallTerm.Arguments.Count - GasSymbols.ParamRegisters.Count]));
        }

        // We count how many elements we have to push on memory - these are from dummy registers
        var pushesCount = functionCallTerm.Arguments
            .Zip(tempRegisters)
            .Count(pair => pair.Second == Register.None);

        // Based on number of pushes we have to do, do we have to push dummy 8 byte word to
        // align stack?
        var shouldAlignStack = ((pushesCount * 8) + _stackAlignedTo) % 16 != 0;

        if (shouldAlignStack)
        {
            AddInstruction(GasSymbols.GeneratePush(0));
        }

        foreach (var (argument, register) in functionCallTerm.Arguments.Zip(tempRegisters))
        {
            switch (argument)
            {
                case ConstantIntTerm intTerm:
                    AddInstruction(
                        register == Register.None
                            ? GasSymbols.GeneratePush(intTerm.Value)
                            : GasSymbols.GenerateMov(intTerm.Value, register));
                    break;
                case ConstantBoolTerm boolTerm:
                    AddInstruction(
                        register == Register.None
                            ? GasSymbols.GeneratePush(Convert.ToInt32(boolTerm.Value))
                            : GasSymbols.GenerateMov(Convert.ToInt32(boolTerm.Value), register.GetLowByte()));
                    break;
                case ConstantStringTerm stringTerm:
                    if (register == Register.None)
                    {
                        AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[stringTerm.Value], Register.RAX));
                        AddInstruction(GasSymbols.GeneratePush(Register.RAX));
                    }
                    else
                    {
                        AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[stringTerm.Value], register));
                    }

                    break;
                case RegisterTerm registerTerm:
                {
                    if (!_vrToPr.TryGetValue(registerTerm.VirtualRegister, out var pr) || pr == null)
                    {
                        AddInstruction(
                            GasSymbols.GenerateMovFromOffset(_registerOffsetMap[registerTerm.Name], Register.RAX));
                        pr = Register.RAX;
                    }

                    // AddInstruction(GasSymbols.GenerateMov());

                    // var registerOffset = _registerOffsetMap[registerTerm.Name];
                    //
                    // if (registerTerm.Type == LatteType.Boolean)
                    // {
                    //     AddInstruction(GasSymbols.GenerateMovzxFromOffset(registerOffset, Register.RAX));
                    // }
                    // else
                    // {
                    //     AddInstruction(GasSymbols.GenerateMovFromOffset(registerOffset, Register.RAX));
                    // }

                    AddInstruction(
                        register == Register.None
                            ? GasSymbols.GeneratePush(pr.Value)
                            : GasSymbols.GenerateMov(pr.Value, register));
                    break;
                }
            }
        }

        AddInstruction(GasSymbols.GenerateFunctionCall(functionCallTerm.Name));

        var toSubtractFromStack = (pushesCount * 8) + (shouldAlignStack ? 8 : 0);

        if (toSubtractFromStack > 0)
        {
            AddInstruction(GasSymbols.GenerateAdd(Register.RSP, toSubtractFromStack));
        }

        if (target == null)
        {
            return;
        }

        var targetOffset = _registerOffsetMap[target.Name];

        AddInstruction(
            target.Type == LatteType.Boolean
                ? GasSymbols.GenerateMovToOffset(targetOffset, Register.AL)
                : GasSymbols.GenerateMovToOffset(targetOffset, Register.RAX));
    }

    private void GenerateReturn(Term term)
    {
        switch (term)
        {
            case ConstantIntTerm constantIntTerm:
                AddInstruction(GasSymbols.GenerateMov(constantIntTerm.Value, Register.RAX));
                break;
            case ConstantBoolTerm constantBoolTerm:
                AddInstruction(GasSymbols.GenerateMov(Convert.ToInt32(constantBoolTerm.Value), Register.RAX));
                break;
            case RegisterTerm registerTerm:
            {
                var offset = _registerOffsetMap[registerTerm.Name];

                AddInstruction(
                    registerTerm.Type == LatteType.Boolean
                        ? GasSymbols.GenerateMovzxFromOffset(offset, Register.RAX)
                        : GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
                break;
            }
        }

        AddFnEpilog();
    }

    private void GenerateAdd(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;

        if (leftRegister != null && rightRegister != null)
        {
            if (target.PhysicalRegister == leftRegister.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, rightRegister.PhysicalRegister));

                return;
            }

            if (target.PhysicalRegister == rightRegister.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, leftRegister.PhysicalRegister));

                return;
            }
            
            AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, Register.RAX));
            AddInstruction(GasSymbols.GenerateAdd(Register.RAX, rightRegister.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));

            return;
        }

        if (leftRegister != null)
        {
            if (leftRegister.PhysicalRegister == target.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateAdd(leftRegister.PhysicalRegister, rightInt.Value));

                return;
            }
            
            AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, rightInt.Value));

            return;
        }

        if (rightRegister.PhysicalRegister == target.PhysicalRegister)
        {
            AddInstruction(GasSymbols.GenerateAdd(rightRegister.PhysicalRegister, leftInt.Value));

            return;
        }
            
        AddInstruction(GasSymbols.GenerateMov(leftInt.Value, target.PhysicalRegister));
        AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, rightRegister.PhysicalRegister));
    }

    private void GenerateAddStrings(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftString = left as ConstantStringTerm;
        var rightString = right as ConstantStringTerm;
        var leftRegisterOffset =
            leftRegister != null ? _registerOffsetMap.GetValueOrDefault(leftRegister.Name) : 0;
        var rightRegisterOffset =
            rightRegister != null ? _registerOffsetMap.GetValueOrDefault(rightRegister.Name) : 0;
        var targetRegisterOffset =
            target != null ? _registerOffsetMap.GetValueOrDefault(target.Name) : 0;

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RDI));
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RSI));
        }
        else if (leftRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RDI));
            AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[rightString.Value], Register.RSI));
        }
        else if (rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[leftString.Value], Register.RDI));
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RSI));
        }

        AddInstruction(GasSymbols.GenerateFunctionCall("concatStrings"));
        AddInstruction(GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));
    }

    private void GenerateSubtract(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;

        if (leftRegister != null && rightRegister != null)
        {
            if (target.PhysicalRegister == leftRegister.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, rightRegister.PhysicalRegister));

                return;
            }

            if (target.PhysicalRegister == rightRegister.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, leftRegister.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateNegation(target.PhysicalRegister));

                return;
            }
            
            AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, Register.RAX));
            AddInstruction(GasSymbols.GenerateSubtract(Register.RAX, rightRegister.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));

            return;
        }

        if (leftRegister != null)
        {
            if (leftRegister.PhysicalRegister == target.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateSubtract(leftRegister.PhysicalRegister, rightInt.Value));

                return;
            }
            
            AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, rightInt.Value));

            return;
        }

        if (rightRegister.PhysicalRegister == target.PhysicalRegister)
        {
            AddInstruction(GasSymbols.GenerateSubtract(rightRegister.PhysicalRegister, leftInt.Value));
            AddInstruction(GasSymbols.GenerateNegation(rightRegister.PhysicalRegister));

            return;
        }
            
        AddInstruction(GasSymbols.GenerateMov(leftInt.Value, target.PhysicalRegister));
        AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, rightRegister.PhysicalRegister));
    }

    private void GenerateMultiply(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;

        if (leftRegister != null && rightRegister != null)
        {
            if (target.PhysicalRegister == leftRegister.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, rightRegister.PhysicalRegister));

                return;
            }

            if (target.PhysicalRegister == rightRegister.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, leftRegister.PhysicalRegister));

                return;
            }
            
            AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, Register.RAX));
            AddInstruction(GasSymbols.GenerateMultiply(Register.RAX, rightRegister.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));

            return;
        }

        if (leftRegister != null)
        {
            if (leftRegister.PhysicalRegister == target.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateMultiply(leftRegister.PhysicalRegister, rightInt.Value));

                return;
            }
            
            AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, rightInt.Value));

            return;
        }

        if (rightRegister.PhysicalRegister == target.PhysicalRegister)
        {
            AddInstruction(GasSymbols.GenerateMultiply(rightRegister.PhysicalRegister, leftInt.Value));

            return;
        }
            
        AddInstruction(GasSymbols.GenerateMov(leftInt.Value, target.PhysicalRegister));
        AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, rightRegister.PhysicalRegister));
    }

    private void GenerateDivide(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightRegister.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));

            return;
        }

        if (leftRegister != null)
        {
            AddInstruction(GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightInt.Value));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));

            return;
        }

        AddInstruction(GasSymbols.GenerateDivide(leftInt.Value, rightRegister.PhysicalRegister));
        AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
    }

    private void GenerateModulo(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightRegister.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMov(Register.RDX, target.PhysicalRegister));

            return;
        }

        if (leftRegister != null)
        {
            AddInstruction(GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightInt.Value));
            AddInstruction(GasSymbols.GenerateMov(Register.RDX, target.PhysicalRegister));

            return;
        }

        AddInstruction(GasSymbols.GenerateDivide(leftInt.Value, rightRegister.PhysicalRegister));
        AddInstruction(GasSymbols.GenerateMov(Register.RDX, target.PhysicalRegister));
    }

    private void GenerateEqual(Term left, Term right, RegisterTerm target) =>
        GenerateEqualityComparison(left, right, target, true);

    private void GenerateNotEqual(Term left, Term right, RegisterTerm target) =>
        GenerateEqualityComparison(left, right, target, false);

    private void GenerateEqualityComparison(Term left, Term right, RegisterTerm target, bool equal)
    {
        if (left is not RegisterTerm && right is RegisterTerm)
        {
            (left, right) = (right, left);
        }

        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var rightInt = right as ConstantIntTerm;
        var rightBool = right as ConstantBoolTerm;
        var rightString = right as ConstantStringTerm;
        var leftRegisterOffset =
            leftRegister != null ? _registerOffsetMap.GetValueOrDefault(leftRegister.Name) : 0;
        var rightRegisterOffset =
            rightRegister != null ? _registerOffsetMap.GetValueOrDefault(rightRegister.Name) : 0;
        var targetRegisterOffset = _registerOffsetMap.GetValueOrDefault(target.Name);

        if (leftRegister != null && rightRegister != null)
        {
            var firstRegister = leftRegister.Type == LatteType.Boolean ? Register.AL : Register.RDI;
            var secondRegister = leftRegister.Type == LatteType.Boolean ? Register.DIL : Register.RSI;

            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, firstRegister));
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, secondRegister));

            if (leftRegister.Type == LatteType.String)
            {
                AddInstruction(GasSymbols.GenerateFunctionCall("compareStrings"));
                AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));

                AddInstruction(
                    equal
                        ? GasSymbols.GenerateSetNotEqualToOffset(targetRegisterOffset)
                        : GasSymbols.GenerateSetEqualToOffset(targetRegisterOffset));

                return;
            }

            AddInstruction(GasSymbols.GenerateCmp(firstRegister, secondRegister));
            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetEqualToOffset(targetRegisterOffset)
                    : GasSymbols.GenerateSetNotEqualToOffset(targetRegisterOffset));

            return;
        }

        if (leftRegister == null)
        {
            return;
        }

        if (leftRegister.Type == LatteType.String)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RDI));
            AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[rightString.Value], Register.RSI));

            AddInstruction(GasSymbols.GenerateFunctionCall("compareStrings"));
            AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));
            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetNotEqualToOffset(targetRegisterOffset)
                    : GasSymbols.GenerateSetEqualToOffset(targetRegisterOffset));

            return;
        }

        var register = leftRegister.Type == LatteType.Boolean ? Register.AL : Register.RAX;
        var value = rightInt?.Value ?? Convert.ToInt32(rightBool.Value);

        AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, register));
        AddInstruction(GasSymbols.GenerateCmp(register, value));
        AddInstruction(
            equal
                ? GasSymbols.GenerateSetEqualToOffset(targetRegisterOffset)
                : GasSymbols.GenerateSetNotEqualToOffset(targetRegisterOffset));
    }

    private void GenerateOr(Term left, Term right, RegisterTerm target) =>
        GenerateBinaryBoolOp(
            left,
            right,
            target,
            GasSymbols.GenerateOr,
            GasSymbols.GenerateOr);

    private void GenerateAnd(Term left, Term right, RegisterTerm target) =>
        GenerateBinaryBoolOp(
            left,
            right,
            target,
            GasSymbols.GenerateAnd,
            GasSymbols.GenerateAnd);

    private void GenerateBinaryBoolOp(
        Term left,
        Term right,
        RegisterTerm target,
        Func<Register, Register, string> registerFunc,
        Func<Register, int, string> registerValueFunc)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftBool = left as ConstantBoolTerm;
        var rightBool = right as ConstantBoolTerm;
        var leftRegisterOffset =
            leftRegister != null ? _registerOffsetMap.GetValueOrDefault(leftRegister.Name) : 0;
        var rightRegisterOffset =
            rightRegister != null ? _registerOffsetMap.GetValueOrDefault(rightRegister.Name) : 0;
        var targetRegisterOffset = _registerOffsetMap.GetValueOrDefault(target.Name);

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.AL));
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.DIL));
            AddInstruction(registerFunc(Register.AL, Register.DIL));
            AddInstruction(GasSymbols.GenerateSetNotEqualToOffset(targetRegisterOffset));

            return;
        }

        if (leftRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.AL));
            AddInstruction(registerValueFunc(Register.AL, Convert.ToInt32(rightBool.Value)));
            AddInstruction(GasSymbols.GenerateSetNotEqualToOffset(targetRegisterOffset));

            return;
        }

        if (rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.AL));
            AddInstruction(registerValueFunc(Register.AL, Convert.ToInt32(leftBool.Value)));
            AddInstruction(GasSymbols.GenerateSetNotEqualToOffset(targetRegisterOffset));
        }
    }

    private void GenerateRelOp(
        Term left,
        Term right,
        RegisterTerm target,
        Func<int, string> generate,
        Func<int, string> generateReverse)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftRegisterOffset =
            leftRegister != null ? _registerOffsetMap.GetValueOrDefault(leftRegister.Name) : 0;
        var rightRegisterOffset =
            rightRegister != null ? _registerOffsetMap.GetValueOrDefault(rightRegister.Name) : 0;
        var targetRegisterOffset = _registerOffsetMap.GetValueOrDefault(target.Name);

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RAX));
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RDI));
            AddInstruction(GasSymbols.GenerateCmp(Register.RAX, Register.RDI));
            AddInstruction(generate(targetRegisterOffset));

            return;
        }

        if (leftRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RAX));
            AddInstruction(GasSymbols.GenerateCmp(Register.RAX, rightInt.Value));
            AddInstruction(generate(targetRegisterOffset));

            return;
        }

        if (rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RAX));
            AddInstruction(GasSymbols.GenerateCmp(Register.RAX, leftInt.Value));
            AddInstruction(generateReverse(targetRegisterOffset));
        }
    }

    private void GenerateNegate(Term term, RegisterTerm target)
    {
        if (term is not RegisterTerm registerTerm)
        {
            return;
        }

        var offset = _registerOffsetMap[registerTerm.Name];
        var targetOffset = _registerOffsetMap[target.Name];

        AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
        AddInstruction(GasSymbols.GenerateNegation(Register.RAX));
        AddInstruction(GasSymbols.GenerateMovToOffset(targetOffset, Register.RAX));
    }

    private void GenerateNot(Term term, RegisterTerm target)
    {
        if (term is not RegisterTerm registerTerm)
        {
            return;
        }

        var offset = _registerOffsetMap[registerTerm.Name];
        var targetOffset = _registerOffsetMap[target.Name];

        AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.DIL));
        AddInstruction(GasSymbols.GenerateNot(Register.DIL));
        AddInstruction(GasSymbols.GenerateMovToOffset(targetOffset, Register.DIL));
    }

    private void AddInstruction(string instruction) => _result.Add(instruction);

    private void AddStringLiteral(string literal)
    {
        if (_literalsMap.ContainsKey(literal))
        {
            return;
        }

        var label = $"str{_literalsMap.Count}";
        _literalsMap[literal] = label;
        AddInstruction($"{label}:");
        AddInstruction($".string \"{literal}\"");
    }

    private void AddFnProlog()
    {
        AddInstruction(GasSymbols.GeneratePush(Register.RBP));
        AddInstruction(GasSymbols.GeneratePush(Register.RBX));
        AddInstruction(GasSymbols.GeneratePush(Register.R12));
        AddInstruction(GasSymbols.GeneratePush(Register.R13));
        AddInstruction(GasSymbols.GeneratePush(Register.R14));
        AddInstruction(GasSymbols.GeneratePush(Register.R15));
        AddInstruction(GasSymbols.GenerateMov(Register.RSP, Register.RBP));
    }

    private void AddFnEpilog()
    {
        AddInstruction(GasSymbols.GenerateMov(Register.RBP, Register.RSP));
        AddInstruction(GasSymbols.GeneratePop(Register.R15));
        AddInstruction(GasSymbols.GeneratePop(Register.R14));
        AddInstruction(GasSymbols.GeneratePop(Register.R13));
        AddInstruction(GasSymbols.GeneratePop(Register.R12));
        AddInstruction(GasSymbols.GeneratePop(Register.RBX));
        AddInstruction(GasSymbols.GeneratePop(Register.RBP));
        AddInstruction(GasSymbols.GenerateRet());
    }

    private Register GetAPr(string vr, int nu)
    {
        Register result;

        if (_physicalRegistersStack.Any())
        {
            result = _physicalRegistersStack.Pop();
        }
        else
        {
            result = _prNu.Where(x => x.Value == -1).Select(x => x.Key).FirstOrDefault(x => !_marked[x]);

            if (result == Register.None)
            {
                result = _prNu.OrderByDescending(x => x.Value).FirstOrDefault(x => !_marked[x.Key]).Key;
            }

            var spilledVr = _prToVr[result];
            var sr = _vrToSr[spilledVr];

            if (!_registerOffsetMap.ContainsKey(sr))
            {
                // AddInstruction(GasSymbols.GenerateSubtract(Register.RSP, 8));
                AddInstruction(GasSymbols.GeneratePush(result));
                _registerOffsetMap[sr] = _registerOffsetMap.Values.Min() - 8;
            }
            else
            {
                AddInstruction(GasSymbols.GenerateMovToOffset(_registerOffsetMap[sr], result));
            }

            _vrToPr[spilledVr] = null;
        }

        _vrToPr[vr] = result;
        _prToVr[result] = vr;
        _prNu[result] = nu;

        return result;
    }

    private void FreeAPr(Register pr)
    {
        _vrToPr[_prToVr[pr]] = null;
        _prToVr[pr] = null;
        _prNu[pr] = -1;
        _physicalRegistersStack.Push(pr);
    }
}