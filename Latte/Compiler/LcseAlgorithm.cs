namespace Latte.Compiler;

using System.Security.Cryptography;
using System.Text;
using Latte.Models.Intermediate;

public class LcseAlgorithm
{
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
            ProcessBlock(block.ToList());
        }
    }

    private void ProcessBlock(List<BaseIntermediateInstruction> instructions)
    {
        var hashes = new Dictionary<string, string>();
        var opHashes = new Dictionary<string, List<RegisterTerm>>();
        
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
            InstructionType.Assignment
        };

        foreach (var instruction in instructions
                     .OfType<IntermediateInstruction>()
                     .Where(x => x.LeftHandSide != null && acceptedInstructions.Contains(x.InstructionType)))
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

    private string GetHash(string input)
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
}