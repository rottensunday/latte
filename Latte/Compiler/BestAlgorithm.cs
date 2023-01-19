namespace Latte.Compiler;

using Latte.Models.Intermediate;

public class BestAlgorithm
{
    private Dictionary<Register, bool> _marked = new();
    private Stack<Register> _physicalRegistersStack = new();

    private List<Register> _physicalRegistersToUse = new()
    {
        Register.RDI,
        Register.RSI,
        Register.RDX,
        Register.RCX,
        Register.R8,
        Register.R9
    };

    private Dictionary<Register, int> _prNu = new();
    private Dictionary<Register, string> _prToVr = new();

    private Dictionary<string, Register?> _vrToPr = new();

    public void Process(List<IntermediateFunction> functions)
    {
        foreach (var function in functions)
        {
            ProcessFunction(function);
        }
    }

    private void ProcessFunction(IntermediateFunction function)
    {
        var blocks = function.Instructions
            .GroupBy(x => x.Block)
            .Where(x => x.Key >= 0)
            .ToList();

        foreach (var block in blocks)
        {
            var intermediateInstructions = block.OfType<IntermediateInstruction>().ToList();

            foreach (var instruction in intermediateInstructions)
            {
                if (instruction.LeftHandSide != null)
                {
                    instruction.LeftHandSide = new RegisterTerm(
                        instruction.LeftHandSide.Name,
                        instruction.LeftHandSide.Type,
                        instruction.LeftHandSide.Identifier);
                }

                if (instruction.FirstOperand is FunctionCallTerm functionCallTerm)
                {
                    // var registers = functionCallTerm.Arguments.OfType<RegisterTerm>().ToList();
                    //
                    // for (var i = 0; i < registers.Count; i++)
                    // {
                    //     registers[i] = new RegisterTerm(
                    //         registers[i].Name,
                    //         registers[i].Type,
                    //         registers[i].Identifier);
                    // }
                    //
                    //
                    // foreach (var arg in functionCallTerm.Arguments)
                    // {
                    //     args.Add(arg);
                    // }
                    
                    var args = new List<Term>();

                    instruction.FirstOperand = new FunctionCallTerm(functionCallTerm.Name, args);

                    foreach (var arg in functionCallTerm.Arguments)
                    {
                        if (arg is RegisterTerm x)
                        {
                            args.Add(new RegisterTerm(
                                x.Name,
                                x.Type,
                                x.Identifier));
                        }
                        else
                        {
                            args.Add(arg);
                        }
                    }
                } 

                if (instruction.FirstOperand is RegisterTerm firstOperand)
                {
                    instruction.FirstOperand = new RegisterTerm(
                        firstOperand.Name,
                        firstOperand.Type,
                        firstOperand.Identifier);
                }

                if (instruction.SecondOperand is RegisterTerm secondOperand)
                {
                    instruction.SecondOperand = new RegisterTerm(
                        secondOperand.Name,
                        secondOperand.Type,
                        secondOperand.Identifier);
                }
            }

            ProcessBlock(block.OfType<IntermediateInstruction>().ToList());
        }
    }

    private void ProcessBlock(List<IntermediateInstruction> instructions) => SetLiveRanges(instructions);

    // _vrToPr = new Dictionary<string, Register?>();
    // _prToVr = new Dictionary<Register, string>();
    // _prNu = new Dictionary<Register, int>();
    // _physicalRegistersStack = new Stack<Register>();
    // _marked = new Dictionary<Register, bool>();
    //
    // var registersUsed = instructions
    //     .SelectMany(x => new[] { x.LeftHandSide, x.FirstOperand as RegisterTerm, x.SecondOperand as RegisterTerm })
    //     .Where(x => x != null)
    //     .Distinct();
    //
    // foreach (var registerUsed in registersUsed)
    // {
    //     _vrToPr[registerUsed.VirtualRegister] = null;
    // }
    //
    // foreach (var physicalRegister in _physicalRegistersToUse)
    // {
    //     _prToVr[physicalRegister] = null;
    //     _prNu[physicalRegister] = -1;
    //     _physicalRegistersStack.Push(physicalRegister);
    // }
    //
    // foreach (var instruction in instructions)
    // {
    //     foreach (var kvp in _marked)
    //     {
    //         _marked[kvp.Key] = false;
    //     }
    //
    //     if (instruction.FirstOperand is RegisterTerm firstOperand)
    //     {
    //         var pr = _vrToPr[firstOperand.VirtualRegister];
    //
    //         if (pr == null)
    //         {
    //             
    //         }
    //
    //     }
    // }
    private void SetLiveRanges(List<IntermediateInstruction> instructions)
    {
        var vrCounter = 0;
        var vrName = $"v{vrCounter}";

        var registersInFunctions = instructions
            .Where(x => x.FirstOperand is FunctionCallTerm)
            .SelectMany(x => (x.FirstOperand as FunctionCallTerm).Arguments.OfType<RegisterTerm>())
            .Where(x => x != null);

        var registersUsed = instructions
            .Where(x => x.LeftHandSide != null || x is FunctionCallTerm)
            .SelectMany(x => new[] { x.LeftHandSide, x.FirstOperand as RegisterTerm, x.SecondOperand as RegisterTerm })
            .Where(x => x != null)
            .Concat(registersInFunctions)
            .DistinctBy(x => x.Name);

        var srToVr = new Dictionary<string, string>();
        var prevUse = new Dictionary<string, int>();

        foreach (var registerUsed in registersUsed)
        {
            srToVr[registerUsed.Name] = null;
            prevUse[registerUsed.Name] = -1;
        }

        var reversed =
            new List<IntermediateInstruction>(instructions.Where(x => x.LeftHandSide != null || x.FirstOperand is FunctionCallTerm));
        reversed.Reverse();
        var index = reversed.Count - 1;

        foreach (var instruction in reversed)
        {
            // for each operand, O, that OP defines do
            if (instruction.LeftHandSide != null)
            {
                if (srToVr[instruction.LeftHandSide.Name] == null)
                {
                    srToVr[instruction.LeftHandSide.Name] = vrName;
                    UpdateVr(ref vrCounter, ref vrName);
                }

                instruction.LeftHandSide.VirtualRegister = srToVr[instruction.LeftHandSide.Name];
                instruction.LeftHandSide.NextUse = prevUse[instruction.LeftHandSide.Name];
                prevUse[instruction.LeftHandSide.Name] = -1;
                srToVr[instruction.LeftHandSide.Name] = null;
            }

            // for each operand, O, that OP uses do
            if (instruction.FirstOperand is FunctionCallTerm functionCallTerm)
            {
                foreach (var term in functionCallTerm.Arguments)
                {
                    if (term is RegisterTerm argRegister)
                    {
                        if (srToVr[argRegister.Name] == null)
                        {
                            srToVr[argRegister.Name] = vrName;
                            UpdateVr(ref vrCounter, ref vrName);
                        }

                        argRegister.VirtualRegister = srToVr[argRegister.Name];
                        argRegister.NextUse = prevUse[argRegister.Name];
                        prevUse[argRegister.Name] = index;
                    }
                }
            }

            if (instruction.FirstOperand is RegisterTerm firstOperand)
            {
                if (srToVr[firstOperand.Name] == null)
                {
                    srToVr[firstOperand.Name] = vrName;
                    UpdateVr(ref vrCounter, ref vrName);
                }

                firstOperand.VirtualRegister = srToVr[firstOperand.Name];
                firstOperand.NextUse = prevUse[firstOperand.Name];
                prevUse[firstOperand.Name] = index;
            }

            if (instruction.SecondOperand is RegisterTerm secondOperand)
            {
                if (srToVr[secondOperand.Name] == null)
                {
                    srToVr[secondOperand.Name] = vrName;
                    UpdateVr(ref vrCounter, ref vrName);
                }

                secondOperand.VirtualRegister = srToVr[secondOperand.Name];
                secondOperand.NextUse = prevUse[secondOperand.Name];
                prevUse[secondOperand.Name] = index;
            }

            index--;
        }
    }

    private void UpdateVr(ref int counter, ref string name)
    {
        counter++;
        name = $"v{counter}";
    }
}