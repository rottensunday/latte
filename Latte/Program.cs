using Latte.Compiler;

var result = LatteCompiler.Compile("Programs/program.lat");

if (!result.CompilationResult.Success)
{
    Console.WriteLine("COMPILATION ERRORS:");
    Console.WriteLine("-----------------------------");
    result.CompilationResult?.WriteErrors();
    Console.WriteLine("-----------------------------");

    return;
}

Console.WriteLine();
Console.WriteLine("ASM CODE:");
Console.WriteLine("-----------------------------");
result.CompilationResult?.WriteInstructions();
Console.WriteLine();
Console.WriteLine("-----------------------------");
Console.WriteLine();
result.CompilationResult?.WriteInstructionsToFile(
    "/Users/rotten/Learn/Compilers/latte/Latte/Latte/Programs/call_printint.s");
Console.WriteLine();
result.CompilationResult?.WriteOutputToFile(
    "/Users/rotten/Learn/Compilers/latte/Latte/Latte/Programs/call_printint.output");
Console.WriteLine();
Console.WriteLine("CODE RUN RESULT:");
Console.WriteLine("-----------------------------");
result.CompilationResult?.WriteOutput();
Console.WriteLine("-----------------------------");
