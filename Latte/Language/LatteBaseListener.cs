//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.11.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from Latte.g4 by ANTLR 4.11.1

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419


using Antlr4.Runtime.Misc;
using IErrorNode = Antlr4.Runtime.Tree.IErrorNode;
using ITerminalNode = Antlr4.Runtime.Tree.ITerminalNode;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;

/// <summary>
/// This class provides an empty implementation of <see cref="ILatteListener"/>,
/// which can be extended to create a listener which only needs to handle a subset
/// of the available methods.
/// </summary>
[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.11.1")]
[System.Diagnostics.DebuggerNonUserCode]
[System.CLSCompliant(false)]
public partial class LatteBaseListener : ILatteListener {
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.program"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterProgram([NotNull] LatteParser.ProgramContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.program"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitProgram([NotNull] LatteParser.ProgramContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>TopDefFunction</c>
	/// labeled alternative in <see cref="LatteParser.topDef"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterTopDefFunction([NotNull] LatteParser.TopDefFunctionContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>TopDefFunction</c>
	/// labeled alternative in <see cref="LatteParser.topDef"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitTopDefFunction([NotNull] LatteParser.TopDefFunctionContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>TopDefClass</c>
	/// labeled alternative in <see cref="LatteParser.topDef"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterTopDefClass([NotNull] LatteParser.TopDefClassContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>TopDefClass</c>
	/// labeled alternative in <see cref="LatteParser.topDef"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitTopDefClass([NotNull] LatteParser.TopDefClassContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.arg"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterArg([NotNull] LatteParser.ArgContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.arg"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitArg([NotNull] LatteParser.ArgContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.block"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterBlock([NotNull] LatteParser.BlockContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.block"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitBlock([NotNull] LatteParser.BlockContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Empty</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEmpty([NotNull] LatteParser.EmptyContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Empty</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEmpty([NotNull] LatteParser.EmptyContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>BlockStmt</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterBlockStmt([NotNull] LatteParser.BlockStmtContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>BlockStmt</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitBlockStmt([NotNull] LatteParser.BlockStmtContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Decl</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterDecl([NotNull] LatteParser.DeclContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Decl</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitDecl([NotNull] LatteParser.DeclContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Ass</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAss([NotNull] LatteParser.AssContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Ass</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAss([NotNull] LatteParser.AssContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Incr</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterIncr([NotNull] LatteParser.IncrContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Incr</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitIncr([NotNull] LatteParser.IncrContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Decr</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterDecr([NotNull] LatteParser.DecrContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Decr</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitDecr([NotNull] LatteParser.DecrContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Ret</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterRet([NotNull] LatteParser.RetContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Ret</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitRet([NotNull] LatteParser.RetContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>VRet</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterVRet([NotNull] LatteParser.VRetContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>VRet</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitVRet([NotNull] LatteParser.VRetContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Cond</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterCond([NotNull] LatteParser.CondContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Cond</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitCond([NotNull] LatteParser.CondContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>CondElse</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterCondElse([NotNull] LatteParser.CondElseContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>CondElse</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitCondElse([NotNull] LatteParser.CondElseContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>While</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterWhile([NotNull] LatteParser.WhileContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>While</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitWhile([NotNull] LatteParser.WhileContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>SExp</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterSExp([NotNull] LatteParser.SExpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>SExp</c>
	/// labeled alternative in <see cref="LatteParser.stmt"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitSExp([NotNull] LatteParser.SExpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>IdLhs</c>
	/// labeled alternative in <see cref="LatteParser.lhs"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterIdLhs([NotNull] LatteParser.IdLhsContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>IdLhs</c>
	/// labeled alternative in <see cref="LatteParser.lhs"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitIdLhs([NotNull] LatteParser.IdLhsContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>FieldAccessLHS</c>
	/// labeled alternative in <see cref="LatteParser.lhs"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFieldAccessLHS([NotNull] LatteParser.FieldAccessLHSContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>FieldAccessLHS</c>
	/// labeled alternative in <see cref="LatteParser.lhs"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFieldAccessLHS([NotNull] LatteParser.FieldAccessLHSContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.classDecl"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterClassDecl([NotNull] LatteParser.ClassDeclContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.classDecl"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitClassDecl([NotNull] LatteParser.ClassDeclContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Int</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterInt([NotNull] LatteParser.IntContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Int</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitInt([NotNull] LatteParser.IntContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Str</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterStr([NotNull] LatteParser.StrContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Str</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitStr([NotNull] LatteParser.StrContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Bool</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterBool([NotNull] LatteParser.BoolContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Bool</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitBool([NotNull] LatteParser.BoolContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>Void</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterVoid([NotNull] LatteParser.VoidContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>Void</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitVoid([NotNull] LatteParser.VoidContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>ClassInstance</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterClassInstance([NotNull] LatteParser.ClassInstanceContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>ClassInstance</c>
	/// labeled alternative in <see cref="LatteParser.type_"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitClassInstance([NotNull] LatteParser.ClassInstanceContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>SimpleDecl</c>
	/// labeled alternative in <see cref="LatteParser.item"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterSimpleDecl([NotNull] LatteParser.SimpleDeclContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>SimpleDecl</c>
	/// labeled alternative in <see cref="LatteParser.item"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitSimpleDecl([NotNull] LatteParser.SimpleDeclContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>AssDecl</c>
	/// labeled alternative in <see cref="LatteParser.item"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAssDecl([NotNull] LatteParser.AssDeclContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>AssDecl</c>
	/// labeled alternative in <see cref="LatteParser.item"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAssDecl([NotNull] LatteParser.AssDeclContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EId</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEId([NotNull] LatteParser.EIdContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EId</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEId([NotNull] LatteParser.EIdContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EFunCall</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEFunCall([NotNull] LatteParser.EFunCallContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EFunCall</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEFunCall([NotNull] LatteParser.EFunCallContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>ERelOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterERelOp([NotNull] LatteParser.ERelOpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>ERelOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitERelOp([NotNull] LatteParser.ERelOpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>ETrue</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterETrue([NotNull] LatteParser.ETrueContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>ETrue</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitETrue([NotNull] LatteParser.ETrueContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EOr</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEOr([NotNull] LatteParser.EOrContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EOr</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEOr([NotNull] LatteParser.EOrContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EInt</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEInt([NotNull] LatteParser.EIntContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EInt</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEInt([NotNull] LatteParser.EIntContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EUnOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEUnOp([NotNull] LatteParser.EUnOpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EUnOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEUnOp([NotNull] LatteParser.EUnOpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EStr</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEStr([NotNull] LatteParser.EStrContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EStr</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEStr([NotNull] LatteParser.EStrContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EFieldAccessRHS</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEFieldAccessRHS([NotNull] LatteParser.EFieldAccessRHSContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EFieldAccessRHS</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEFieldAccessRHS([NotNull] LatteParser.EFieldAccessRHSContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EMulOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEMulOp([NotNull] LatteParser.EMulOpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EMulOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEMulOp([NotNull] LatteParser.EMulOpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EAnd</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEAnd([NotNull] LatteParser.EAndContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EAnd</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEAnd([NotNull] LatteParser.EAndContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EParen</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEParen([NotNull] LatteParser.EParenContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EParen</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEParen([NotNull] LatteParser.EParenContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EFalse</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEFalse([NotNull] LatteParser.EFalseContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EFalse</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEFalse([NotNull] LatteParser.EFalseContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>ENew</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterENew([NotNull] LatteParser.ENewContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>ENew</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitENew([NotNull] LatteParser.ENewContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>EAddOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterEAddOp([NotNull] LatteParser.EAddOpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>EAddOp</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitEAddOp([NotNull] LatteParser.EAddOpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>ENull</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterENull([NotNull] LatteParser.ENullContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>ENull</c>
	/// labeled alternative in <see cref="LatteParser.expr"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitENull([NotNull] LatteParser.ENullContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.fieldAccess"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFieldAccess([NotNull] LatteParser.FieldAccessContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.fieldAccess"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFieldAccess([NotNull] LatteParser.FieldAccessContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.addOp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAddOp([NotNull] LatteParser.AddOpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.addOp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAddOp([NotNull] LatteParser.AddOpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.mulOp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterMulOp([NotNull] LatteParser.MulOpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.mulOp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitMulOp([NotNull] LatteParser.MulOpContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="LatteParser.relOp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterRelOp([NotNull] LatteParser.RelOpContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="LatteParser.relOp"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitRelOp([NotNull] LatteParser.RelOpContext context) { }

	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void EnterEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void ExitEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitTerminal([NotNull] ITerminalNode node) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitErrorNode([NotNull] IErrorNode node) { }
}
