namespace Latte.Compiler.Models;

using Latte.Models;

public class CompileResult
{
    public CompileResult(ParsingResultType parsingResultType, CompilationResult compilationResult = null)
    {
        ParsingResultType = parsingResultType;
        CompilationResult = compilationResult;
    }

    public ParsingResultType ParsingResultType { get; set; }

    public CompilationResult CompilationResult { get; set; }
}
