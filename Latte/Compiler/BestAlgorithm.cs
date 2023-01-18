namespace Latte.Compiler;

using Latte.Models.Intermediate;

public class BestAlgorithm
{
    private List<Register> _physicalRegistersToUse = new()
    {
        Register.RDI,
        Register.RSI,
        Register.RDX,
        Register.RCX,
        Register.R8,
        Register.R9
    };
    
    private Dictionary<string, Register?> _vrToPr = new ();
    private Dictionary<Register, string> _prToVr = new ();
    private Dictionary<Register, int> _prNu = new ();
    private Stack<Register> _physicalRegistersStack = new ();
    private Dictionary<Register, bool> _marked = new ();
    
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
                        instruction.LeftHandSide.Type);
                }

                if (instruction.FirstOperand is RegisterTerm firstOperand)
                {
                    instruction.FirstOperand = new RegisterTerm(firstOperand.Name, firstOperand.Type);
                }

                if (instruction.SecondOperand is RegisterTerm secondOperand)
                {
                    instruction.SecondOperand = new RegisterTerm(secondOperand.Name, secondOperand.Type);
                }
            }
            
            ProcessBlock(block.OfType<IntermediateInstruction>().ToList());
        }
    }

    private void ProcessBlock(List<IntermediateInstruction> instructions)
    {
        SetLiveRanges(instructions);
        
        _vrToPr = new Dictionary<string, Register?>();
        _prToVr = new Dictionary<Register, string>();
        _prNu = new Dictionary<Register, int>();
        _physicalRegistersStack = new Stack<Register>();
        _marked = new Dictionary<Register, bool>();
        
        var registersUsed = instructions
            .SelectMany(x => new[] { x.LeftHandSide, x.FirstOperand as RegisterTerm, x.SecondOperand as RegisterTerm })
            .Where(x => x != null)
            .Distinct();

        foreach (var registerUsed in registersUsed)
        {
            _vrToPr[registerUsed.VirtualRegister] = null;
        }

        foreach (var physicalRegister in _physicalRegistersToUse)
        {
            _prToVr[physicalRegister] = null;
            _prNu[physicalRegister] = -1;
            _physicalRegistersStack.Push(physicalRegister);
        }

        foreach (var instruction in instructions)
        {
            foreach (var kvp in _marked)
            {
                _marked[kvp.Key] = false;
            }

            if (instruction.FirstOperand is RegisterTerm firstOperand)
            {
                var pr = _vrToPr[firstOperand.VirtualRegister];

                if (pr == null)
                {
                    
                }

            }
        }
    }

    private void SetLiveRanges(List<IntermediateInstruction> instructions)
    {
        var vrCounter = 0;
        var vrName = $"v{vrCounter}";

        var registersUsed = instructions
            .SelectMany(x => new[] { x.LeftHandSide, x.FirstOperand as RegisterTerm, x.SecondOperand as RegisterTerm })
            .Where(x => x != null)
            .Distinct();

        var srToVr = new Dictionary<string, string>();
        var prevUse = new Dictionary<string, int>();

        foreach (var registerUsed in registersUsed)
        {
            srToVr[registerUsed.Name] = null;
            prevUse[registerUsed.Name] = -1;
        }

        var reversed = new List<IntermediateInstruction>(instructions.Where(x => x.LeftHandSide != null));
        reversed.Reverse();
        var index = reversed.Count;

        foreach (var instruction in reversed)
        {
            // for each operand, O, that OP defines do
            if (srToVr[instruction.LeftHandSide.Name] == null)
            {
                srToVr[instruction.LeftHandSide.Name] = vrName;
                UpdateVr(ref vrCounter, ref vrName);
            }

            instruction.LeftHandSide.VirtualRegister = srToVr[instruction.LeftHandSide.Name];
            instruction.LeftHandSide.NextUse = prevUse[instruction.LeftHandSide.Name];
            prevUse[instruction.LeftHandSide.Name] = -1;
            srToVr[instruction.LeftHandSide.Name] = null;

            // for each operand, O, that OP uses do
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

    private Register GetAPr(string vr, int nu)
    {
        Register result = Register.None;
        
        if (_physicalRegistersStack.Any())
        {
            result = _physicalRegistersStack.Pop();
        }
        else
        {
            var unmarked = _marked.FirstOrDefault(x => !x.Value).Key;
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

    private void UpdateVr(ref int counter, ref string name)
    {
        counter++;
        name = $"v{counter}";
    }
}