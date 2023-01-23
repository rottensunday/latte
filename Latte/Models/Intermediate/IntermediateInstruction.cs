namespace Latte.Models.Intermediate;

public class IntermediateInstruction : BaseIntermediateInstruction
{
    public IntermediateInstruction(
        RegisterTerm leftHandSide,
        Term firstOperand,
        InstructionType instructionType,
        Term secondOperand,
        int block)
    {
        LeftHandSide = leftHandSide;
        FirstOperand = firstOperand;
        InstructionType = instructionType;
        SecondOperand = secondOperand;
        Block = block;
    }

    public RegisterTerm LeftHandSide { get; set; }

    public Term FirstOperand { get; set; }

    public InstructionType InstructionType { get; set; }

    public Term SecondOperand { get; set; }

    public override string ToString() =>
        InstructionType switch
        {
            InstructionType.And => $"{LeftHandSide} = {FirstOperand} && {SecondOperand}",
            InstructionType.Assignment => FirstOperand is NewTerm nt ? $"{LeftHandSide} = new {nt.LatteType}" : $"{LeftHandSide} = {FirstOperand}",
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
            InstructionType.New => $"{LeftHandSide} = new {LeftHandSide.Type}",
            InstructionType.Null => $"",
            InstructionType.LhsFieldAccess => $"{LeftHandSide} = {FirstOperand}",
            InstructionType.RhsFieldAccess => $"{LeftHandSide} = {FirstOperand}",
            InstructionType.None => FirstOperand.ToString()
        } + $"  block {Block}" + $"      {InBoolExpr}";

    public override List<string> GetStringLiterals()
    {
        var firstOperandLiterals = FirstOperand?.GetStringLiterals() ?? new List<string>();
        var secondOperandLiterals = SecondOperand?.GetStringLiterals() ?? new List<string>();

        return firstOperandLiterals.Concat(secondOperandLiterals).ToList();
    }

    public override List<RegisterTerm> GetOperands()
    {
        var result = new List<RegisterTerm>();
        
        result.AddRange(FirstOperand?.GetUsedRegisters() ?? new List<RegisterTerm>());
        result.AddRange(SecondOperand?.GetUsedRegisters() ?? new List<RegisterTerm>());
        
        return result;
    }

    public override RegisterTerm GetTarget() => LeftHandSide;
    public override void SwitchRegisters(string used, string newRegister)
    {
        if (LeftHandSide?.FieldAccessTerm != null)
        {
            LeftHandSide.SwitchRegisters(used, newRegister);
        }
        
        if (FirstOperand != null)
        {
            FirstOperand.SwitchRegisters(used, newRegister);
        }

        if (SecondOperand != null)
        {
            SecondOperand.SwitchRegisters(used, newRegister);
        }
    }
}
