namespace Latte.Compiler;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Latte.Models;
using Listeners;
using Models;

public static class LatteCompiler
{
    public static CompileResult Compile(string filePath)
    {
        var input = new AntlrFileStream(filePath);
        var lexer = new LatteLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new LatteParser(tokens);
        var tree = parser.program();

        if (parser.NumberOfSyntaxErrors == 0)
        {
            var walker = new ParseTreeWalker();
            var symbolTablePass = new SymbolTablePass();
            walker.Walk(symbolTablePass, tree);

            if (symbolTablePass.Errors is { Count: > 0 })
            {
                return new CompileResult(ParsingResultType.Ok, new CompilationResult(symbolTablePass.Errors));
            }

            var typesPass = new TypesPass(symbolTablePass.Globals, symbolTablePass.Scopes);
            walker.Walk(typesPass, tree);

            var constantsPass = new InlinePass(symbolTablePass.Globals, symbolTablePass.Scopes);
            walker.Walk(constantsPass, tree);

            var intermediatePass = new IntermediateBuilderPass(
                symbolTablePass.Globals, symbolTablePass.Scopes, typesPass.Types, constantsPass.ConstantExpressions);
            walker.Walk(intermediatePass, tree);

            // var intermediatePass = new IntermediateBuilderPassVisitor(
            //     symbolTablePass.Globals, symbolTablePass.Scopes, typesPass.Types, constantsPass.ConstantExpressions);
            // intermediatePass.VisitProgram(tree);
            
            Console.WriteLine("INTERMEDIATE REPRESENTATION:");
            Console.WriteLine("-----------------------------");

            foreach (var function in intermediatePass.IntermediateFunctions)
            {
                Console.WriteLine($"{function.Name}:");

                foreach (var instruction in function.Instructions)
                {
                    Console.WriteLine(instruction);
                }
                
                Console.WriteLine();
            }
            
            Console.WriteLine("-----------------------------");

            var testVisitor = new LatteVisitor(
                symbolTablePass.Globals, symbolTablePass.Scopes, typesPass.Types, constantsPass.ConstantExpressions);
            var result = testVisitor.VisitProgram(tree);
            
            result.WriteErrors();

            if (!result.Success)
            {
                return new CompileResult(ParsingResultType.Ok, new CompilationResult(result.Errors));
            }

            // var generatingVisitor = new LatteVisitorGenerator(
            //     symbolTablePass.Globals, symbolTablePass.Scopes, typesPass.Types,
            //     constantsPass.ConstantExpressions);
            //
            // generatingVisitor.VisitProgram(tree);

            var instructions = new List<string>();
            var compiler = new IntermediateToX86Compiler();
            
            instructions.Add(GasSymbols.Prefix);

            instructions.AddRange(compiler.Compile(intermediatePass.IntermediateFunctions));
            

            return new CompileResult(
                ParsingResultType.Ok, new CompilationResult(null, instructions));
        }

        Console.WriteLine("Syntax errors in code; unable to generate");
        return new CompileResult(ParsingResultType.SyntaxError);
    }
}
