namespace Latte.Compiler;

using System.Security.Cryptography;
using System.Text;
using Latte.Models.Intermediate;

public class FlowGraph
{
    public Dictionary<Block, List<Block>> Neighbors { get; set; } = new();
}

public class Block
{
    public Block(List<BaseIntermediateInstruction> instructions, HashSet<string> liveOut, bool inBoolExpr)
    {
        Instructions = instructions;
        LiveOut = liveOut;
        InBoolExpr = inBoolExpr;
    }

    public List<BaseIntermediateInstruction> Instructions { get; set; }

    public HashSet<string> LiveOut { get; set; }

    public bool InBoolExpr { get; set; }

    public bool RedefinesRegisterAfterInstruction(BaseIntermediateInstruction instruction, string register)
    {
        var index = Instructions.FindIndex(x => x == instruction);
        var newInstructions = Instructions.Skip(index + 1);
        var redefinedRegisters = newInstructions.Select(x => x.GetTarget()).Where(x => x != null);

        return redefinedRegisters.Any(x => x.Name == register);
    }
}

public class FlowAnalyzer
{
    public static void SetLiveRanges(Block block)
    {
        var instructions = block.Instructions;

        var vrCounter = 0;
        var vrName = $"v{vrCounter}";

        var registersUsed = instructions
            .SelectMany(x => x.GetOperands())
            .Concat(instructions.Select(x => x.GetTarget()))
            .Where(x => x != null)
            .DistinctBy(x => x.Name);

        var srToVr = new Dictionary<string, string>();
        var prevUse = new Dictionary<string, int>();

        foreach (var registerUsed in registersUsed)
        {
            srToVr[registerUsed.Name] = null;
            prevUse[registerUsed.Name] = -1;
        }

        var reversed =
            new List<BaseIntermediateInstruction>(instructions);
        reversed.Reverse();
        var index = reversed.Count - 1;

        foreach (var baseInstruction in reversed)
        {
            if (baseInstruction is LabelIntermediateInstruction)
            {
                index--;
                continue;
            }

            if (baseInstruction is not IntermediateInstruction intermediateInstruction)
            {
                if (baseInstruction is IfIntermediateInstruction ifInstruction)
                {
                    if (ifInstruction.Condition is RegisterTerm registerTerm)
                    {
                        if (srToVr[registerTerm.Name] == null)
                        {
                            srToVr[registerTerm.Name] = vrName;
                            UpdateVr(ref vrCounter, ref vrName);
                        }

                        registerTerm.VirtualRegister = srToVr[registerTerm.Name];
                        registerTerm.NextUse = prevUse[registerTerm.Name];
                        prevUse[registerTerm.Name] = index;
                    }

                    if (ifInstruction.Condition is FieldAccessTerm fat)
                    {
                        var register = fat.InstanceRegister;

                        if (srToVr[register.Name] == null)
                        {
                            srToVr[register.Name] = vrName;
                            UpdateVr(ref vrCounter, ref vrName);
                        }

                        register.VirtualRegister = srToVr[register.Name];
                        register.NextUse = prevUse[register.Name];
                        prevUse[register.Name] = index;
                    
                        // if (srToVr[instruction.LeftHandSide.Name] == null)
                        // {
                        //     srToVr[instruction.LeftHandSide.Name] = vrName;
                        //     UpdateVr(ref vrCounter, ref vrName);
                        // }
                        //
                        // instruction.LeftHandSide.VirtualRegister = srToVr[instruction.LeftHandSide.Name];
                        // instruction.LeftHandSide.NextUse = prevUse[instruction.LeftHandSide.Name];
                        // prevUse[instruction.LeftHandSide.Name] = -1;
                        // srToVr[instruction.LeftHandSide.Name] = null;
                    }
                }
            }
            else
            {
                var instruction = intermediateInstruction;

                // for each operand, O, that OP defines do
                if (instruction.LeftHandSide != null)
                {
                    if (instruction.LeftHandSide.FieldAccessTerm != null)
                    {
                        var register = instruction.LeftHandSide.FieldAccessTerm.InstanceRegister;

                        if (srToVr[register.Name] == null)
                        {
                            srToVr[register.Name] = vrName;
                            UpdateVr(ref vrCounter, ref vrName);
                        }

                        register.VirtualRegister = srToVr[register.Name];
                        register.NextUse = prevUse[register.Name];
                        prevUse[register.Name] = index;
                    }
                    
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
                    foreach (var argRegister in functionCallTerm.GetUsedRegisters())
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

                if (instruction.FirstOperand is FieldAccessTerm fieldAccessTerm)
                {
                    var register = fieldAccessTerm.InstanceRegister;

                    if (srToVr[register.Name] == null)
                    {
                        srToVr[register.Name] = vrName;
                        UpdateVr(ref vrCounter, ref vrName);
                    }

                    register.VirtualRegister = srToVr[register.Name];
                    register.NextUse = prevUse[register.Name];
                    prevUse[register.Name] = index;
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
                
                if (instruction.SecondOperand is FieldAccessTerm fieldAccessTerm2)
                {
                    var register = fieldAccessTerm2.InstanceRegister;

                    if (srToVr[register.Name] == null)
                    {
                        srToVr[register.Name] = vrName;
                        UpdateVr(ref vrCounter, ref vrName);
                    }

                    register.VirtualRegister = srToVr[register.Name];
                    register.NextUse = prevUse[register.Name];
                    prevUse[register.Name] = index;
                }
            }

            index--;
        }
    }

    public static void PropagateCopies(List<Block> blocks)
    {
        BuildLiveOutSets(blocks);

        foreach (var block in blocks)
        {
            var instructions = block.Instructions;
            var instructionToRemove = -1;
            var changed = true;

            while (changed)
            {
                changed = false;

                for (var i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i] is IntermediateInstruction
                        {
                            InstructionType: InstructionType.Assignment, FirstOperand: RegisterTerm rt, LeftHandSide.FieldAccessTerm: null
                        } intermediate)
                    {
                        var leftRegister = intermediate.LeftHandSide;
                        var rightRegister = rt;
                        var skippedInstructions = instructions.Skip(i + 1).ToList();

                        var leftRegisterNextDefinitionIndex = skippedInstructions
                            .FindIndex(
                                x => x is IntermediateInstruction intermediate2 &&
                                     intermediate2.LeftHandSide?.Name == leftRegister.Name);

                        var leftRegisterHasNextUse = skippedInstructions
                            .Any(x => x.GetOperands().Any(x => x.Name == leftRegister.Name));

                        var rightRegisterNextDefinitionIndex = skippedInstructions
                            .FindIndex(
                                x => x is IntermediateInstruction intermediate2 &&
                                     intermediate2.LeftHandSide?.Name == rightRegister.Name);

                        if ((leftRegisterNextDefinitionIndex >= 0 &&
                             rightRegisterNextDefinitionIndex > leftRegisterNextDefinitionIndex)
                            || (rightRegisterNextDefinitionIndex == -1 && leftRegisterNextDefinitionIndex != -1))
                        {
                            changed = true;
                            instructionToRemove = i;

                            foreach (var instruction in skippedInstructions.Take(leftRegisterNextDefinitionIndex + 1))
                            {
                                instruction.SwitchRegisters(leftRegister.Name, rightRegister.Name);
                            }

                            break;
                        }

                        if (leftRegisterNextDefinitionIndex == -1 && rightRegisterNextDefinitionIndex == -1 &&
                            !block.LiveOut.Contains(leftRegister.Name))
                        {
                            changed = true;
                            instructionToRemove = i;

                            foreach (var instruction in skippedInstructions)
                            {
                                instruction.SwitchRegisters(leftRegister.Name, rightRegister.Name);
                            }

                            break;
                        }

                        if (!leftRegisterHasNextUse && !block.LiveOut.Contains(leftRegister.Name))
                        {
                            changed = true;
                            instructionToRemove = i;
                            
                            if (leftRegisterNextDefinitionIndex >= 0)
                            {
                                foreach (var instruction in skippedInstructions.Take(leftRegisterNextDefinitionIndex + 1))
                                {
                                    instruction.SwitchRegisters(leftRegister.Name, rightRegister.Name);
                                }
                            }
                            else
                            {
                                foreach (var instruction in skippedInstructions)
                                {
                                    instruction.SwitchRegisters(leftRegister.Name, rightRegister.Name);
                                }
                            }
                            
                            break;
                        }
                    }
                }

                if (changed)
                {
                    instructions.RemoveAt(instructionToRemove);
                }
            }
        }
    }

    public static void EliminateCommonSubexpressions(List<Block> blocks)
    {
        var acceptedInstructions = new List<InstructionType>
        {
            InstructionType.AddInt,
            InstructionType.AddString,
            InstructionType.Subtract,
            InstructionType.Multiply,
            InstructionType.Modulo,
            InstructionType.Divide,
            InstructionType.Greater,
            InstructionType.GreaterEqual,
            InstructionType.Less,
            InstructionType.LessEqual,
            InstructionType.Equal,
            InstructionType.NotEqual,
            InstructionType.NegateBool,
            InstructionType.NegateInt,
            // InstructionType.Assignment
        };

        foreach (var instructions in blocks.Select(x => x.Instructions))
        {
            var hashes = new Dictionary<string, string>();
            var opHashes = new Dictionary<string, List<RegisterTerm>>();

            foreach (var instruction in instructions
                         .OfType<IntermediateInstruction>()
                         .Where(x => acceptedInstructions.Contains(x.InstructionType) && x.LeftHandSide.FieldAccessTerm == null))
            {
                var firstOperandStr = instruction.FirstOperand.ToString();
                var secondOperandStr = instruction.SecondOperand?.ToString();
                string secondOperandHash = "";

                if (!hashes.TryGetValue(firstOperandStr, out var firstOperandHash))
                {
                    firstOperandHash = GetHash(firstOperandStr);
                    hashes[firstOperandStr] = firstOperandHash;
                }

                if (secondOperandStr != null)
                {
                    if (!hashes.TryGetValue(secondOperandStr, out secondOperandHash))
                    {
                        secondOperandHash = GetHash(secondOperandStr);
                        hashes[secondOperandStr] = secondOperandHash;
                    }
                }

                var opHash = GetHash($"{firstOperandHash} {instruction.InstructionType} {secondOperandHash}");

                if (!opHashes.TryGetValue(opHash, out var opRegisterList))
                {
                    foreach (var kvp in opHashes)
                    {
                        kvp.Value.RemoveAll(x => x == instruction.LeftHandSide);
                    }

                    opHashes = opHashes
                        .Where(x => x.Value.Any())
                        .ToDictionary(x => x.Key, x => x.Value);

                    opHashes[opHash] = new List<RegisterTerm> { instruction.LeftHandSide };
                    hashes[instruction.LeftHandSide.ToString()] = opHash;
                }
                else
                {
                    instruction.FirstOperand = opRegisterList.First();
                    instruction.InstructionType = InstructionType.Assignment;
                    instruction.SecondOperand = null;

                    opRegisterList.Add(instruction.LeftHandSide);
                }
            }
        }
    }

    public static void BuildLiveOutSets(List<Block> blocks)
    {
        var flowGraph = BuildFlowGraph(blocks);

        var uevar =
            new Dictionary<Block, HashSet<string>>();

        var varkill =
            new Dictionary<Block, HashSet<string>>();

        foreach (var block in blocks)
        {
            var uevarSet = new HashSet<string>();
            var varkillSet = new HashSet<string>();

            uevar[block] = uevarSet;
            varkill[block] = varkillSet;

            foreach (var instruction in block.Instructions)
            {
                var operandRegisters = instruction.GetOperands();
                var resultRegister = instruction.GetTarget();

                foreach (var operandRegister in operandRegisters)
                {
                    if (!varkillSet.Contains(operandRegister.Name))
                    {
                        uevarSet.Add(operandRegister.Name);
                    }
                }

                if (resultRegister != null)
                {
                    varkillSet.Add(resultRegister.Name);
                }
            }
        }

        var varKillSum = varkill.Values.SelectMany(x => x).ToHashSet();
        var varKillComplement =
            new Dictionary<Block, HashSet<string>>();

        var liveOut =
            new Dictionary<Block, HashSet<string>>();
        var changed = true;

        foreach (var block in blocks)
        {
            liveOut[block] = new HashSet<string>();
            varKillComplement[block] = varKillSum.Except(varkill[block]).ToHashSet();
        }

        while (changed)
        {
            changed = false;

            foreach (var block in blocks)
            {
                var contributions = new HashSet<string>();

                foreach (var neighbor in flowGraph.Neighbors[block])
                {
                    var contribution = uevar[neighbor].Concat(liveOut[neighbor].Intersect(varKillComplement[neighbor]))
                        .ToHashSet();

                    foreach (var elem in contribution)
                    {
                        contributions.Add(elem);
                    }
                }

                if (contributions.Count <= liveOut[block].Count)
                {
                    continue;
                }

                changed = true;
                liveOut[block] = contributions;
            }
        }

        foreach (var block in blocks)
        {
            block.LiveOut = liveOut[block];
        }
    }

    public static FlowGraph BuildFlowGraph(List<Block> blocks)
    {
        var graph = new FlowGraph();
        var labelBlockMap = new Dictionary<string, Block>();

        foreach (var label in blocks.SelectMany(x => x.Instructions).OfType<LabelIntermediateInstruction>())
        {
            if (!label.IsJump)
            {
                labelBlockMap.Add(
                    label.LabelTerm.Label,
                    blocks.FirstOrDefault(x => x.Instructions.First().Block == label.Block));
            }
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var neighbors = new List<Block>();
            graph.Neighbors[block] = neighbors;


            if (block.Instructions.Last() is IfIntermediateInstruction ifIntermediateInstruction)
            {
                var targetIf = labelBlockMap.GetValueOrDefault(ifIntermediateInstruction.JumpLabel.LabelTerm.Label);

                if (targetIf != null)
                {
                    neighbors.Add(targetIf);
                }

                if (ifIntermediateInstruction.IfElseEndLabel != null)
                {
                    var targetElse =
                        labelBlockMap.GetValueOrDefault(ifIntermediateInstruction.IfElseEndLabel.LabelTerm.Label);

                    if (targetElse != null)
                    {
                        neighbors.Add(targetElse);
                    }
                }
            }

            if (block.Instructions.Last() is LabelIntermediateInstruction { IsJump: true } lt)
            {
                var target = labelBlockMap[lt.LabelTerm.Label];
                neighbors.Add(target);
            }
            else if (i < blocks.Count - 1)
            {
                neighbors.Add(blocks[i + 1]);
            }
        }

        return graph;
    }

    public static List<Block> BuildBlocks(IntermediateFunction function)
    {
        var currentBlock = 0;

        foreach (var instruction in function.Instructions)
        {
            if (instruction is LabelIntermediateInstruction { IsJump: false } lt &&
                LabelHasCorrespondingJump(lt.LabelTerm.Label, function.Instructions))
            {
                if (function.Instructions.Any(x => x.Block == currentBlock)
                    || function.Instructions.All(x => x is LabelIntermediateInstruction))
                {
                    currentBlock++;
                }
            }

            instruction.Block = currentBlock;

            switch (instruction)
            {
                case IfIntermediateInstruction:
                    currentBlock++;
                    break;
            }
        }

        var blocksOfInstructions = function.Instructions
            .GroupBy(x => x.Block)
            .ToList();

        var blocks = new List<Block>();

        foreach (var block in blocksOfInstructions)
        {
            var blockInstance = new Block(block.ToList(), new HashSet<string>(), block.First().InBoolExpr);

            blocks.Add(blockInstance);
        }

        return blocks;
    }

    public static void RefreshInstructions(IntermediateFunction function)
    {
        foreach (var instruction in function.Instructions.OfType<IntermediateInstruction>())
        {
            if (instruction.LeftHandSide != null)
            {
                FieldAccessTerm accessTerm = null;

                if (instruction.LeftHandSide.FieldAccessTerm != null)
                {
                    accessTerm = instruction.LeftHandSide.FieldAccessTerm;
                    accessTerm = new FieldAccessTerm(accessTerm.ClassSymbol, accessTerm.InstanceName, accessTerm.InnerFieldAccess, new RegisterTerm(accessTerm.InstanceRegister.Name, accessTerm.InstanceRegister.Type), accessTerm.Type);
                }
                
                instruction.LeftHandSide = new RegisterTerm(
                    instruction.LeftHandSide.Name,
                    instruction.LeftHandSide.Type,
                    instruction.LeftHandSide.Identifier,
                    fat: accessTerm);
            }

            if (instruction.FirstOperand is FunctionCallTerm functionCallTerm)
            {
                var args = new List<Term>();

                instruction.FirstOperand = new FunctionCallTerm(functionCallTerm.Name, args);

                foreach (var arg in functionCallTerm.Arguments)
                {
                    if (arg is RegisterTerm x)
                    {
                        args.Add(
                            new RegisterTerm(
                                x.Name,
                                x.Type,
                                x.Identifier,
                                fat: x.FieldAccessTerm));
                    }
                    else if (arg is FieldAccessTerm fat)
                    {
                        args.Add(
                            new FieldAccessTerm(fat.ClassSymbol, fat.InstanceName, fat.InnerFieldAccess, new RegisterTerm(fat.InstanceRegister.Name, fat.InstanceRegister.Type), fat.Type));
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
                    firstOperand.Identifier,
                    fat: firstOperand.FieldAccessTerm);
            }

            if (instruction.SecondOperand is RegisterTerm secondOperand)
            {
                instruction.SecondOperand = new RegisterTerm(
                    secondOperand.Name,
                    secondOperand.Type,
                    secondOperand.Identifier,
                    fat: secondOperand.FieldAccessTerm);
            }

            if (instruction.FirstOperand is FieldAccessTerm fieldAccessTerm)
            {
                fieldAccessTerm.InstanceRegister = new RegisterTerm(
                    fieldAccessTerm.InstanceRegister.Name,
                    fieldAccessTerm.InstanceRegister.Type,
                    fieldAccessTerm.InstanceRegister.Identifier,
                    fat: fieldAccessTerm.InstanceRegister.FieldAccessTerm);
            }
            
            if (instruction.SecondOperand is FieldAccessTerm fieldAccessTerm2)
            {
                fieldAccessTerm2.InstanceRegister = new RegisterTerm(
                    fieldAccessTerm2.InstanceRegister.Name,
                    fieldAccessTerm2.InstanceRegister.Type,
                    fieldAccessTerm2.InstanceRegister.Identifier,
                    fat: fieldAccessTerm2.InstanceRegister.FieldAccessTerm);
            }
        }

        foreach (var ifInstruction in function.Instructions.OfType<IfIntermediateInstruction>())
        {
            if (ifInstruction.Condition is RegisterTerm registerTerm)
            {
                ifInstruction.Condition = new RegisterTerm(
                    registerTerm.Name,
                    registerTerm.Type,
                    registerTerm.Identifier,
                    fat: registerTerm.FieldAccessTerm);
            }
        }
    }

    private static bool LabelHasCorrespondingJump(string label, List<BaseIntermediateInstruction> instructions) =>
        instructions.Any(
            x =>
                (x is IfIntermediateInstruction inter &&
                 inter.JumpLabel.LabelTerm.Label == label) ||
                (x is LabelIntermediateInstruction { IsJump: true } lt1 &&
                 lt1.LabelTerm.Label == label));

    private static string GetHash(string input)
    {
        using var algorithm = SHA256.Create();
        var data = algorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sBuilder = new StringBuilder();

        foreach (var t in data)
        {
            sBuilder.Append(t.ToString("x2"));
        }

        return sBuilder.ToString();
    }

    private static void UpdateVr(ref int counter, ref string name)
    {
        counter++;
        name = $"v{counter}";
    }
}