namespace Latte.Models.Intermediate;

public enum InstructionType
{
    None,
    Assignment,
    Increment,
    Decrement,
    Return,
    NegateInt,
    NegateBool,
    AddInt,
    AddString,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    Equal,
    NotEqual,
    And,
    Or,
    FunctionCall,
    Jump
}

// public static class InstructionTypeExtensions
// {
//     public static InstructionType Negate(this InstructionType instructionType)
//     {
//         return instructionType switch
//         {
//             InstructionType.And => 
//         };
//     }
// }
