namespace Latte.Models.Intermediate;

public class IntermediateInstruction : BaseIntermediateInstruction
{
    public IntermediateInstruction(
        RegisterTerm leftHandSide,
        Term firstOperand,
        InstructionType instructionType,
        Term secondOperand)
    {
        LeftHandSide = leftHandSide;
        FirstOperand = firstOperand;
        InstructionType = instructionType;
        SecondOperand = secondOperand;
    }

    public RegisterTerm LeftHandSide { get; set; }

    public Term FirstOperand { get; set; }

    public InstructionType InstructionType { get; set; }

    public Term SecondOperand { get; set; }

    public override string ToString()
    {
        return InstructionType switch
        {
            InstructionType.And => $"{LeftHandSide} = {FirstOperand} && {SecondOperand}",
            InstructionType.Assignment => $"{LeftHandSide} = {FirstOperand}",
            InstructionType.Decrement => $"{LeftHandSide}--",
            InstructionType.Divide => $"{LeftHandSide} = {FirstOperand} / {SecondOperand}",
            InstructionType.Greater => $"{LeftHandSide} = {FirstOperand} > {SecondOperand}",
            InstructionType.Equal => $"{LeftHandSide} = {FirstOperand} == {SecondOperand}",
            InstructionType.Increment => $"{LeftHandSide}++",
            InstructionType.Less => $"{LeftHandSide} = {FirstOperand} < {SecondOperand}",
            InstructionType.Modulo => $"{LeftHandSide} = {FirstOperand} % {SecondOperand}",
            InstructionType.Multiply => $"{LeftHandSide} = {FirstOperand} * {SecondOperand}",
            InstructionType.Or => $"{LeftHandSide} = {FirstOperand} || {SecondOperand}",
            InstructionType.Return => $"return {FirstOperand?.ToString() ?? ""}",
            InstructionType.NotEqual => $"{LeftHandSide} = {FirstOperand} != {SecondOperand}",
            InstructionType.Subtract => $"{LeftHandSide} = {FirstOperand} - {SecondOperand}",
            InstructionType.AddInt => $"{LeftHandSide} = {FirstOperand} + {SecondOperand}",
            InstructionType.AddString => $"{LeftHandSide} = {FirstOperand} + {SecondOperand}",
            InstructionType.GreaterEqual => $"{LeftHandSide} = {FirstOperand} >= {SecondOperand}",
            InstructionType.LessEqual => $"{LeftHandSide} = {FirstOperand} <= {SecondOperand}",
            InstructionType.NegateBool => $"{LeftHandSide} = !{FirstOperand}",
            InstructionType.NegateInt => $"{LeftHandSide} = -{FirstOperand}",
            InstructionType.FunctionCall => $"{LeftHandSide} = {FirstOperand}",
            InstructionType.Jump => $"jmp {FirstOperand}",
            InstructionType.None => FirstOperand.ToString(),
        };
    }

    public override List<string> GetStringLiterals()
    {
        var firstOperandLiterals = FirstOperand?.GetStringLiterals() ?? new List<string>();
        var secondOperandLiterals = SecondOperand?.GetStringLiterals() ?? new List<string>();

        return firstOperandLiterals.Concat(secondOperandLiterals).ToList();
    }
}
