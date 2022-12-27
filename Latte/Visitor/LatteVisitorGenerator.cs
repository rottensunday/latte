// namespace Latte;
//
// using Antlr4.Runtime.Tree;
// using Models.ConstExpression;
// using Scopes;
//
// public class LatteVisitorGenerator : LatteBaseVisitor<int>
// {
//     private readonly GlobalScope _globals;
//     private readonly ParseTreeProperty<IScope> _scopes;
//     private readonly ParseTreeProperty<LatteType> _types;
//     private readonly ParseTreeProperty<IConstExpression> _constantExpressions;
//     private IScope _currentScope;
//     public List<string> Instructions = new();
//
//     public LatteVisitorGenerator(
//         GlobalScope globals,
//         ParseTreeProperty<IScope> scopes,
//         ParseTreeProperty<LatteType> types,
//         ParseTreeProperty<IConstExpression> constantExpressions)
//     {
//         _scopes = scopes;
//         _globals = globals;
//         _types = types;
//         _constantExpressions = constantExpressions;
//     }
//     
//     public override int VisitProgram(LatteParser.ProgramContext context)
//     {
//         Instructions.Add(GasSymbols.Prefix);
//         
//         _currentScope = _globals;
//         
//         VisitChildren(context);
//
//         return 0;
//     }
//
//     public override int VisitTopDef(LatteParser.TopDefContext context)
//     {
//         _currentScope = _scopes.Get(context);
//         
//         var parametersNodes = context.arg()?.ID();
//         var metParameters = new HashSet<string>();
//         var functionSymbol = _currentScope.Resolve(context.ID().GetText());
//         
//         Instructions.Add(GasSymbols.GenerateFunctionSymbol(functionSymbol.Name));
//
//         VisitChildren(context);
//         
//         _currentScope = _currentScope.GetEnclosingScope();
//
//         return 0;
//     }
//
//     public override int VisitBlock(LatteParser.BlockContext context)
//     {
//         _currentScope = _scopes.Get(context);
//
//         VisitChildren(context);
//         
//         _currentScope = _currentScope.GetEnclosingScope();
//
//         return 0;
//     }
//     
//     public override int VisitEFunCall(LatteParser.EFunCallContext context)
//     {
//         // var args = context.expr();
//         // var name = context.ID().GetText();
//         // var symbol = _currentScope.Resolve(name) as FunctionSymbol;
//         // var symbolArgs = symbol.ArgumentsList;
//         //
//         // var firstArg = _constantExpressions.Get(args[0]);
//         //
//         // if (firstArg is ConstExpression<int> x)
//         // {
//         //     AddInstruction(GasSymbols.GenerateMov(x.Value, Register.RDI));
//         // }
//         //
//         // AddInstruction(GasSymbols.GenerateFunctionCall(name));
//         //
//         // VisitChildren(context);
//
//         return 0;
//     }
//
//     public override int VisitVRet(LatteParser.VRetContext context)
//     {
//         AddInstruction(GasSymbols.GenerateRet());
//
//         VisitChildren(context);
//
//         return 0;
//     }
//
//     public override int VisitRet(LatteParser.RetContext context)
//     {
//         var expr = context.expr();
//         var value = _constantExpressions.Get(expr);
//         
//         if (value is ConstExpression<int> x)
//         {
//             AddInstruction(GasSymbols.GenerateMov(x.Value, Register.RAX));
//         }
//         
//         AddInstruction(GasSymbols.GenerateRet());
//
//         VisitChildren(context);
//
//         return 0;
//     }
//
//     private void AddInstruction(string instruction)
//         => Instructions.Add(instruction);
// }
