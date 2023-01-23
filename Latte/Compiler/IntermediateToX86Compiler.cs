namespace Latte.Compiler;

using Latte.Models.Intermediate;
using Listeners;
using Scopes;

public class IntermediateToX86Compiler
{
    private readonly Dictionary<string, string> _literalsMap = new();
    private readonly Dictionary<string, int> _registerOffsetMap = new();

    private List<ClassSymbol> _classes;
    private Dictionary<Register, bool> _marked = new();
    private int _minusOffset;
    private Stack<Register> _physicalRegistersStack = new();

    private int _previousBlock = -3;
    private HashSet<Register> _prevRegistersUsed = new();
    private Dictionary<Register, int> _prNu = new();
    private Dictionary<Register, string> _prToVr = new();
    private int _pushedRegistersOffset;

    private int _pushes;

    private HashSet<Register> _registersUsed = new();
    private List<string> _result = new();

    private int _stackAlignedTo;

    private Dictionary<string, Register?> _vrToPr = new();
    private Dictionary<string, string> _vrToSr = new();

    public IEnumerable<string> Compile(
        List<IntermediateFunction> functions,
        List<ClassSymbol> classes)
    {
        _classes = classes;
        var result = new List<string>();

        var literals = functions
            .SelectMany(x => x.Instructions)
            .SelectMany(x => x.GetStringLiterals());

        foreach (var literal in literals)
        {
            result.AddRange(AddStringLiteral(literal));
        }

        foreach (var function in functions)
        {
            Reset();
            CompileFunction(function);
            Reset();
            result.AddRange(CompileFunction(function));
        }

        return result;
    }

    private List<string> CompileFunction(IntermediateFunction function)
    {
        _result = new List<string>();
        _pushes = 0;
        // AddInstruction(GasSymbols.GenerateFunctionSymbol(function.Name));
        CompileInstructions(function.Instructions, function.Variables, function.Blocks);

        _result = new List<string> { GasSymbols.GenerateFunctionSymbol(function.Name) }.Concat(AddFnProlog())
            .Concat(_result).ToList();

        if (_result.Last().Contains("RET"))
        {
            return _result;
        }

        AddFnEpilog();

        return _result;
    }

    private void Reset() => _registerOffsetMap.Clear();

    private void CompileInstructions(
        List<BaseIntermediateInstruction> instructions,
        List<RegisterTerm> variables,
        List<Block> blocks)
    {
        // AddFnProlog();
        _pushedRegistersOffset = _registersUsed.Intersect(GasSymbols.PreservedRegisters).Count() * 8;

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

                AddInstruction(GasSymbols.GenerateMovToOffset(minusOffset - _pushedRegistersOffset, register));

                _registerOffsetMap[parameter.Name] = minusOffset - _pushedRegistersOffset;
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

            _registerOffsetMap[registerNeeded.Name] = minusOffset - _pushedRegistersOffset;
        }

        minusOffset = -minusOffset;
        // We need to round up offset to closest number divisible by 8
        minusOffset += (8 - (minusOffset % 8)) % 8;

        _minusOffset = minusOffset;

        if (minusOffset != 0)
        {
            AddInstruction(GasSymbols.GenerateSubtract(Register.RSP, minusOffset));
        }

        _stackAlignedTo = (plusOffset - 8 + minusOffset + _pushedRegistersOffset) % 16;

        var previousInBool = false;
        var first = true;
        _prevRegistersUsed = _registersUsed;
        _registersUsed = new HashSet<Register>();

        foreach (var instruction in instructions)
        {
            if (!first && !previousInBool && instruction.InBoolExpr)
            {
                foreach (var kvp in _prToVr)
                {
                    if (kvp.Value != null)
                    {
                        var sr = _vrToSr[kvp.Value];

                        if (instruction is LabelIntermediateInstruction)
                        {
                            if (blocks[instruction.Block - 1].LiveOut.Contains(sr) &&
                                _registerOffsetMap.TryGetValue(sr, out var value))
                            {
                                AddInstruction(GasSymbols.GenerateMovToOffset(value, kvp.Key));
                            }
                        }
                        else
                        {
                            if (blocks[instruction.Block].LiveOut.Contains(sr) && _registerOffsetMap.ContainsKey(sr))
                            {
                                AddInstruction(GasSymbols.GenerateMovToOffset(_registerOffsetMap[sr], kvp.Key));
                            }
                        }
                    }
                }
            }

            first = false;

            if (_previousBlock != instruction.Block)
            {
                _previousBlock = instruction.Block;

                PreprocessBlock(
                    instructions.Where(x => x.Block == instruction.Block).ToList());
            }

            CompileInstruction(instruction, blocks[instruction.Block]);

            previousInBool = instruction.InBoolExpr;
        }

        _previousBlock++;
    }

    private void PreprocessBlock(List<BaseIntermediateInstruction> instructions)
    {
        _vrToPr = new Dictionary<string, Register?>();
        _prToVr = new Dictionary<Register, string>();
        _prNu = new Dictionary<Register, int>();
        _physicalRegistersStack = new Stack<Register>();
        _marked = new Dictionary<Register, bool>();
        _vrToSr = new Dictionary<string, string>();

        var registersUsed =
            instructions
                .SelectMany(x => x.GetOperands().Concat(new List<RegisterTerm> { x.GetTarget() }))
                .Where(x => x != null)
                .Where(x => x.VirtualRegister != null);

        foreach (var registerUsed in registersUsed)
        {
            _vrToPr[registerUsed.VirtualRegister] = null;
        }

        foreach (var physicalRegister in GasSymbols.AllocationRegisters)
        {
            _prToVr[physicalRegister] = null;
            _prNu[physicalRegister] = -1;
            _physicalRegistersStack.Push(physicalRegister);
            _marked[physicalRegister] = false;
        }
    }

    private void CompileInstruction(BaseIntermediateInstruction instruction, Block block)
    {
        switch (instruction)
        {
            case LabelIntermediateInstruction labelIntermediateInstruction:
                CompileLabelInstruction(labelIntermediateInstruction);
                break;
            case IntermediateInstruction intermediateInstruction:
                CompileIntermediateInstruction(intermediateInstruction, block);
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

    private void CompileIntermediateInstruction(IntermediateInstruction intermediateInstruction, Block block)
    {
        foreach (var kvp in _marked)
        {
            _marked[kvp.Key] = false;
        }

        if (intermediateInstruction.FirstOperand is FunctionCallTerm fct)
        {
            var argumentRegisters = fct.GetUsedRegisters();

            foreach (var argumentRegister in argumentRegisters)
            {
                // var pr = _vrToPr[argumentRegister.VirtualRegister];
                //
                // if (pr != null)
                // {
                //     argumentRegister.PhysicalRegister = pr.Value;
                // }

                _vrToSr[argumentRegister.VirtualRegister] = argumentRegister.Name;

                var pr = _vrToPr[argumentRegister.VirtualRegister];

                if (pr == null)
                {
                    pr = GetAPr(
                        argumentRegister.VirtualRegister,
                        argumentRegister.NextUse);

                    if (pr == Register.None)
                    {
                        continue;
                    }

                    argumentRegister.PhysicalRegister = pr.Value;

                    AddInstruction(
                        GasSymbols.GenerateMovFromOffset(
                            _registerOffsetMap[argumentRegister.Name],
                            argumentRegister.PhysicalRegister));
                }
                else
                {
                    argumentRegister.PhysicalRegister = pr.Value;
                }

                _marked[pr.Value] = true;
            }
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

        if (intermediateInstruction.FirstOperand is FieldAccessTerm fieldAccessTerm)
        {
            var register = fieldAccessTerm.InstanceRegister;
            _vrToSr[register.VirtualRegister] = register.Name;

            var pr = _vrToPr[register.VirtualRegister];

            if (pr == null)
            {
                pr = GetAPr(
                    register.VirtualRegister,
                    register.NextUse);

                register.PhysicalRegister = pr.Value;

                AddInstruction(
                    GasSymbols.GenerateMovFromOffset(
                        _registerOffsetMap[register.Name],
                        register.PhysicalRegister));
            }
            else
            {
                register.PhysicalRegister = pr.Value;
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

        if (intermediateInstruction.SecondOperand is FieldAccessTerm fieldAccessTerm2)
        {
            var register = fieldAccessTerm2.InstanceRegister;
            _vrToSr[register.VirtualRegister] = register.Name;

            var pr = _vrToPr[register.VirtualRegister];

            if (pr == null)
            {
                pr = GetAPr(
                    register.VirtualRegister,
                    register.NextUse);

                register.PhysicalRegister = pr.Value;

                AddInstruction(
                    GasSymbols.GenerateMovFromOffset(
                        _registerOffsetMap[register.Name],
                        register.PhysicalRegister));
            }
            else
            {
                register.PhysicalRegister = pr.Value;
            }

            _marked[pr.Value] = true;
        }

        if (intermediateInstruction.FirstOperand is RegisterTerm firstOperandNew)
        {
            if (firstOperandNew.NextUse == -1 && _prToVr[firstOperandNew.PhysicalRegister] != null)
            {
                if (firstOperandNew.Identifier != null &&
                    !block.RedefinesRegisterAfterInstruction(intermediateInstruction, firstOperandNew.Name) &&
                    block.LiveOut.Contains(firstOperandNew.Name))
                {
                    AddInstruction(
                        GasSymbols.GenerateMovToOffset(
                            _registerOffsetMap[_vrToSr[firstOperandNew.VirtualRegister]],
                            firstOperandNew.PhysicalRegister));
                }

                FreeAPr(firstOperandNew.PhysicalRegister);
            }
        }

        if (intermediateInstruction.FirstOperand is FieldAccessTerm fieldAccessTermNew)
        {
            var register = fieldAccessTermNew.InstanceRegister;
            if (register.NextUse == -1 && _prToVr[register.PhysicalRegister] != null)
            {
                if (register.Identifier != null &&
                    !block.RedefinesRegisterAfterInstruction(intermediateInstruction, register.Name) &&
                    block.LiveOut.Contains(register.Name))
                {
                    AddInstruction(
                        GasSymbols.GenerateMovToOffset(
                            _registerOffsetMap[_vrToSr[register.VirtualRegister]],
                            register.PhysicalRegister));
                }

                FreeAPr(register.PhysicalRegister);
                _marked[register.PhysicalRegister] = true;
            }
        }

        // if (intermediateInstruction.FirstOperand is FunctionCallTerm fct1)
        // {
        //     var registers = fct1.GetUsedRegisters();
        //
        //     foreach (var register in registers)
        //     {
        //         if (register.NextUse == -1 && _prToVr[register.PhysicalRegister] != null)
        //         {
        //             if (!block.RedefinesRegisterAfterInstruction(intermediateInstruction, register.Name) && block.LiveOut.Contains(register.Name))
        //             {
        //                 AddInstruction(GasSymbols.GenerateMovToOffset(_registerOffsetMap[_vrToSr[register.VirtualRegister]], register.PhysicalRegister));
        //             }
        //         
        //             FreeAPr(register.PhysicalRegister);
        //         }
        //     }
        // }

        if (intermediateInstruction.SecondOperand is RegisterTerm secondOperandNew)
        {
            if (secondOperandNew.NextUse == -1 && _prToVr[secondOperandNew.PhysicalRegister] != null)
            {
                if (secondOperandNew.Identifier != null &&
                    !block.RedefinesRegisterAfterInstruction(intermediateInstruction, secondOperandNew.Name) &&
                    block.LiveOut.Contains(secondOperandNew.Name))
                {
                    AddInstruction(
                        GasSymbols.GenerateMovToOffset(
                            _registerOffsetMap[_vrToSr[secondOperandNew.VirtualRegister]],
                            secondOperandNew.PhysicalRegister));
                }

                FreeAPr(secondOperandNew.PhysicalRegister);
            }
        }

        if (intermediateInstruction.SecondOperand is FieldAccessTerm fieldAccessTermNew2)
        {
            var register = fieldAccessTermNew2.InstanceRegister;
            if (register.NextUse == -1 && _prToVr[register.PhysicalRegister] != null)
            {
                if (register.Identifier != null &&
                    !block.RedefinesRegisterAfterInstruction(intermediateInstruction, register.Name) &&
                    block.LiveOut.Contains(register.Name))
                {
                    AddInstruction(
                        GasSymbols.GenerateMovToOffset(
                            _registerOffsetMap[_vrToSr[register.VirtualRegister]],
                            register.PhysicalRegister));
                }

                FreeAPr(register.PhysicalRegister);
                _marked[register.PhysicalRegister] = true;
            }
        }

        foreach (var kvp in _marked)
        {
            _marked[kvp.Key] = false;
        }

        var lhs = intermediateInstruction.LeftHandSide;

        if (lhs != null)
        {
            if (!string.IsNullOrEmpty(lhs.Identifier))
            {
                var prev = _vrToSr
                    .Where(x => x.Value == lhs.Name);

                foreach (var x in prev.Select(x => x.Key))
                {
                    var pr = _vrToPr.GetValueOrDefault(x);

                    if (pr != null)
                    {
                        _vrToPr.Remove(x);
                        _prToVr.Remove(pr.Value);
                        _physicalRegistersStack.Push(pr.Value);
                    }
                }
            }

            if (lhs.FieldAccessTerm != null)
            {
                var fat = lhs.FieldAccessTerm;
                var register = fat.InstanceRegister;
                _vrToSr[register.VirtualRegister] = register.Name;

                var pr = _vrToPr[register.VirtualRegister];

                if (pr == null)
                {
                    pr = GetAPr(
                        register.VirtualRegister,
                        register.NextUse);

                    register.PhysicalRegister = pr.Value;

                    AddInstruction(
                        GasSymbols.GenerateMovFromOffset(
                            _registerOffsetMap[register.Name],
                            register.PhysicalRegister));
                }
                else
                {
                    register.PhysicalRegister = pr.Value;
                }
            }

            _vrToSr[lhs.VirtualRegister] = lhs.Name;

            lhs.PhysicalRegister = GetAPr(
                lhs.VirtualRegister,
                lhs.NextUse);

            _marked[lhs.PhysicalRegister] = true;
        }


        switch (intermediateInstruction.InstructionType)
        {
            case InstructionType.LhsFieldAccess:
                GenerateLhsFieldAccess(intermediateInstruction);
                break;
            case InstructionType.New:
                GenerateNew(intermediateInstruction);
                break;
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
                    GasSymbols.GenerateSetGreater,
                    GasSymbols.GenerateSetLess);
                break;
            case InstructionType.GreaterEqual:
                GenerateRelOp(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide,
                    GasSymbols.GenerateSetGreaterEqual,
                    GasSymbols.GenerateSetLessEqual);
                break;
            case InstructionType.Less:
                GenerateRelOp(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide,
                    GasSymbols.GenerateSetLess,
                    GasSymbols.GenerateSetGreater);
                break;
            case InstructionType.LessEqual:
                GenerateRelOp(
                    intermediateInstruction.FirstOperand,
                    intermediateInstruction.SecondOperand,
                    intermediateInstruction.LeftHandSide,
                    GasSymbols.GenerateSetLessEqual,
                    GasSymbols.GenerateSetGreaterEqual);
                break;
            // case InstructionType.And:
            //     GenerateAnd(
            //         intermediateInstruction.FirstOperand,
            //         intermediateInstruction.SecondOperand,
            //         intermediateInstruction.LeftHandSide);
            //     break;
            // case InstructionType.Or:
            //     GenerateOr(
            //         intermediateInstruction.FirstOperand,
            //         intermediateInstruction.SecondOperand,
            //         intermediateInstruction.LeftHandSide);
            //     break;
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
        var opFieldAccess = cmpInstruction as FieldAccessTerm;
        var offset =
            opRegister != null ? _registerOffsetMap.GetValueOrDefault(opRegister.Name) : 0;

        if (opRegister != null)
        {
            var register = _vrToPr[opRegister.VirtualRegister];

            if (offset != 0)
            {
                AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
            }
            else
            {
                AddInstruction(GasSymbols.GenerateTest(register.Value, register.Value));

                AddInstruction(
                    ifIntermediateInstruction.Negate
                        ? GasSymbols.GenerateJumpEqual(ifIntermediateInstruction.JumpLabel.LabelTerm.Label)
                        : GasSymbols.GenerateJumpNotEqual(ifIntermediateInstruction.JumpLabel.LabelTerm.Label));

                return;
            }
        }

        if (opBool != null)
        {
            AddInstruction(GasSymbols.GenerateMov(Convert.ToInt32(opBool.Value), Register.RAX));
        }

        if (opFunCall != null)
        {
            GenerateFunctionCall(opFunCall);
        }

        if (opFieldAccess != null)
        {
            var register = _vrToPr.GetValueOrDefault(opFieldAccess.InstanceRegister.Name);
            opFieldAccess.InstanceRegister.PhysicalRegister = register ?? Register.None;

            if (opFieldAccess.InstanceRegister.PhysicalRegister == Register.None)
            {
                offset = _registerOffsetMap[opFieldAccess.InstanceRegister.Name];
                AddInstruction(GasSymbols.GenerateMovFromOffset(offset, Register.RAX));
                opFieldAccess.InstanceRegister.PhysicalRegister = Register.RAX;
            }

            SaveFieldAccessToRegister(opFieldAccess, Register.RAX, true);
        }

        AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));

        AddInstruction(
            ifIntermediateInstruction.Negate
                ? GasSymbols.GenerateJumpEqual(ifIntermediateInstruction.JumpLabel.LabelTerm.Label)
                : GasSymbols.GenerateJumpNotEqual(ifIntermediateInstruction.JumpLabel.LabelTerm.Label));
    }

    private void GenerateLhsFieldAccess(IntermediateInstruction intermediateInstruction)
    {
        if (intermediateInstruction.FirstOperand is not FieldAccessTerm fieldAccessTerm)
        {
            throw new Exception();
        }

        var register = fieldAccessTerm.InstanceRegister;
        var x = fieldAccessTerm.InnerFieldAccess;
        var first = true;
        ClassSymbol currentClass = null;

        while (x != null)
        {
            if (first)
            {
                if (x.InnerFieldAccess == null)
                {
                    AddInstruction(
                        GasSymbols.GenerateLeaFromMemory(
                            register.PhysicalRegister,
                            fieldAccessTerm.ClassSymbol.GetFieldOffset(x.ClassField),
                            intermediateInstruction.LeftHandSide.PhysicalRegister));
                }
                else
                {
                    AddInstruction(
                        GasSymbols.GenerateMovFromMemory(
                            register.PhysicalRegister,
                            fieldAccessTerm.ClassSymbol.GetFieldOffset(x.ClassField),
                            intermediateInstruction.LeftHandSide.PhysicalRegister));
                }

                var fieldSymbol = fieldAccessTerm.ClassSymbol.Fields.FirstOrDefault(y => y.Name == x.ClassField);
                var isBasicType = TypesHelper.IsBasicType(fieldSymbol.LatteType);

                if (!isBasicType)
                {
                    currentClass = _classes.FirstOrDefault(y => y.Name == fieldSymbol.LatteType);
                }

                first = false;
            }
            else
            {
                if (x.InnerFieldAccess == null)
                {
                    AddInstruction(
                        GasSymbols.GenerateLeaFromMemory(
                            register.PhysicalRegister,
                            currentClass.GetFieldOffset(x.ClassField),
                            intermediateInstruction.LeftHandSide.PhysicalRegister));
                }
                else
                {
                    AddInstruction(
                        GasSymbols.GenerateMovFromMemory(
                            intermediateInstruction.LeftHandSide.PhysicalRegister,
                            currentClass.GetFieldOffset(x.ClassField),
                            intermediateInstruction.LeftHandSide.PhysicalRegister));
                }

                var fieldSymbol = currentClass.Fields.FirstOrDefault(y => y.Name == x.ClassField);
                var isBasicType = TypesHelper.IsBasicType(fieldSymbol.LatteType);

                if (!isBasicType)
                {
                    currentClass = _classes.FirstOrDefault(y => y.Name == fieldSymbol.LatteType);
                }
            }

            x = x.InnerFieldAccess;
        }
    }

    private void GenerateNew(IntermediateInstruction intermediateInstruction)
    {
        var type = intermediateInstruction.LeftHandSide.Type;
        var classPrototype = _classes.FirstOrDefault(x => x.Name == type);
        var size = classPrototype.Size;

        if (_vrToPr.ContainsValue(Register.RDI))
        {
            AddInstruction(GasSymbols.GeneratePush(Register.RDI));
        }

        AddInstruction($"MOV RDI, {size}");
        AddInstruction("CALL _malloc");

        var offset = 0;
        
        foreach (var _ in classPrototype.Fields)
        {
            AddInstruction(offset == 0 ? $"MOV QWORD PTR [RDI], 0" : $"MOV QWORD PTR [RDI+{offset}], 0");

            offset += 8;
        }

        if (_vrToPr.ContainsValue(Register.RDI))
        {
            AddInstruction(GasSymbols.GeneratePop(Register.RDI));
        }

        if (intermediateInstruction.LeftHandSide.PhysicalRegister != Register.RAX)
        {
            AddInstruction($"MOV {intermediateInstruction.LeftHandSide.PhysicalRegister}, RAX");
        }
    }

    private void GenerateAssignment(IntermediateInstruction intermediateInstruction)
    {
        var toMemory = intermediateInstruction.LeftHandSide.FieldAccessTerm != null;

        if (intermediateInstruction.FirstOperand is NewTerm nt)
        {
            var type = intermediateInstruction.LeftHandSide.Type;
            var classPrototype = _classes.FirstOrDefault(x => x.Name == type);
            var size = classPrototype.Size;

            if (_vrToPr.ContainsValue(Register.RDI))
            {
                AddInstruction(GasSymbols.GeneratePush(Register.RDI));
            }

            AddInstruction($"MOV RDI, {size}");
            AddInstruction("CALL _malloc");

            if (_vrToPr.ContainsValue(Register.RDI))
            {
                AddInstruction(GasSymbols.GeneratePop(Register.RDI));
            }

            if (intermediateInstruction.LeftHandSide.FieldAccessTerm == null)
            {
                if (intermediateInstruction.LeftHandSide.PhysicalRegister != Register.RAX)
                {
                    AddInstruction($"MOV {intermediateInstruction.LeftHandSide.PhysicalRegister}, RAX");
                }
            }
            else
            {
                SaveFieldAccessToRegister(intermediateInstruction.LeftHandSide.FieldAccessTerm, intermediateInstruction.LeftHandSide.PhysicalRegister);
                AddInstruction(
                    GasSymbols.GenerateMov(Register.RAX, intermediateInstruction.LeftHandSide.PhysicalRegister, true));
            }


            return;
        }

        if (intermediateInstruction.FirstOperand is ConstantNullTerm)
        {
            AddInstruction(
                GasSymbols.GenerateMov(0, intermediateInstruction.LeftHandSide.PhysicalRegister, toMemory));
        }

        if (intermediateInstruction.LeftHandSide.FieldAccessTerm != null)
        {
            // if (intermediateInstruction.LeftHandSide.FieldAccessTerm.)
            // SaveFieldAccessToRegister(
            //     intermediateInstruction.LeftHandSide.FieldAccessTerm, 
            //     Register.RAX);

            if (intermediateInstruction.FirstOperand is RegisterTerm)
            {
                SaveFieldAccessToRegister(
                    intermediateInstruction.LeftHandSide.FieldAccessTerm,
                    Register.RAX);
            }
            else
            {
                SaveFieldAccessToRegister(
                    intermediateInstruction.LeftHandSide.FieldAccessTerm,
                    intermediateInstruction.LeftHandSide.PhysicalRegister);
            }
        }

        if (intermediateInstruction.FirstOperand is FieldAccessTerm fat)
        {
            SaveFieldAccessToRegister(fat, intermediateInstruction.LeftHandSide.PhysicalRegister, true);
        }

        if (intermediateInstruction.FirstOperand is ConstantIntTerm intTerm)
        {
            AddInstruction(
                GasSymbols.GenerateMov(intTerm.Value, intermediateInstruction.LeftHandSide.PhysicalRegister, toMemory));
        }

        if (intermediateInstruction.FirstOperand is ConstantBoolTerm boolTerm)
        {
            if (intermediateInstruction.InBoolExpr)
            {
                if (_registerOffsetMap.Values.Any() && _registerOffsetMap.MinBy(x => x.Value).Key ==
                    intermediateInstruction.LeftHandSide.Name)
                {
                    AddInstruction(GasSymbols.GeneratePush(Convert.ToInt32(boolTerm.Value)));
                }
                else
                {
                    AddInstruction(GasSymbols.GeneratePush(Convert.ToInt32(boolTerm.Value)));
                    _stackAlignedTo = (_stackAlignedTo + 8) % 16;
                    _pushes++;
                    if (_registerOffsetMap.Values.Any())
                    {
                        _registerOffsetMap[intermediateInstruction.LeftHandSide.Name] =
                            _registerOffsetMap.Values.Min() - 8;
                    }
                    else
                    {
                        _registerOffsetMap[intermediateInstruction.LeftHandSide.Name] = -_pushedRegistersOffset - 8;
                    }
                }

                return;
            }

            AddInstruction(
                GasSymbols.GenerateMov(
                    Convert.ToInt32(boolTerm.Value),
                    intermediateInstruction.LeftHandSide.PhysicalRegister,
                    toMemory));
        }

        if (intermediateInstruction.FirstOperand is ConstantStringTerm stringTerm)
        {
            var label = _literalsMap[stringTerm.Value];

            if (toMemory)
            {
                AddInstruction(GasSymbols.GenerateLeaForLiteral(label, Register.RAX));
                AddInstruction(
                    GasSymbols.GenerateMov(
                        Register.RAX,
                        intermediateInstruction.LeftHandSide.PhysicalRegister,
                        toMemory));
            }
            else
            {
                AddInstruction(
                    GasSymbols.GenerateLeaForLiteral(label, intermediateInstruction.LeftHandSide.PhysicalRegister));
            }
        }

        if (intermediateInstruction.FirstOperand is RegisterTerm registerTerm)
        {
            if (toMemory)
            {
                AddInstruction(
                    GasSymbols.GenerateMov(
                        registerTerm.PhysicalRegister,
                        Register.RAX,
                        toMemory));

                AddInstruction(
                    GasSymbols.GenerateMov(Register.RAX, intermediateInstruction.LeftHandSide.PhysicalRegister));
            }
            else
            {
                AddInstruction(
                    GasSymbols.GenerateMov(
                        registerTerm.PhysicalRegister,
                        intermediateInstruction.LeftHandSide.PhysicalRegister));
            }
        }
    }

    private void GenerateIncrement(Term operand)
    {
        if (operand is RegisterTerm registerTerm)
        {
            AddInstruction(GasSymbols.GenerateIncrement(registerTerm.PhysicalRegister));
        }

        if (operand is FieldAccessTerm fat)
        {
            SaveFieldAccessToRegister(fat, Register.RAX);
            AddInstruction(GasSymbols.GenerateIncrementAddress(Register.RAX));
        }
    }

    private void GenerateDecrement(Term operand)
    {
        if (operand is RegisterTerm registerTerm)
        {
            AddInstruction(GasSymbols.GenerateDecrement(registerTerm.PhysicalRegister));
        }

        if (operand is FieldAccessTerm fat)
        {
            SaveFieldAccessToRegister(fat, Register.RAX);
            AddInstruction(GasSymbols.GenerateDecrementAddress(Register.RAX));
        }
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


        var registersToSave = _vrToPr.Values.Where(x => x != null).Select(x => x.Value)
            .Intersect(GasSymbols.NotPreservedRegisters);
        var registersToSaveReverse = registersToSave.Reverse();

        foreach (var registerToSave in registersToSave)
        {
            AddInstruction(GasSymbols.GeneratePush(registerToSave));
        }

        // Based on number of pushes we have to do, do we have to push dummy 8 byte word to
        // align stack?
        var shouldAlignStack = ((pushesCount * 8) + _stackAlignedTo) % 16 != 0;

        if (shouldAlignStack)
        {
            AddInstruction(GasSymbols.GeneratePush(0));
        }

        var pushedRegisters = new Stack<Register>();

        var zipped = functionCallTerm.Arguments.Zip(tempRegisters);
        var dummy = zipped.Where(x => x.Second == Register.None);
        var rest = zipped.Except(dummy);

        foreach (var (argument, register) in dummy.Concat(rest))
        {
            switch (argument)
            {
                case ConstantIntTerm intTerm:
                    if (register == Register.None)
                    {
                        AddInstruction(GasSymbols.GeneratePush(intTerm.Value));
                    }
                    else
                    {
                        if (GasSymbols.ParamRegisters.Contains(register))
                        {
                            AddInstruction(GasSymbols.GeneratePush(intTerm.Value));
                            pushedRegisters.Push(register);
                        }
                        else
                        {
                            AddInstruction(GasSymbols.GenerateMov(intTerm.Value, register));
                        }
                    }

                    break;
                case ConstantNullTerm:
                    if (register == Register.None)
                    {
                        AddInstruction(GasSymbols.GeneratePush(0));
                    }
                    else
                    {
                        if (GasSymbols.ParamRegisters.Contains(register))
                        {
                            AddInstruction(GasSymbols.GeneratePush(0));
                            pushedRegisters.Push(register);
                        }
                        else
                        {
                            AddInstruction(GasSymbols.GenerateMov(0, register));
                        }
                    }

                    break;
                case ConstantBoolTerm boolTerm:
                    if (register == Register.None)
                    {
                        AddInstruction(GasSymbols.GeneratePush(Convert.ToInt32(boolTerm.Value)));
                    }
                    else
                    {
                        if (GasSymbols.ParamRegisters.Contains(register))
                        {
                            AddInstruction(GasSymbols.GeneratePush(Convert.ToInt32(boolTerm.Value)));
                            pushedRegisters.Push(register);
                        }
                        else
                        {
                            AddInstruction(GasSymbols.GenerateMov(Convert.ToInt32(boolTerm.Value), register));
                        }
                    }

                    break;
                case ConstantStringTerm stringTerm:
                    if (register == Register.None)
                    {
                        AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[stringTerm.Value], Register.RAX));
                        AddInstruction(GasSymbols.GeneratePush(Register.RAX));
                    }
                    else
                    {
                        if (GasSymbols.ParamRegisters.Contains(register))
                        {
                            AddInstruction(
                                GasSymbols.GenerateLeaForLiteral(_literalsMap[stringTerm.Value], Register.RAX));
                            AddInstruction(GasSymbols.GeneratePush(Register.RAX));
                            pushedRegisters.Push(register);
                        }
                        else
                        {
                            AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[stringTerm.Value], register));
                        }
                    }

                    break;
                case FieldAccessTerm fat:
                    SaveFieldAccessToRegister(fat, Register.RAX, true);
                    if (register == Register.None)
                    {
                        AddInstruction(GasSymbols.GeneratePush(Register.RAX));
                    }
                    else
                    {
                        if (GasSymbols.ParamRegisters.Contains(register))
                        {
                            AddInstruction(GasSymbols.GeneratePush(Register.RAX));
                            pushedRegisters.Push(register);
                        }
                        else
                        {
                            AddInstruction(GasSymbols.GenerateMov(Register.RAX, register));
                        }
                    }

                    break;
                case RegisterTerm registerTerm:
                {
                    // if (!_vrToPr.TryGetValue(registerTerm.VirtualRegister, out var pr) || pr == null)
                    // var pr = registerTerm.PhysicalRegister;
                    // Register? pr = registerTerm.PhysicalRegister;
                    if (!_vrToPr.TryGetValue(registerTerm.VirtualRegister, out var pr) || pr == null)
                    {
                        // if (register != Register.None)
                        // {
                        //     AddInstruction(
                        //         GasSymbols.GenerateMovFromOffset(_registerOffsetMap[registerTerm.Name], register));
                        //     break;
                        // }
                        //
                        // AddInstruction(
                        //     GasSymbols.GenerateMovFromOffset(_registerOffsetMap[registerTerm.Name], Register.RAX));
                        pr = Register.None;
                    }

                    if (register == Register.None && pr == Register.None)
                    {
                        AddInstruction(
                            GasSymbols.GenerateMovFromOffset(_registerOffsetMap[registerTerm.Name], Register.RAX));
                        AddInstruction(GasSymbols.GeneratePush(Register.RAX));
                    }

                    if (register == Register.None && pr != Register.None)
                    {
                        AddInstruction(GasSymbols.GeneratePush(pr.Value));
                    }

                    if (register != Register.None && pr == Register.None)
                    {
                        AddInstruction(
                            GasSymbols.GenerateMovFromOffset(_registerOffsetMap[registerTerm.Name], register));
                    }

                    if (register != Register.None && pr != Register.None)
                    {
                        if (GasSymbols.ParamRegisters.Contains(register))
                        {
                            AddInstruction(GasSymbols.GeneratePush(pr.Value));
                            pushedRegisters.Push(register);
                        }
                        else
                        {
                            AddInstruction(GasSymbols.GenerateMov(pr.Value, register));
                        }
                    }

                    break;
                }
            }
        }

        while (pushedRegisters.Any())
        {
            AddInstruction(GasSymbols.GeneratePop(pushedRegisters.Pop()));
        }

        AddInstruction(GasSymbols.GenerateFunctionCall(functionCallTerm.Name));

        var toSubtractFromStack = (pushesCount * 8) + (shouldAlignStack ? 8 : 0);

        if (toSubtractFromStack > 0)
        {
            AddInstruction(GasSymbols.GenerateAdd(Register.RSP, toSubtractFromStack));
        }

        foreach (var registerToSave in registersToSaveReverse)
        {
            AddInstruction(GasSymbols.GeneratePop(registerToSave));
        }

        if (target != null)
        {
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
        }
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
                AddInstruction(GasSymbols.GenerateMov(registerTerm.PhysicalRegister, Register.RAX));
                break;
            }
            case ConstantNullTerm:
            {
                AddInstruction(GasSymbols.GenerateMov(0, Register.RAX));
                break;
            }
            case FieldAccessTerm fat:
            {
                SaveFieldAccessToRegister(fat, Register.RAX, true);
                break;
            }
        }

        AddFnEpilog();
    }

    private void GenerateAdd(Term left, Term right, RegisterTerm target)
    {
        if (left is not RegisterTerm && right is RegisterTerm)
        {
            (left, right) = (right, left);
        }

        if (left is ConstantIntTerm && right is FieldAccessTerm)
        {
            (left, right) = (right, left);
        }

        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var rightInt = right as ConstantIntTerm;
        var leftFieldAccess = left as FieldAccessTerm;
        var rightFieldAccess = right as FieldAccessTerm;

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
            if (rightInt != null)
            {
                if (leftRegister.PhysicalRegister == target.PhysicalRegister)
                {
                    AddInstruction(GasSymbols.GenerateAdd(leftRegister.PhysicalRegister, rightInt.Value));

                    return;
                }

                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, rightInt.Value));
            }
            else
            {
                if (leftRegister.PhysicalRegister == target.PhysicalRegister)
                {
                    SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
                    AddInstruction(GasSymbols.GenerateAdd(leftRegister.PhysicalRegister, Register.RAX));

                    return;
                }

                SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, Register.RAX));
            }

            return;
        }

        if (leftFieldAccess != null && rightInt != null)
        {
            SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);
            AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, rightInt.Value));
        }

        if (leftFieldAccess != null && rightFieldAccess != null)
        {
            SaveFieldAccessToRegister(leftFieldAccess, target.PhysicalRegister, true);
            SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
            AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, Register.RAX));
        }
    }

    private void GenerateAddStrings(Term left, Term right, RegisterTerm target)
    {
        var rdiUsed = _vrToPr.ContainsValue(Register.RDI);
        var rsiUsed = _vrToPr.ContainsValue(Register.RSI);

        if (rdiUsed)
        {
            AddInstruction(GasSymbols.GeneratePush(Register.RDI));
        }

        if (rsiUsed)
        {
            AddInstruction(GasSymbols.GeneratePush(Register.RSI));
        }

        switch (left)
        {
            case RegisterTerm rt:
                AddInstruction(GasSymbols.GenerateMov(rt.PhysicalRegister, Register.RDI));
                break;
            case ConstantStringTerm str:
                AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[str.Value], Register.RDI));
                break;
            case FieldAccessTerm fat:
                SaveFieldAccessToRegister(fat, Register.RDI, true);
                break;
        }

        switch (right)
        {
            case RegisterTerm rt:
                AddInstruction(GasSymbols.GenerateMov(rt.PhysicalRegister, Register.RSI));
                break;
            case ConstantStringTerm str:
                AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[str.Value], Register.RSI));
                break;
            case FieldAccessTerm fat:
                SaveFieldAccessToRegister(fat, Register.RSI, true);
                break;
        }

        AddInstruction(GasSymbols.GenerateFunctionCall("concatStrings"));

        if (rsiUsed)
        {
            AddInstruction(GasSymbols.GeneratePop(Register.RSI));
        }

        if (rdiUsed)
        {
            AddInstruction(GasSymbols.GeneratePop(Register.RDI));
        }

        AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
    }

    private void GenerateSubtract(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftFieldAccess = left as FieldAccessTerm;
        var rightFieldAccess = right as FieldAccessTerm;

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
            if (rightInt != null)
            {
                if (leftRegister.PhysicalRegister == target.PhysicalRegister)
                {
                    AddInstruction(GasSymbols.GenerateSubtract(leftRegister.PhysicalRegister, rightInt.Value));

                    return;
                }

                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, rightInt.Value));
            }
            else
            {
                if (leftRegister.PhysicalRegister == target.PhysicalRegister)
                {
                    SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
                    AddInstruction(GasSymbols.GenerateSubtract(leftRegister.PhysicalRegister, Register.RAX));

                    return;
                }

                SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateAdd(target.PhysicalRegister, Register.RAX));
            }

            return;
        }

        if (leftInt != null && rightRegister != null)
        {
            if (rightRegister.PhysicalRegister == target.PhysicalRegister)
            {
                AddInstruction(GasSymbols.GenerateSubtract(rightRegister.PhysicalRegister, leftInt.Value));
                AddInstruction(GasSymbols.GenerateNegation(rightRegister.PhysicalRegister));

                return;
            }

            AddInstruction(GasSymbols.GenerateMov(leftInt.Value, target.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, rightRegister.PhysicalRegister));

            return;
        }

        if (leftFieldAccess != null && rightRegister != null)
        {
            if (rightRegister.PhysicalRegister == target.PhysicalRegister)
            {
                SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
                AddInstruction(GasSymbols.GenerateSubtract(rightRegister.PhysicalRegister, Register.RAX));
                AddInstruction(GasSymbols.GenerateNegation(rightRegister.PhysicalRegister));

                return;
            }

            SaveFieldAccessToRegister(leftFieldAccess, target.PhysicalRegister, true);
            AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, rightRegister.PhysicalRegister));

            return;
        }

        if (leftInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(leftInt.Value, target.PhysicalRegister));
        }

        if (rightInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(rightInt.Value, Register.RAX));
        }

        if (leftFieldAccess != null)
        {
            SaveFieldAccessToRegister(leftFieldAccess, target.PhysicalRegister, true);
        }

        if (rightFieldAccess != null)
        {
            SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
        }

        AddInstruction(GasSymbols.GenerateSubtract(target.PhysicalRegister, Register.RAX));
    }

    private void GenerateMultiply(Term left, Term right, RegisterTerm target)
    {
        if (left is not RegisterTerm && right is RegisterTerm)
        {
            (left, right) = (right, left);
        }

        if (left is ConstantIntTerm && right is FieldAccessTerm)
        {
            (left, right) = (right, left);
        }

        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftFieldAccess = left as FieldAccessTerm;
        var rightFieldAccess = right as FieldAccessTerm;

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
            if (rightInt != null)
            {
                if (leftRegister.PhysicalRegister == target.PhysicalRegister)
                {
                    AddInstruction(GasSymbols.GenerateMultiply(leftRegister.PhysicalRegister, rightInt.Value));

                    return;
                }

                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, rightInt.Value));
            }
            else
            {
                if (leftRegister.PhysicalRegister == target.PhysicalRegister)
                {
                    SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
                    AddInstruction(GasSymbols.GenerateMultiply(leftRegister.PhysicalRegister, Register.RAX));

                    return;
                }

                SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, Register.RAX));
            }

            return;
        }

        if (leftFieldAccess != null && rightInt != null)
        {
            SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);
            AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, rightInt.Value));
        }

        if (leftFieldAccess != null && rightFieldAccess != null)
        {
            SaveFieldAccessToRegister(leftFieldAccess, target.PhysicalRegister, true);
            SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
            AddInstruction(GasSymbols.GenerateMultiply(target.PhysicalRegister, Register.RAX));
        }
    }

    private void GenerateDivide(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftFieldAccess = left as FieldAccessTerm;
        var rightFieldAccess = right as FieldAccessTerm;
        var rdxUsed = _vrToPr.Values.Any(x => x == Register.RDX);

        if (rdxUsed)
        {
            AddInstruction(GasSymbols.GeneratePush(Register.RDX));
        }

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightRegister.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));

            if (rdxUsed)
            {
                AddInstruction(GasSymbols.GeneratePop(Register.RDX));
            }

            return;
        }

        if (leftRegister != null)
        {
            if (rightInt != null)
            {
                AddInstruction(
                    GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightInt.Value, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
            }
            else
            {
                AddInstruction($"MOV RAX, {leftRegister}");
                SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);
                AddInstruction("CQO");
                AddInstruction($"IDIV {target.PhysicalRegister}");
                AddInstruction($"MOV {target.PhysicalRegister}, RAX");
            }

            if (rdxUsed)
            {
                AddInstruction(GasSymbols.GeneratePop(Register.RDX));
            }

            return;
        }

        if (leftInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(leftInt.Value, Register.RAX));
        }

        if (rightInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(rightInt.Value, target.PhysicalRegister));
        }

        if (leftFieldAccess != null)
        {
            SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
        }

        if (rightFieldAccess != null)
        {
            SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);
        }

        AddInstruction("CQO");
        AddInstruction($"IDIV {target.PhysicalRegister}");
        AddInstruction($"MOV {target.PhysicalRegister}, RAX");

        if (rdxUsed)
        {
            AddInstruction(GasSymbols.GeneratePop(Register.RDX));
        }
    }

    private void GenerateModulo(Term left, Term right, RegisterTerm target)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftFieldAccess = left as FieldAccessTerm;
        var rightFieldAccess = right as FieldAccessTerm;
        var rdxUsed = _vrToPr.Values.Any(x => x == Register.RDX);

        if (rdxUsed)
        {
            AddInstruction(GasSymbols.GeneratePush(Register.RDX));
        }

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightRegister.PhysicalRegister));
            AddInstruction(GasSymbols.GenerateMov(Register.RDX, target.PhysicalRegister));

            if (rdxUsed)
            {
                AddInstruction(GasSymbols.GeneratePop(Register.RDX));
            }

            return;
        }

        if (leftRegister != null)
        {
            if (rightInt != null)
            {
                AddInstruction(
                    GasSymbols.GenerateDivide(leftRegister.PhysicalRegister, rightInt.Value, target.PhysicalRegister));
                AddInstruction(GasSymbols.GenerateMov(Register.RDX, target.PhysicalRegister));
            }
            else
            {
                AddInstruction($"MOV RAX, {leftRegister}");
                SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);
                AddInstruction("CQO");
                AddInstruction($"IDIV {target.PhysicalRegister}");
                AddInstruction($"MOV {target.PhysicalRegister}, RDX");
            }

            if (rdxUsed)
            {
                AddInstruction(GasSymbols.GeneratePop(Register.RDX));
            }

            return;
        }

        if (leftInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(leftInt.Value, Register.RAX));
        }

        if (rightInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(rightInt.Value, target.PhysicalRegister));
        }

        if (leftFieldAccess != null)
        {
            SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
        }

        if (rightFieldAccess != null)
        {
            SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);
        }

        AddInstruction("CQO");
        AddInstruction($"IDIV {target.PhysicalRegister}");
        AddInstruction($"MOV {target.PhysicalRegister}, RDX");

        if (rdxUsed)
        {
            AddInstruction(GasSymbols.GeneratePop(Register.RDX));
        }
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

        if (left is ConstantNullTerm or ConstantIntTerm or ConstantBoolTerm or ConstantStringTerm &&
            right is FieldAccessTerm)
        {
            (left, right) = (right, left);
        }

        var leftRegister = left as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var leftBool = left as ConstantBoolTerm;
        var leftString = left as ConstantStringTerm;
        var leftNull = left as ConstantNullTerm;
        var leftFieldAccess = left as FieldAccessTerm;
        var rightRegister = right as RegisterTerm;
        var rightInt = right as ConstantIntTerm;
        var rightBool = right as ConstantBoolTerm;
        var rightString = right as ConstantStringTerm;
        var rightNull = right as ConstantNullTerm;
        var rightFieldAccess = right as FieldAccessTerm;

        var rdiUsed = _vrToPr.ContainsValue(Register.RDI);
        var rsiUsed = _vrToPr.ContainsValue(Register.RSI);

        if (leftRegister != null && rightRegister != null)
        {
            if (leftRegister.Type == LatteType.String)
            {
                if (rdiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePush(Register.RDI));
                }

                if (rsiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePush(Register.RSI));
                }

                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, Register.RDI));
                AddInstruction(GasSymbols.GenerateMov(rightRegister.PhysicalRegister, Register.RSI));

                AddInstruction(GasSymbols.GenerateFunctionCall("compareStrings"));
                AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));

                AddInstruction(
                    equal
                        ? GasSymbols.GenerateSetNotEqual(target.PhysicalRegister)
                        : GasSymbols.GenerateSetEqual(target.PhysicalRegister));

                if (rdiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePop(Register.RSI));
                }

                if (rsiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePop(Register.RDI));
                }

                return;
            }

            AddInstruction(GasSymbols.GenerateCmp(leftRegister.PhysicalRegister, rightRegister.PhysicalRegister));
            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetEqual(target.PhysicalRegister)
                    : GasSymbols.GenerateSetNotEqual(target.PhysicalRegister));

            return;
        }

        if (leftRegister != null)
        {
            if (leftRegister.Type == LatteType.String)
            {
                if (rdiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePush(Register.RDI));
                }

                if (rsiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePush(Register.RSI));
                }

                AddInstruction(GasSymbols.GenerateMov(leftRegister.PhysicalRegister, Register.RDI));

                if (rightString != null)
                {
                    AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[rightString.Value], Register.RSI));
                }
                else
                {
                    SaveFieldAccessToRegister(rightFieldAccess, Register.RSI, true);
                }

                AddInstruction(GasSymbols.GenerateFunctionCall("compareStrings"));
                AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));
                AddInstruction(
                    equal
                        ? GasSymbols.GenerateSetNotEqual(target.PhysicalRegister)
                        : GasSymbols.GenerateSetEqual(target.PhysicalRegister));

                if (rdiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePop(Register.RSI));
                }

                if (rsiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePop(Register.RDI));
                }

                return;
            }


            if (rightFieldAccess != null)
            {
                SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);

                AddInstruction(GasSymbols.GenerateCmp(leftRegister.PhysicalRegister, Register.RAX));
            }
            else
            {
                var value = 0;

                if (rightInt != null || rightBool != null)
                {
                    value = rightInt?.Value ?? Convert.ToInt32(rightBool.Value);
                    ;
                }

                AddInstruction(GasSymbols.GenerateCmp(leftRegister.PhysicalRegister, value));
            }

            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetEqual(target.PhysicalRegister)
                    : GasSymbols.GenerateSetNotEqual(target.PhysicalRegister));

            return;
        }

        if (rightInt != null || rightBool != null)
        {
            var value = rightInt?.Value ?? Convert.ToInt32(rightBool.Value);
            SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
            AddInstruction(GasSymbols.GenerateCmp(Register.RAX, value));

            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetEqual(target.PhysicalRegister)
                    : GasSymbols.GenerateSetNotEqual(target.PhysicalRegister));

            return;
        }

        if (rightString != null)
        {
            if (rdiUsed)
            {
                AddInstruction(GasSymbols.GeneratePush(Register.RDI));
            }

            if (rsiUsed)
            {
                AddInstruction(GasSymbols.GeneratePush(Register.RSI));
            }

            SaveFieldAccessToRegister(leftFieldAccess, Register.RDI, true);
            AddInstruction(GasSymbols.GenerateLeaForLiteral(_literalsMap[rightString.Value], Register.RSI));

            AddInstruction(GasSymbols.GenerateFunctionCall("compareStrings"));
            AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));
            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetNotEqual(target.PhysicalRegister)
                    : GasSymbols.GenerateSetEqual(target.PhysicalRegister));

            if (rdiUsed)
            {
                AddInstruction(GasSymbols.GeneratePop(Register.RSI));
            }

            if (rsiUsed)
            {
                AddInstruction(GasSymbols.GeneratePop(Register.RDI));
            }

            return;
        }

        if (rightNull != null)
        {
            SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
            AddInstruction(GasSymbols.GenerateCmp(Register.RAX, 0));

            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetEqual(target.PhysicalRegister)
                    : GasSymbols.GenerateSetNotEqual(target.PhysicalRegister));

            return;
        }

        if (leftFieldAccess != null && rightFieldAccess != null)
        {
            if (leftFieldAccess.Type == LatteType.String)
            {
                if (rdiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePush(Register.RDI));
                }

                if (rsiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePush(Register.RSI));
                }

                SaveFieldAccessToRegister(leftFieldAccess, Register.RDI, true);
                SaveFieldAccessToRegister(rightFieldAccess, Register.RSI, true);

                AddInstruction(GasSymbols.GenerateFunctionCall("compareStrings"));
                AddInstruction(GasSymbols.GenerateTest(Register.RAX, Register.RAX));
                AddInstruction(
                    equal
                        ? GasSymbols.GenerateSetNotEqual(target.PhysicalRegister)
                        : GasSymbols.GenerateSetEqual(target.PhysicalRegister));

                if (rdiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePop(Register.RSI));
                }

                if (rsiUsed)
                {
                    AddInstruction(GasSymbols.GeneratePop(Register.RDI));
                }

                return;
            }

            SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
            SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);

            AddInstruction(
                equal
                    ? GasSymbols.GenerateSetEqual(target.PhysicalRegister)
                    : GasSymbols.GenerateSetNotEqual(target.PhysicalRegister));
        }
    }

    private void GenerateRelOp(
        Term left,
        Term right,
        RegisterTerm target,
        Func<Register, string> generate,
        Func<Register, string> generateReverse)
    {
        var leftRegister = left as RegisterTerm;
        var rightRegister = right as RegisterTerm;
        var leftInt = left as ConstantIntTerm;
        var rightInt = right as ConstantIntTerm;
        var leftFieldAccess = left as FieldAccessTerm;
        var rightFieldAccess = right as FieldAccessTerm;

        if (leftRegister != null && rightRegister != null)
        {
            AddInstruction(GasSymbols.GenerateCmp(leftRegister.PhysicalRegister, rightRegister.PhysicalRegister));
            AddInstruction(generate(target.PhysicalRegister));

            return;
        }

        if (leftRegister != null)
        {
            if (rightFieldAccess != null)
            {
                SaveFieldAccessToRegister(rightFieldAccess, Register.RAX, true);
                AddInstruction(GasSymbols.GenerateCmp(leftRegister.PhysicalRegister, Register.RAX));
            }
            else
            {
                AddInstruction(GasSymbols.GenerateCmp(leftRegister.PhysicalRegister, rightInt.Value));
            }

            AddInstruction(generate(target.PhysicalRegister));

            return;
        }

        if (rightRegister != null)
        {
            if (leftFieldAccess != null)
            {
                SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
                AddInstruction(GasSymbols.GenerateCmp(rightRegister.PhysicalRegister, Register.RAX));
            }
            else
            {
                AddInstruction(GasSymbols.GenerateCmp(rightRegister.PhysicalRegister, leftInt.Value));
            }

            AddInstruction(generateReverse(target.PhysicalRegister));

            return;
        }

        if (leftInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(leftInt.Value, Register.RAX));
        }

        if (rightInt != null)
        {
            AddInstruction(GasSymbols.GenerateMov(rightInt.Value, target.PhysicalRegister));
        }

        if (leftFieldAccess != null)
        {
            SaveFieldAccessToRegister(leftFieldAccess, Register.RAX, true);
        }

        if (rightFieldAccess != null)
        {
            SaveFieldAccessToRegister(rightFieldAccess, target.PhysicalRegister, true);
        }

        AddInstruction(GasSymbols.GenerateCmp(Register.RAX, target.PhysicalRegister));
        AddInstruction(generate(target.PhysicalRegister));
    }

    private void GenerateNegate(Term term, RegisterTerm target)
    {
        if (term is RegisterTerm registerTerm)
        {
            AddInstruction(GasSymbols.GenerateMov(registerTerm.PhysicalRegister, Register.RAX));
            AddInstruction(GasSymbols.GenerateNegation(Register.RAX));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
        }
        else if (term is FieldAccessTerm fat)
        {
            SaveFieldAccessToRegister(fat, Register.RAX, true);
            AddInstruction(GasSymbols.GenerateNegation(Register.RAX));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
        }
    }

    private void GenerateNot(Term term, RegisterTerm target)
    {
        if (term is RegisterTerm registerTerm)
        {
            AddInstruction(GasSymbols.GenerateMov(registerTerm.PhysicalRegister, Register.RAX));
            AddInstruction(GasSymbols.GenerateNot(Register.RAX));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
        }
        else if (term is FieldAccessTerm fat)
        {
            SaveFieldAccessToRegister(fat, Register.RAX, true);
            AddInstruction(GasSymbols.GenerateNot(Register.RAX));
            AddInstruction(GasSymbols.GenerateMov(Register.RAX, target.PhysicalRegister));
        }
    }

    private void AddInstruction(string instruction) => _result.Add(instruction);

    private List<string> AddStringLiteral(string literal)
    {
        var result = new List<string>();

        if (_literalsMap.ContainsKey(literal))
        {
            return result;
        }

        var label = $"str{_literalsMap.Count}";
        _literalsMap[literal] = label;
        result.Add($"{label}:");
        result.Add($".string \"{literal}\"");

        return result;
    }

    private List<string> AddFnProlog()
    {
        var result = new List<string>();

        result.Add(GasSymbols.GeneratePush(Register.RBP));
        result.Add(GasSymbols.GenerateMov(Register.RSP, Register.RBP));

        var registersUsedToSave = _registersUsed.Intersect(GasSymbols.PreservedRegisters);

        foreach (var registerToSave in registersUsedToSave)
        {
            result.Add(GasSymbols.GeneratePush(registerToSave));
        }

        // AddInstruction(GasSymbols.GeneratePush(Register.RBX));
        // AddInstruction(GasSymbols.GeneratePush(Register.R12));
        // AddInstruction(GasSymbols.GeneratePush(Register.R13));
        // AddInstruction(GasSymbols.GeneratePush(Register.R14));
        // AddInstruction(GasSymbols.GeneratePush(Register.R15));

        return result;
    }

    private void AddFnEpilog()
    {
        if ((_pushes * 8) + _minusOffset > 0)
        {
            AddInstruction(GasSymbols.GenerateAdd(Register.RSP, (_pushes * 8) + _minusOffset));
        }

        var registersUsedToSave = _prevRegistersUsed.Intersect(GasSymbols.PreservedRegisters).Reverse();

        foreach (var registerToSave in registersUsedToSave)
        {
            AddInstruction(GasSymbols.GeneratePop(registerToSave));
        }

        // AddInstruction(GasSymbols.GeneratePop(Register.R15));
        // AddInstruction(GasSymbols.GeneratePop(Register.R14));
        // AddInstruction(GasSymbols.GeneratePop(Register.R13));
        // AddInstruction(GasSymbols.GeneratePop(Register.R12));
        // AddInstruction(GasSymbols.GeneratePop(Register.RBX));
        AddInstruction(GasSymbols.GenerateMov(Register.RBP, Register.RSP));
        AddInstruction(GasSymbols.GeneratePop(Register.RBP));
        AddInstruction(GasSymbols.GenerateRet());
    }

    private Register GetAPr(string vr, int nu)
    {
        Register result;

        if (_physicalRegistersStack.Any(x => !_marked[x]))
        {
            var x = _physicalRegistersStack.Pop();
            var copy = new List<Register>();

            while (_marked[x])
            {
                copy.Add(x);
                x = _physicalRegistersStack.Pop();
            }

            result = x;

            foreach (var reg in copy)
            {
                _physicalRegistersStack.Push(reg);
            }
        }
        else if (_physicalRegistersStack.Any())
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

            if (result == Register.None)
            {
                return Register.None;
            }

            var spilledVr = _prToVr[result];
            var sr = _vrToSr[spilledVr];

            if (!_registerOffsetMap.ContainsKey(sr))
            {
                // AddInstruction(GasSymbols.GenerateSubtract(Register.RSP, 8));
                AddInstruction(GasSymbols.GeneratePush(result));
                _stackAlignedTo = (_stackAlignedTo + 8) % 16;
                _pushes++;

                if (_registerOffsetMap.Any())
                {
                    _registerOffsetMap[sr] = _registerOffsetMap.Values.Min() - 8;
                }
                else
                {
                    _registerOffsetMap[sr] = -_pushedRegistersOffset - 8;
                }
            }
            else
            {
                AddInstruction(GasSymbols.GenerateMovToOffset(_registerOffsetMap[sr], result));
            }

            _vrToPr[spilledVr] = null;
        }

        _registersUsed.Add(result);

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

    private void SaveFieldAccessToRegister(FieldAccessTerm fat, Register targetRegister, bool saveValue = false)
    {
        var register = fat.InstanceRegister;
        var x = fat.InnerFieldAccess;
        var first = true;
        ClassSymbol currentClass = null;

        while (x != null)
        {
            if (first)
            {
                if (x.InnerFieldAccess == null && !saveValue)
                {
                    AddInstruction(
                        GasSymbols.GenerateLeaFromMemory(
                            register.PhysicalRegister,
                            fat.ClassSymbol.GetFieldOffset(x.ClassField),
                            targetRegister));
                }
                else
                {
                    AddInstruction(
                        GasSymbols.GenerateMovFromMemory(
                            register.PhysicalRegister,
                            fat.ClassSymbol.GetFieldOffset(x.ClassField),
                            targetRegister));
                }

                var fieldSymbol = fat.ClassSymbol.Fields.FirstOrDefault(y => y.Name == x.ClassField);
                var isBasicType = TypesHelper.IsBasicType(fieldSymbol.LatteType);

                if (!isBasicType)
                {
                    currentClass = _classes.FirstOrDefault(y => y.Name == fieldSymbol.LatteType);
                }

                first = false;
            }
            else
            {
                if (x.InnerFieldAccess == null && !saveValue)
                {
                    AddInstruction(
                        GasSymbols.GenerateLeaFromMemory(
                            targetRegister,
                            currentClass.GetFieldOffset(x.ClassField),
                            targetRegister));
                }
                else
                {
                    AddInstruction(
                        GasSymbols.GenerateMovFromMemory(
                            targetRegister,
                            currentClass.GetFieldOffset(x.ClassField),
                            targetRegister));
                }

                var fieldSymbol = currentClass.Fields.FirstOrDefault(y => y.Name == x.ClassField);
                var isBasicType = TypesHelper.IsBasicType(fieldSymbol.LatteType);

                if (!isBasicType)
                {
                    currentClass = _classes.FirstOrDefault(y => y.Name == fieldSymbol.LatteType);
                }
            }

            x = x.InnerFieldAccess;
        }
    }
}