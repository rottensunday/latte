namespace Latte.Compiler;

using System.Diagnostics.CodeAnalysis;

// RDI, RSI, RDX, RCX, R8, R9
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum Register
{
    None,
    RAX,
    RDI,
    RSI,
    RDX,
    RCX,
    RBP,
    RSP,
    R8,
    R9,
    DIL,
    SIL,
    AL,
    BL,
    DL,
    CL,
    BPL,
    SPL,
    R8L,
    R8B,
    R9B,
    RBX,
    R12,
    R13,
    R14,
    R15,
    R12B,
    R13B,
    R14B,
    R15B,
    R10,
    R11,
    R10B,
    R11B
}

public static class RegisterExtensions
{
    public static Register GetLowByte(this Register register) =>
        register switch
        {
            Register.RAX => Register.AL,
            Register.RDI => Register.DIL,
            Register.RSI => Register.SIL,
            Register.RDX => Register.DL,
            Register.RCX => Register.CL,
            Register.RBP => Register.BPL,
            Register.RSP => Register.SPL,
            Register.R8 => Register.R8B,
            Register.R9 => Register.R9B,
            Register.R12 => Register.R12B,
            Register.R13 => Register.R13B,
            Register.R14 => Register.R14B,
            Register.R15 => Register.R15B,
            Register.RBX => Register.BL,
            Register.R10 => Register.R10B,
            Register.R11 => Register.R11B
        };
}

public static class GasSymbols
{
    public const string Prefix = @"
.extern _printInt
.extern _printString
.extern _readString
.extern _readInt
.extern _error
.extern _concatStrings
.extern _compareStrings
.intel_syntax
.global _main";

    public static readonly List<Register> ParamRegisters = new()
    {
        Register.RDI,
        Register.RSI,
        Register.RDX,
        Register.RCX,
        Register.R8,
        Register.R9
    };
    
    public static readonly List<Register> PreservedRegisters = new()
    {
        Register.RBX,
        Register.R12,
        Register.R13,
        Register.R14,
        Register.R15
    };
    
    public static readonly List<Register> NotPreservedRegisters = new()
    {
        Register.RDI,
        Register.RSI,
        Register.RDX,
        Register.RCX,
        Register.R8,
        Register.R9,
        Register.R10,
        Register.R11
    };

    public static readonly List<Register> AllocationRegisters = new()
    {
        Register.RDI,
        Register.RCX,
        // Register.R8,
        Register.R9,
        Register.R10,
        Register.R11,
        Register.RSI,
        Register.RDX,
        Register.RBX,
        Register.R12,
        Register.R13,
        Register.R14,
        Register.R15,
    };

    public static string GenerateFunctionSymbol(string name) => $"_{name}:";

    public static string GenerateFunctionCall(string name) => $"CALL _{name}";

    public static string GenerateMov(Register from, Register to, bool toMemory = false)
    {
        if (toMemory)
        {
            return $"MOV [{to}], {from}";
        }
        
        return $"MOV {to}, {from}";
    }

    public static string GenerateMovFromMemory(Register baseRegister, int offset, Register targetRegister)
    {
        if (offset == 0)
        {
            return $"MOV {targetRegister}, [{baseRegister}]";
        }

        return $"MOV {targetRegister}, [{baseRegister}+{offset}]";
    }
    
    public static string GenerateLeaFromMemory(Register baseRegister, int offset, Register targetRegister)
    {
        if (offset == 0)
        {
            return $"LEA {targetRegister}, [{baseRegister}]";
        }

        return $"LEA {targetRegister}, [{baseRegister}+{offset}]";
    }
    
    // public static string GenerateMovToMemory(Register source, Register target)
    // {
    //     return $"MOV [{target}], {source}";
    // }
    //
    // public static string GenerateMovToMemory(int value, Register target)
    // {
    //     return $"MOV [{target}], {value}";
    // }

    public static string GenerateMov(int value, Register to, bool toMemory = false)
    {
        if (toMemory)
        {
            return $"MOV QWORD PTR [{to}], {value}";
        }
        
        return $"MOV {to}, {value}";
    }

    public static string GenerateLeaForLiteral(string label, Register to) => $"LEA {to}, {label}[RIP]";

    public static string GenerateMovzx(int value, Register to) => $"MOVZX {to}, {value}";

    public static string GenerateMovzxFromOffset(int offset, Register to) =>
        $"MOVZX {to}, BYTE PTR [RBP{BuildOffsetString(offset)}]";

    public static string GenerateMovFromOffset(int offset, Register to) =>
        $"MOV {to}, [RBP{BuildOffsetString(offset)}]";

    public static string GenerateMovToOffset(int offset, Register from) =>
        $"MOV [RBP{BuildOffsetString(offset)}], {from}";

    public static string GenerateConstantMovToMemory(int offset, int value, bool isBool) =>
        $"MOV {(isBool ? "BYTE" : "QWORD")} PTR [RBP{BuildOffsetString(offset)}], {value}";

    public static string GenerateUnconditionalJump(string labelName) => $"JMP {labelName}";

    public static string GenerateLabel(string labelName) => $"{labelName}:";

    public static string GenerateIncrement(Register register) => $"INC {register}";
    
    public static string GenerateIncrementAddress(Register register) => $"INC QWORD PTR [{register}]";

    public static string GenerateIncrementToOffset(int offset) => $"INC QWORD PTR [RBP{BuildOffsetString(offset)}]";

    public static string GenerateDecrement(Register register) => $"DEC {register}";
    
    public static string GenerateDecrementAddress(Register register) => $"DEC QWORD PTR [{register}]";

    public static string GenerateDecrementToOffset(int offset) => $"DEC QWORD PTR [RBP{BuildOffsetString(offset)}]";

    public static string GenerateNegation(Register register) => $"NEG {register}";
    
    // public static string GenerateNegationAddress(Register register) => $"NEG QWORD PTR [{register}]";

    public static string GenerateNegationToOffset(int offset) => $"NEG QWORD PTR [RBP{BuildOffsetString(offset)}]";

    public static string GenerateNot(Register register) => $"XOR {register}, 1";
    
    public static string GenerateNotToOffset(int offset) => $"XOR QWORD PTR [RBP{BuildOffsetString(offset)}], 1";

    public static string GenerateSubtract(Register left, Register right) => $"SUB {left}, {right}";

    public static string GenerateSubtract(Register register, int value) => $"SUB {register}, {value}";

    public static string GenerateAdd(Register left, Register right) => $"ADD {left}, {right}";

    public static string GenerateAdd(Register left, int right) => $"ADD {left}, {right}";

    public static string GenerateMultiply(Register left, Register right) => $"IMUL {left}, {right}";

    public static string GenerateMultiply(Register left, int right) => $"IMUL {left}, {right}";

    public static string GenerateDivide(Register left, Register right)
    {
        var result = $"MOV RAX, {left}\n";
        result += "CQO \n";
        result += $"IDIV {right}";

        return result;
    }

    public static string GenerateDivide(Register left, int right, Register help)
    {
        var result = $"MOV RAX, {left}\n";
        result += "CQO \n";
        // result += "PUSH RDI\n";
        result += $"{GenerateMov(right, help)}\n";
        result += $"IDIV {help}\n";
        // result += "POP RDI";

        return result;
    }
    
    public static string GenerateDivide(int left, Register right)
    {
        var result = $"MOV RAX, {left}\n";
        result += "CQO \n";
        result += $"IDIV {right}";

        return result;
    }

    public static string GenerateCmp(Register left, Register right) => $"CMP {left}, {right}";

    public static string GenerateCmp(Register left, int right) => $"CMP {left}, {right}";

    public static string GenerateSetEqualToOffset(int offset) => $"SETE [RBP{BuildOffsetString(offset)}]";

    public static string GenerateSetNotEqualToOffset(int offset) => $"SETNE [RBP{BuildOffsetString(offset)}]";

    public static string GenerateSetGreaterToOffset(int offset) => $"SETG [RBP{BuildOffsetString(offset)}]";

    public static string GenerateSetGreaterEqualToOffset(int offset) => $"SETGE [RBP{BuildOffsetString(offset)}]";

    public static string GenerateSetLessToOffset(int offset) => $"SETL [RBP{BuildOffsetString(offset)}]";

    public static string GenerateSetLessEqualToOffset(int offset) => $"SETLE [RBP{BuildOffsetString(offset)}]";
    
    public static string GenerateSetEqual(Register register)
    {
        return $"MOV {register}, 0\n SETE {register.GetLowByte()}";
    }

    public static string GenerateSetNotEqual(Register register)
    {
        return $"MOV {register}, 0\nSETNE {register.GetLowByte()}";
    }

    public static string GenerateSetGreater(Register register)
    {
        return $"MOV {register}, 0\nSETG {register.GetLowByte()}";
    }

    public static string GenerateSetGreaterEqual(Register register)
    {
        return $"MOV {register}, 0\nSETGE {register.GetLowByte()}";
    }

    public static string GenerateSetLess(Register register)
    {
        return $"MOV {register}, 0\nSETL {register.GetLowByte()}";
    }

    public static string GenerateSetLessEqual(Register register)
    {
        return $"MOV {register}, 0\nSETLE {register.GetLowByte()}";
    }

    public static string GenerateAnd(Register left, Register right) => $"AND {left}, {right}";

    public static string GenerateAnd(Register left, int right) => $"AND {left}, {right}";

    public static string GenerateOr(Register left, Register right) => $"OR {left}, {right}";

    public static string GenerateOr(Register left, int right) => $"OR {left}, {right}";

    public static string GenerateJumpEqual(string label) => $"JE {label}";

    public static string GenerateJumpNotEqual(string label) => $"JNE {label}";

    public static string GenerateJumpGreater(string label) => $"JG {label}";

    public static string GenerateJumpGreaterEqual(string label) => $"JGE {label}";

    public static string GenerateJumpLess(string label) => $"JL {label}";

    public static string GenerateJumpLessEqual(string label) => $"JLE {label}";

    public static string ZeroRegister(Register register) => $"XOR {register}, {register}";

    public static string GenerateTest(Register left, Register right) => $"TEST {left}, {right}";

    public static string GeneratePush(Register from) => $"PUSH {from}";

    public static string GeneratePush(int value) => $"PUSH {value}";

    public static string GeneratePop(Register to) => $"POP {to}";

    public static string GenerateRet() => "RET";

    private static string BuildOffsetString(int offset) =>
        offset switch
        {
            > 0 => $"+{offset}",
            < 0 => $"{offset}",
            _ => ""
        };
}
