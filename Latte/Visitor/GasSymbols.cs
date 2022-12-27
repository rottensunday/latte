namespace Latte;

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
    R9
}

public static class GasSymbols
{
    public const string Prefix = @"
.extern _printInt
.extern _printString
.extern _readString
.extern _readInt
.extern _error
.intel_syntax
.global _main";

    public static string GenerateFunctionSymbol(string name)
        => $"_{name}:";

    public static string GenerateFunctionCall(string name)
        => $"CALL _{name}";

    public static string GenerateMov(Register from, Register to)
        => $"MOV {to}, {from}";

    public static string GenerateMov(int value, Register to)
        => $"MOV {to}, {value}";

    public static string GenerateMovFromOffset(int offset, Register to) 
        => $"MOV {to}, [RBP{BuildOffsetString(offset)}]";

    public static string GenerateMovToOffset(int offset, Register from)
        => $"MOV [RBP{BuildOffsetString(offset)}], {from}";
    
    public static string GenerateConstantMovToMemory(int offset, int value)
        => $"MOV QWORD PTR [RBP{BuildOffsetString(offset)}], {value}";

    public static string GenerateUnconditionalJump(string labelName)
        => $"JMP {labelName}";

    public static string GenerateLabel(string labelName)
        => $"{labelName}:";

    public static string GenerateIncrement(Register register)
        => $"INC {register}";

    public static string GenerateDecrement(Register register)
        => $"DEC {register}";

    public static string GenerateNegation(Register register)
        => $"NEG {register}";

    public static string GenerateNot(Register register)
        => $"XOR {register}, 1";
    
    public static string GenerateSubtract(Register left, Register right)
        => $"SUB {left}, {right}";

    public static string GenerateSubtract(Register register, int value)
        => $"SUB {register}, {value}";

    public static string GenerateAdd(Register left, Register right)
        => $"ADD {left}, {right}";

    public static string GenerateAdd(Register left, int right)
        => $"ADD {left}, {right}";
    
    public static string GenerateMultiply(Register left, Register right)
        => $"IMUL {left}, {right}";

    public static string GenerateMultiply(Register left, int right)
        => $"IMUL {left}, {right}";

    public static string GenerateDivide(Register left, Register right)
    {
        var result = $"MOV RAX, {left}\n";
        result += "CQO \n";
        result += $"IDIV {right}";

        return result;
    }

    public static string GenerateDivide(Register left, int right)
    {
        var result = $"MOV RAX, {left}\n";
        result += "CQO \n";
        result += $"IDIV {right}";

        return result;
    }

    public static string GenerateCmp(Register left, Register right)
        => $"CMP {left}, {right}";

    public static string GenerateCmp(Register left, int right)
        => $"CMP {left}, {right}";

    public static string GenerateAnd(Register left, Register right)
        => $"AND {left}, {right}";

    public static string GenerateAnd(Register left, int right)
        => $"AND {left}, {right}";

    public static string GenerateOr(Register left, Register right)
        => $"OR {left}, {right}";

    public static string GenerateOr(Register left, int right)
        => $"OR {left}, {right}";

    public static string GenerateJumpEqual(string label)
        => $"JE {label}";

    public static string GenerateJumpNotEqual(string label)
        => $"JNE {label}";

    public static string GenerateJumpGreater(string label)
        => $"JG {label}";

    public static string GenerateJumpGreaterEqual(string label)
        => $"JGE {label}";

    public static string GenerateJumpLess(string label)
        => $"JL {label}";

    public static string GenerateJumpLessEqual(string label)
        => $"JLE {label}";

    public static string ZeroRegister(Register register)
        => $"XOR {register}, {register}";

    public static string GeneratePush(Register from)
        => $"PUSH {from}";

    public static string GeneratePush(int value)
        => $"PUSH {value}";

    public static string GeneratePop(Register to)
        => $"POP {to}";

    public static string GenerateRet()
        => "RET";

    private static string BuildOffsetString(int offset) =>
        offset switch
        {
            > 0 => $"+{offset}",
            < 0 => $"{offset}",
            _ => ""
        };
}

