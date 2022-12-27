using Latte.Compiler;

var result = LatteCompiler.Compile("Programs/program.lat");
Console.WriteLine();
result.CompilationResult?.WriteErrors();
Console.WriteLine();
result.CompilationResult?.WriteInstructions();
Console.WriteLine();
result.CompilationResult?.WriteInstructionsToFile("/Users/rotten/Learn/Compilers/latte/Latte/Latte/Programs/call_printint.s");
Console.WriteLine();
result.CompilationResult?.WriteOutputToFile("/Users/rotten/Learn/Compilers/latte/Latte/Latte/Programs/call_printint.output");
Console.WriteLine();
Console.WriteLine("Code run result:");
result.CompilationResult?.WriteOutput();
