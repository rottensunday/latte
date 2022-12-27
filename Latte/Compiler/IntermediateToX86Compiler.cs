namespace Latte.Compiler;

using Latte.Models.Intermediate;

public class IntermediateToX86Compiler
{
    private readonly List<Register> _paramRegisters = new()
    {
        Register.RDI,
        Register.RSI,
        Register.RDX,
        Register.RCX,
        Register.R8,
        Register.R9
    };

    private readonly List<InstructionType> _intComparisons = new()
    {
        InstructionType.Equal,
        InstructionType.NotEqual,
        InstructionType.Greater,
        InstructionType.GreaterEqual,
        InstructionType.Less,
        InstructionType.LessEqual
    };

    private readonly Dictionary<RegisterTerm, int> _registerOffsetMap = new();
    private readonly List<string> _result = new();

    public List<string> Compile(List<IntermediateFunction> functions)
    {
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
    }

    private void Reset()
    {
        _registerOffsetMap.Clear();
    }

    private void CompileInstructions(
        List<BaseIntermediateInstruction> instructions,
        List<RegisterTerm> variables)
    {
        AddInstruction(GasSymbols.GeneratePush(Register.RBP));
        AddInstruction(GasSymbols.GenerateMov(Register.RSP, Register.RBP));

        var parameters = variables.Where(x => x.IsParam).ToList();
        parameters = parameters
            .Take(_paramRegisters.Count)
            .Concat(parameters.Skip(_paramRegisters.Count).Reverse())
            .ToList();
        var tempRegisters = new List<Register>(_paramRegisters);

        if (parameters.Count > _paramRegisters.Count)
        {
            tempRegisters.AddRange(new List<Register>(new Register[parameters.Count - _paramRegisters.Count]));
        }

        var minusOffset = 0;
        var plusOffset = 8;

        foreach (var (parameter, register) in parameters.Zip(tempRegisters))
        {
            if (register == Register.None)
            {
                plusOffset += 8;
                _registerOffsetMap[parameter] = plusOffset;
            }
            else
            {
                minusOffset -= 8;
                AddInstruction(GasSymbols.GenerateMovToOffset(minusOffset, register));
                _registerOffsetMap[parameter] = minusOffset;
            }
        }

        var registersNeeded = instructions
            .OfType<IntermediateInstruction>()
            .SelectMany(x => new[] { x.LeftHandSide, x.FirstOperand as RegisterTerm, x.SecondOperand as RegisterTerm })
            .Where(x => x != null)
            .Except(parameters)
            .Distinct();


        foreach (var registerNeeded in registersNeeded)
        {
            minusOffset -= 8;
            _registerOffsetMap.Add(registerNeeded, minusOffset);
        }

        AddInstruction(GasSymbols.GenerateSubtract(Register.RSP, -minusOffset));

        foreach (var instruction in instructions)
        {
            if (instruction is IntermediateInstruction { InstructionType: InstructionType.Return })
            {
                AddInstruction(GasSymbols.GenerateMov(Register.RBP, Register.RSP));
                AddInstruction(GasSymbols.GeneratePop(Register.RBP));
            }

            CompileInstruction(instruction, variables);
        }
    }

    private void CompileInstruction(
        BaseIntermediateInstruction instruction,
        List<RegisterTerm> variables)
    {
        if (instruction is LabelIntermediateInstruction labelIntermediateInstruction)
        {
            AddInstruction(
                labelIntermediateInstruction.IsJump
                    ? GasSymbols.GenerateUnconditionalJump(labelIntermediateInstruction.LabelTerm.Label)
                    : GasSymbols.GenerateLabel(labelIntermediateInstruction.LabelTerm.Label));
        }

        if (instruction is IntermediateInstruction intermediateInstruction)
        {
            switch (intermediateInstruction.InstructionType)
            {
                case InstructionType.Assignment:
                    if (intermediateInstruction.FirstOperand is ConstantIntTerm intTerm)
                    {
                        SaveToVariable(intermediateInstruction.LeftHandSide, intTerm.Value);
                    }

                    if (intermediateInstruction.FirstOperand is ConstantBoolTerm boolTerm)
                    {
                        SaveToVariable(intermediateInstruction.LeftHandSide, Convert.ToInt32(boolTerm.Value));
                    }
                    else if (intermediateInstruction.FirstOperand is RegisterTerm registerTerm)
                    {
                        var sourceOffset = _registerOffsetMap[registerTerm];
                        var targetOffset = _registerOffsetMap[intermediateInstruction.LeftHandSide];
                        AddInstruction(GasSymbols.GenerateMovFromOffset(sourceOffset, Register.RAX));
                        AddInstruction(GasSymbols.GenerateMovToOffset(targetOffset, Register.RAX));
                    }

                    break;
                case InstructionType.Increment:
                    GenerateIncrement(intermediateInstruction.FirstOperand);
                    break;
                case InstructionType.Decrement:
                    GenerateDecrement(intermediateInstruction.FirstOperand);
                    break;
                case InstructionType.FunctionCall:
                    GenerateFunctionCall(intermediateInstruction.FirstOperand);
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
                    
                case InstructionType.NegateInt:
                    GenerateNegate(intermediateInstruction.FirstOperand, intermediateInstruction.LeftHandSide);
                    break;
                case InstructionType.NegateBool:
                    GenerateNot(intermediateInstruction.FirstOperand, intermediateInstruction.LeftHandSide);
                    break;
            }
        }

        if (instruction is IfIntermediateInstruction ifIntermediateInstruction)
        {
            var cmpInstruction = ifIntermediateInstruction.ConditionInstruction;
            var leftOpRegister = cmpInstruction.FirstOperand as RegisterTerm;
            var rightOpRegister = cmpInstruction.SecondOperand as RegisterTerm;
            var leftOpInt = cmpInstruction.FirstOperand as ConstantIntTerm;
            var rightOpInt = cmpInstruction.SecondOperand as ConstantIntTerm;
            var leftOpBool = cmpInstruction.FirstOperand as ConstantBoolTerm;
            var rightOpBool = cmpInstruction.SecondOperand as ConstantBoolTerm;
            var leftOffset = 
                leftOpRegister != null ? _registerOffsetMap.GetValueOrDefault(leftOpRegister) : 0;
            var rightOffset = 
                rightOpRegister != null ? _registerOffsetMap.GetValueOrDefault(rightOpRegister) : 0;

            if (leftOpRegister != null && rightOpRegister != null)
            {
                AddInstruction(GasSymbols.GenerateMovFromOffset(leftOffset, Register.RAX));
                AddInstruction(GasSymbols.GenerateMovFromOffset(rightOffset, Register.RDI));

                if (_intComparisons.Contains(cmpInstruction.InstructionType))
                {
                    AddInstruction(GasSymbols.GenerateCmp(Register.RAX, Register.RDI));
                }
                else
                {
                    if (cmpInstruction.InstructionType == InstructionType.And)
                    {
                        AddInstruction(GasSymbols.GenerateAnd(Register.RAX, Register.RDI));
                    }

                    if (cmpInstruction.InstructionType == InstructionType.Or)
                    {
                        AddInstruction(GasSymbols.GenerateOr(Register.RAX, Register.RDI));
                    }
                }
                
                GenerateInverseJump(cmpInstruction.InstructionType, ifIntermediateInstruction.JumpLabel.LabelTerm.Label);

                return;
            }

            if (leftOpRegister != null)
            {
                if (rightOpInt != null)
                {
                    AddInstruction(GasSymbols.GenerateMovFromOffset(leftOffset, Register.RAX));
                    
                    AddInstruction(GasSymbols.GenerateCmp(Register.RAX, rightOpInt.Value));
                    GenerateInverseJump(cmpInstruction.InstructionType, ifIntermediateInstruction.JumpLabel.LabelTerm.Label);

                    return;
                }
            }

            if (rightOpRegister != null)
            {
                if (leftOpInt != null)
                {
                    AddInstruction(GasSymbols.GenerateMovFromOffset(rightOffset, Register.RAX));
                    
                    AddInstruction(GasSymbols.GenerateCmp(Register.RAX, rightOpInt.Value));
                    GenerateInverseJump(cmpInstruction.InstructionType, ifIntermediateInstruction.JumpLabel.LabelTerm.Label);

                    return;
                }
            }
            
            
        }
    }

    private void GenerateInverseJump(InstructionType instructionType, string label)
    {
        switch (instructionType)
        {
            case InstructionType.Equal:
                AddInstruction(GasSymbols.GenerateJumpNotEqual(label));
                break;
            case InstructionType.NotEqual:
                AddInstruction(GasSymbols.GenerateJumpEqual(label));
                break;
            case InstructionType.Greater:
                AddInstruction(GasSymbols.GenerateJumpLessEqual(label));
                break;
            case InstructionType.GreaterEqual:
                AddInstruction(GasSymbols.GenerateJumpLess(label));
                break;
            case InstructionType.Less:
                AddInstruction(GasSymbols.GenerateJumpGreaterEqual(label));
                break;
            case InstructionType.LessEqual:
                AddInstruction(GasSymbols.GenerateJumpGreater(label));
                break;
            case InstructionType.And:
                AddInstruction(GasSymbols.GenerateJumpEqual(label));
                break;
            case InstructionType.Or:
                AddInstruction(GasSymbols.GenerateJumpEqual(label));
                break;
        }
    }

    private void SaveToVariable(RegisterTerm variable, int value)
    {
        var offset = _registerOffsetMap[variable];

        AddInstruction(GasSymbols.GenerateConstantMovToMemory(offset, value));
    }

    private void GenerateIncrement(Term operand)
    {
        if (operand is not RegisterTerm registerTerm)
        {
            throw new Exception();
        }

        var offset = _registerOffsetMap[registerTerm];

        AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
        AddInstruction(GasSymbols.GenerateIncrement(Register.RAX));
        AddInstruction(GasSymbols.GenerateMovToOffset(offset, Register.RAX));
    }

    private void GenerateDecrement(Term operand)
    {
        if (operand is not RegisterTerm registerTerm)
        {
            throw new Exception();
        }

        var offset = _registerOffsetMap[registerTerm];

        AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
        AddInstruction(GasSymbols.GenerateDecrement(Register.RAX));
        AddInstruction(GasSymbols.GenerateMovToOffset(offset, Register.RAX));
    }

    private void GenerateFunctionCall(Term term)
    {
        if (term is not FunctionCallTerm functionCallTerm)
        {
            throw new Exception();
        }

        var tempRegisters = new List<Register>(_paramRegisters);

        if (functionCallTerm.Arguments.Count > _paramRegisters.Count)
        {
            tempRegisters.AddRange(
                new List<Register>(
                    new Register[functionCallTerm.Arguments.Count - _paramRegisters.Count]));
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
                            : GasSymbols.GenerateMov(Convert.ToInt32(boolTerm.Value), register));
                    break;
                case RegisterTerm registerTerm:
                {
                    var offset = _registerOffsetMap[registerTerm];
                    AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));

                    AddInstruction(
                        register == Register.None
                            ? GasSymbols.GeneratePush(Register.RAX)
                            : GasSymbols.GenerateMov(Register.RAX, register));
                    break;
                }
            }
        }

        AddInstruction(GasSymbols.GenerateFunctionCall(functionCallTerm.Name));
    }

    private void GenerateReturn(Term term)
    {
        if (term is ConstantIntTerm constantIntTerm)
        {
            AddInstruction(GasSymbols.GenerateMov(constantIntTerm.Value, Register.RAX));
        }

        if (term is RegisterTerm registerTerm)
        {
            var offset = _registerOffsetMap[registerTerm];
            AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
        }

        AddInstruction(GasSymbols.GenerateRet());
    }

    private void GenerateAdd(Term left, Term right, RegisterTerm target)
    {
        GenerateBinOp(left, right, target, GasSymbols.GenerateAdd, GasSymbols.GenerateAdd);
    }

    private void GenerateSubtract(Term left, Term right, RegisterTerm target)
    {
        GenerateBinOp(left, right, target, GasSymbols.GenerateSubtract, GasSymbols.GenerateSubtract, true);
    }

    private void GenerateMultiply(Term left, Term right, RegisterTerm target)
    {
        GenerateBinOp(left, right, target, GasSymbols.GenerateMultiply, GasSymbols.GenerateMultiply);
    }

    private void GenerateDivide(Term left, Term right, RegisterTerm target)
    {
        GenerateBinOp(left, right, target, GasSymbols.GenerateDivide, GasSymbols.GenerateDivide, divide: true);
    }

    private void GenerateModulo(Term left, Term right, RegisterTerm target)
    {
        GenerateBinOp(left, right, target, GasSymbols.GenerateDivide, GasSymbols.GenerateDivide, modulo: true);
    }

    private void GenerateEqual(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftRegisterOffset =
            leftRegister != null ? _registerOffsetMap.GetValueOrDefault(leftRegister) : 0;
        var rightRegisterOffset =
            rightRegister != null ? _registerOffsetMap.GetValueOrDefault(rightRegister) : 0;
        var targetRegisterOffset =
            target != null ? _registerOffsetMap.GetValueOrDefault(target) : 0;
        
        
    }

    private void GenerateNegate(Term term, RegisterTerm target)
    {
        if (term is RegisterTerm registerTerm)
        {
            var offset = _registerOffsetMap[registerTerm];
            var targetOffset = _registerOffsetMap[target];
            AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
            AddInstruction(GasSymbols.GenerateNegation(Register.RAX));
            AddInstruction(GasSymbols.GenerateMovToOffset(targetOffset, Register.RAX));
        }
    }

    private void GenerateNot(Term term, RegisterTerm target)
    {
        if (term is RegisterTerm registerTerm)
        {
            var offset = _registerOffsetMap[registerTerm];
            var targetOffset = _registerOffsetMap[target];
            AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
            AddInstruction(GasSymbols.GenerateNot(Register.RAX));
            AddInstruction(GasSymbols.GenerateMovToOffset(targetOffset, Register.RAX));
        }
    }

    private void GenerateBinOp(
        Term left,
        Term right,
        RegisterTerm target,
        Func<Register, Register, string> twoRegisterFun,
        Func<Register, int, string> registerAndParamFun,
        bool negate = false,
        bool divide = false,
        bool modulo = false)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftRegisterOffset =
            leftRegister != null ? _registerOffsetMap.GetValueOrDefault(leftRegister) : 0;
        var rightRegisterOffset =
            rightRegister != null ? _registerOffsetMap.GetValueOrDefault(rightRegister) : 0;
        var targetRegisterOffset =
            target != null ? _registerOffsetMap.GetValueOrDefault(target) : 0;

        if (leftRegister != null && rightRegister != null)
        {
            if (target == leftRegister)
            {
                AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RAX));
                AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RDI));

                AddInstruction(twoRegisterFun(Register.RAX, Register.RDI));

                AddInstruction(
                    modulo
                        ? GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDX)
                        : GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));

                return;
            }

            if (target == rightRegister)
            {
                AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RAX));
                AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RDI));

                AddInstruction(twoRegisterFun(Register.RAX, Register.RDI));

                AddInstruction(
                    modulo
                        ? GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDX)
                        : GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));

                return;
            }

            AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RAX));
            AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RDI));

            AddInstruction(twoRegisterFun(Register.RAX, Register.RDI));

            AddInstruction(
                modulo
                    ? GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDX)
                    : GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));

            return;
        }

        if (leftRegister != null)
        {
            if (right != null)
            {
                if (divide || modulo)
                {
                    AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RAX));
                    AddInstruction(GasSymbols.GenerateMov(rightInt.Value, Register.RDI));
                    AddInstruction(twoRegisterFun(Register.RAX, Register.RDI));

                    AddInstruction(
                        modulo
                            ? GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDX)
                            : GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));
                }
                else
                {
                    AddInstruction(GasSymbols.GenerateMovFromOffset(leftRegisterOffset, Register.RAX));
                    AddInstruction(registerAndParamFun(Register.RAX, rightInt.Value));
                    AddInstruction(GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));
                }

                return;
            }
        }

        if (rightRegister != null)
        {
            if (left != null)
            {
                AddInstruction(GasSymbols.GenerateMovFromOffset(rightRegisterOffset, Register.RDI));

                if (divide || modulo)
                {
                    AddInstruction(GasSymbols.GenerateMov(leftInt.Value, Register.RAX));
                    AddInstruction(GasSymbols.GenerateDivide(Register.RAX, Register.RDI));
                    AddInstruction(
                        modulo
                            ? GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDX)
                            : GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));
                }
                else
                {
                    AddInstruction(registerAndParamFun(Register.RDI, leftInt.Value));

                    if (negate)
                    {
                        AddInstruction(GasSymbols.GenerateNegation(Register.RDI));
                    }

                    AddInstruction(
                        modulo
                            ? GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDX)
                            : GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDI));
                }

                return;
            }
        }

        if (leftInt != null && rightInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(leftInt.Value, Register.RAX));
            AddInstruction(GasSymbols.GenerateMov(rightInt.Value, Register.RDI));
            AddInstruction(twoRegisterFun(Register.RAX, Register.RDI));

            AddInstruction(
                modulo
                    ? GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RDX)
                    : GasSymbols.GenerateMovToOffset(targetRegisterOffset, Register.RAX));

            return;
        }

        throw new Exception();
    }

    private void AddInstruction(string instruction)
    {
        _result.Add(instruction);
    }
}
