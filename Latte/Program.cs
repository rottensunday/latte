using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Latte;
using Latte.Compiler;
using Latte.Listeners;

Console.WriteLine("go...");

// var input = new AntlrFileStream("Programs/program.lat");
// var lexer = new LatteLexer(input);
// var tokens = new CommonTokenStream(lexer);
// var parser = new LatteParser(tokens);
// var tree = parser.program();
// var walker = new ParseTreeWalker();
// var symbolTablePass = new SymbolTablePass();
// walker.Walk(symbolTablePass, tree);
//
// var typesPass = new TypesPass(symbolTablePass.Globals, symbolTablePass.Scopes);
// walker.Walk(typesPass, tree);
//
// var testVisitor = new LatteVisitor(symbolTablePass.Globals, symbolTablePass.Scopes, typesPass.Types);
// var result = testVisitor.VisitProgram(tree);
// result.WriteErrors();

var result = LatteCompiler.Compile("Programs/program.lat");
result.CompilationResult?.WriteErrors();

var x = 1;
