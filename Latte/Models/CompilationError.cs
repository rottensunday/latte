namespace Latte.Models;

using Antlr4.Runtime;

public class CompilationError
{
    public CompilationError(CompilationErrorType type, int line, int column, string message = "")
    {
        ErrorType = type;
        Line = line;
        Column = column;
        Message = message;
    }

    public CompilationError(CompilationErrorType type, IToken token, string message = "")
    {
        ErrorType = type;
        Line = token.Line;
        Column = token.Column;
        Message = message;
    }

    public CompilationErrorType ErrorType { get; set; }

    public string Message { get; set; }

    public int Line { get; set; }

    public int Column { get; set; }
}




