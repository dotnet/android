using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Java.Interop.Tools.Expressions;

class CecilCompilerExpressionVisitor : ExpressionVisitor
{
	public CecilCompilerExpressionVisitor (AssemblyDefinition declaringAssembly, MethodBody body, VariableDefinitions variables, Action<TraceLevel, string> logger)
	{
		this.assemblyDef    = declaringAssembly;
		this.body           = body;
		this.variables      = variables;
		il                  = body.GetILProcessor ();
		Logger              = logger;
	}

	AssemblyDefinition                          assemblyDef;
	MethodBody                                  body;
	ILProcessor                                 il;
	VariableDefinitions                         variables;
	Dictionary<LabelTarget, List<Instruction>>  returnFixups    = new ();
	Action<TraceLevel, string>                  Logger;

	/// <summary>
	/// Dispatches the expression to one of the more specialized visit methods in this class.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	[return: NotNullIfNotNull("node")]
	public override Expression? Visit (
			Expression? node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.Visit [{node?.NodeType.ToString () ?? "<null>"}]: {node}");
		return base.Visit (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="BinaryExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitBinary (
			BinaryExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitBinary: {node} [{node.NodeType}]");
		switch (node.NodeType) {
		case ExpressionType.Assign:
			var target  = node.Left as ParameterExpression;
			if (target == null) {
				Logger (TraceLevel.Verbose, $"# jonp: don't know where to assign `{node.Left}`!");
				return base.VisitBinary (node);
			}
			Logger (TraceLevel.Verbose, $"# jonp: target={target}; target.Type={target.Type}; requires-&? {InstanceInvokeRequiresAddress (target.Type)}");
			if (InstanceInvokeRequiresAddress (target.Type) && node.Right is NewExpression n) {
				variables [target].LoadAddress (il);
				Visit (node.Right);
			} else {
				Visit (node.Right);
				variables [target].Store (il);
			}
			break;
		case ExpressionType.Equal:
			Visit (node.Left);
			Visit (node.Right);
			il.Emit (OpCodes.Ceq);
			break;
		default:
			Logger (TraceLevel.Verbose, $"# jonp: don't know how to emit binary expr {node.NodeType}!");
			base.VisitBinary (node);
			break;
		}
		return node;
	}

	static bool InstanceInvokeRequiresAddress (Type type) => type.IsValueType && !type.IsPrimitive;

	/// <summary>
	/// Visits the children of the <see cref="BlockExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitBlock (
			BlockExpression node)
	{
		// Base method also visits parameter nodes after body; we don't want that.
		// https://cs.github.com/dotnet/runtime/blob/9df6ea21007319967975dc9985413bb6518287da/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/ExpressionVisitor.cs#L214
		// return base.VisitBlock (node);
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitBlock: {node}");
		foreach (var e in node.Expressions) {
			Visit (e);
		}
		return node;
	}

	/// <summary>
	/// Visits the children of the <see cref="ConditionalExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitConditional (
			ConditionalExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitConditional: {node}");
		Visit (node.Test);
		var startFalse = il.Create (OpCodes.Nop);
		var endBranch  = il.Create (OpCodes.Nop);
		il.Emit (OpCodes.Brfalse, startFalse);
		Visit (node.IfTrue);
		il.Emit (OpCodes.Br, endBranch);
		il.Append (startFalse);
		Visit (node.IfFalse);
		il.Append (endBranch);
		return node;
		// return base.VisitConditional (node);
	}

	/// <summary>
	/// Visits the <see cref="ConstantExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitConstant (
			ConstantExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitConstant: {node}");
		switch (Type.GetTypeCode (node.Type)) {
			case TypeCode.String:
				il.Emit (OpCodes.Ldstr, (string?) node.Value);
				break;
			case TypeCode.Boolean:
				if ((bool) node.Value!) {
					il.Emit (OpCodes.Ldc_I4_1);
				} else {
					il.Emit (OpCodes.Ldc_I4_0);
				}
				break;
			case TypeCode.Char:
				il.Emit (OpCodes.Ldc_I4, (char) node.Value!);
				break;
			case TypeCode.SByte:
				il.Emit (OpCodes.Ldc_I4_S, (sbyte) node.Value!);
				break;
			case TypeCode.Byte:
				il.Emit (OpCodes.Ldc_I4, (byte) node.Value!);
				break;
			case TypeCode.Int16:
				il.Emit (OpCodes.Ldc_I4, (short) node.Value!);
				break;
			case TypeCode.Int32:
				il.Emit (OpCodes.Ldc_I4, (int) node.Value!);
				break;
			case TypeCode.Int64:
				il.Emit (OpCodes.Ldc_I8, (long) node.Value!);
				break;
			case TypeCode.Single:
				il.Emit (OpCodes.Ldc_R4, (float) node.Value!);
				break;
			case TypeCode.Double:
				il.Emit (OpCodes.Ldc_R8, (double) node.Value!);
				break;
			case TypeCode.UInt16:
				il.Emit (OpCodes.Ldc_I4, (short) node.Value!);
				break;
			case TypeCode.UInt32:
				il.Emit (OpCodes.Ldc_I4, (int) node.Value!);
				break;
			case TypeCode.UInt64:
				il.Emit (OpCodes.Ldc_I8, (int) node.Value!);
				break;
			case TypeCode.Object:
				if (node.Type == typeof (Type)) {
					Logger (TraceLevel.Verbose, $"# jonp: TODO load type {node.Value}");
					break;
				} else if (node.Value == null) {
					Logger (TraceLevel.Verbose, $"# jonp: TODO ldnull {node.Value}");
					il.Emit (OpCodes.Ldnull);
					break;
				}
				goto default;
			default:
				Logger (TraceLevel.Verbose, $"# jonp: don't know how to deal with constant with value `{node}` NodeType `{node.NodeType}` Type `{node.Type}` typecode {Type.GetTypeCode (node.Type)}");
				break;
				// throw new NotSupportedException ();
		}
		return node;
	}

	/// <summary>
	/// Visits the <see cref="DebugInfoExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override  Expression VisitDebugInfo (
			DebugInfoExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitDebugInfo: {node}");
		return base.VisitDebugInfo (node);
	}

	/// <summary>
	/// Visits the <see cref="DefaultExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitDefault (
			DefaultExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitDefault: {node}");
		return base.VisitDefault (node);
	}

	/// <summary>
	/// Visits the children of the extension expression.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	/// <remarks>
	/// This can be overridden to visit or rewrite specific extension nodes.
	/// If it is not overridden, this method will call <see cref="Expression.VisitChildren"/>,
	/// which gives the node a chance to walk its children. By default,
	/// <see cref="Expression.VisitChildren"/> will try to reduce the node.
	/// </remarks>
	protected override Expression VisitExtension (
			Expression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitExtension: {node}");
		return base.VisitExtension (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="GotoExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitGoto (
			GotoExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitGoto: {node}");
		if (node.Kind != GotoExpressionKind.Return || node.Type == typeof (void)) {
			return base.VisitGoto (node);
		}
		Visit (node.Value);
		variables.ReturnValue?.Store (il);
		il.Emit (OpCodes.Ret);
		List<Instruction> fixups    = GetFixupsForLabelTarget (node.Target);
		fixups.Add (il.Body.Instructions.Last ());
		Logger (TraceLevel.Verbose, $"# jonp: adding fixup for goto `{node}` at index {il.Body.Instructions.Count-1}");
		return node;
	}

	/// <summary>
	/// Visits the children of the <see cref="InvocationExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitInvocation (
			InvocationExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitInvocation: {node}");
		return base.VisitInvocation (node);
	}

	/// <summary>
	/// Visits the <see cref="LabelTarget"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	[return: NotNullIfNotNull("node")]
	protected override LabelTarget? VisitLabelTarget (
			LabelTarget? node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitLabelTarget: {node}");
		if (node != null) {
			il.Emit (OpCodes.Nop);
			GetFixupsForLabelTarget (node).Add (il.Body.Instructions.Last ());
		}
		return base.VisitLabelTarget (node);
	}

	List<Instruction> GetFixupsForLabelTarget (LabelTarget target)
	{
		if (!returnFixups.TryGetValue (target, out List<Instruction>? fixups)) {
			returnFixups.Add (target, fixups = new ());
		}
		return fixups;
	}

	/// <summary>
	/// Visits the children of the <see cref="LabelExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitLabel (
			LabelExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitLabel: {node}");
		var target = il.Body.Instructions.Last ();
		if (returnFixups.TryGetValue (node.Target, out List<Instruction>? fixups)) {
			foreach (var replace in fixups) {
				Logger (TraceLevel.Verbose, $"# jonp: VisitLabel: replacing instruction `{replace}` w/ `leave {target}");
				Debug.Assert (replace.OpCode == OpCodes.Ret || replace.OpCode == OpCodes.Nop);
				replace.OpCode = OpCodes.Leave;
				replace.Operand = target;
			}
		}
		return base.VisitLabel (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="Expression{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type of the delegate.</typeparam>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitLambda<T>(Expression<T> node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitLambda: {node}");
		return Visit (node.Body);
		// Base method also visits parameter nodes after body; we don't want that.
		// return base.VisitLambda (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="LoopExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitLoop (
			LoopExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitLoop: {node}");
		return base.VisitLoop (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="MemberExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitMember (
			MemberExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitMember: {node}");
		base.VisitMember (node);
		switch (node.Member.MemberType) {
		case System.Reflection.MemberTypes.Field:
			var field = (System.Reflection.FieldInfo) node.Member;
			il.Emit (
					field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld,
					assemblyDef.MainModule.ImportReference (field));
			break;
		case System.Reflection.MemberTypes.Property:
			var property    = (System.Reflection.PropertyInfo) node.Member;
			var getter      = property.GetGetMethod ();
			il.Emit (GetCallOpCode (getter!), assemblyDef.MainModule.ImportReference (getter));
			break;
		default:
			throw new NotSupportedException ($"How do I visit `{node.Member.MemberType}`? {node}");
		}
		return node;
	}

	/// <summary>
	/// Visits the children of the <see cref="IndexExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitIndex (
			IndexExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitIndex: {node}");
		return base.VisitIndex (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="MethodCallExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitMethodCall (
			MethodCallExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitMethodCall: {node}; node.Object={node.Object}");
		// We need to special-case `node.Object` handling
		// https://github.com/dotnet/runtime/blob/edd23fcb1b350cb1a53fa409200da55e9c33e99e/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/ExpressionVisitor.cs#L403-L413

		if (node.Object is ParameterExpression target) {
			if (InstanceInvokeRequiresAddress (target.Type)) {
				variables [target].LoadAddress (il);
			} else {
				variables [target].Load (il);
			}
		} else {
			Visit (node.Object);
		}
		foreach (var a in node.Arguments) {
			Visit (a);
		}
		il.Emit (GetCallOpCode (node.Method), assemblyDef.MainModule.ImportReference (node.Method));

		return node;
	}

	OpCode GetCallOpCode (global::System.Reflection.MethodBase method)
	{
		if (method.IsStatic || (method.DeclaringType?.IsValueType ?? false))
			return OpCodes.Call;
		return OpCodes.Callvirt;
	}

	void EmitConsoleWriteLine (ILProcessor il, string message)
	{
		Action<string> cwl = Console.WriteLine;
		il.Emit (OpCodes.Ldstr, message);
		il.Emit (OpCodes.Call, assemblyDef.MainModule.ImportReference (cwl.Method));
	}

	/// <summary>
	/// Visits the children of the <see cref="NewArrayExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitNewArray (
			NewArrayExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitNewArray: {node}");
		return base.VisitNewArray (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="NewExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitNew (
			NewExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitNew: {node}");
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitNew: ctor={node.Constructor} {node.Constructor != null}");
		base.VisitNew (node);
		if (node.Constructor == null && node.Type.IsValueType) {
			il.Emit (OpCodes.Initobj,   assemblyDef.MainModule.ImportReference (node.Type));
		} else {
			il.Emit (OpCodes.Call,      assemblyDef.MainModule.ImportReference (node.Constructor));
		}
		return node;
	}

	/// <summary>
	/// Visits the <see cref="ParameterExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitParameter (
			ParameterExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitParameter: {(node.Type.IsByRef ? "&" : "")}{node}");

		if (node.Type.IsByRef) {
			variables [node].LoadAddress (il);
		} else {
			variables [node].Load (il);
		}
		
		return node;
	}

	/// <summary>
	/// Visits the children of the <see cref="RuntimeVariablesExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitRuntimeVariables (
			RuntimeVariablesExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitRuntimeVariables: {node}");
		return base.VisitRuntimeVariables (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="SwitchCase"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override SwitchCase VisitSwitchCase (
			SwitchCase node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitSwitchCase: {node}");
		return base.VisitSwitchCase (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="SwitchExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitSwitch (
			SwitchExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitSwitch: {node}");
		return base.VisitSwitch (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="CatchBlock"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override CatchBlock VisitCatchBlock (
			CatchBlock node)
	{
		// On entry, IL stream should assume that there is an Exception type on the evaluation stack.

		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitCatchBlock: {node}");

		var startCatchBlock     = il.Body.Instructions.Count;
		var handlerDef          = new ExceptionHandler (ExceptionHandlerType.Catch) {
			TryStart        = TryStart,
		};
		body.ExceptionHandlers.Add (handlerDef);

		if (node.Filter != null) {
			EmitCatchFilter (node);
			handlerDef.HandlerType  = ExceptionHandlerType.Filter;
			handlerDef.FilterStart  = il.Body.Instructions [startCatchBlock];
			startCatchBlock         = il.Body.Instructions.Count;
		} else if (node.Test != null) {
			handlerDef.CatchType        = assemblyDef.MainModule.ImportReference (node.Test);
		}

		if (node.Variable != null) {
			il.Emit (OpCodes.Castclass, assemblyDef.MainModule.ImportReference (node.Variable.Type));
			variables [node.Variable!].Store (il);
		} else {
			il.Emit (OpCodes.Pop);
		}

		Visit (node.Body);
		EmitLeave ();

		handlerDef.HandlerStart = il.Body.Instructions [startCatchBlock];

		return node;
	}

	void EmitCatchFilter (CatchBlock node)
	{
		Instruction? fixupStartFilter   = null;
		Instruction? fixupEndFilter     = null;

		if (node.Test != null) {
			il.Emit (OpCodes.Isinst, assemblyDef.MainModule.ImportReference (node.Test));
			il.Emit (OpCodes.Dup);
			il.Emit (OpCodes.Brtrue_S,  il.Body.Instructions.Last ());
			fixupStartFilter    = il.Body.Instructions.Last ();
			il.Emit (OpCodes.Pop);
			il.Emit (OpCodes.Ldc_I4_0);
			il.Emit (OpCodes.Br_S,      il.Body.Instructions.Last ());
			fixupEndFilter      = il.Body.Instructions.Last ();
		}

		if (node.Variable != null) {
			variables [node.Variable!].Store (il);
		} else {
			il.Emit (OpCodes.Pop);
		}

		if (fixupStartFilter != null) {
			fixupStartFilter.Operand    = il.Body.Instructions.Last ();
		}

		Visit (node.Filter);

		// node.Filter is assumed to leave a "boolean" on the eval stack; convert to an int
		il.Emit (OpCodes.Ldc_I4_0);
		il.Emit (OpCodes.Cgt_Un);

		il.Emit (OpCodes.Endfilter);

		if (fixupEndFilter != null) {
			fixupEndFilter.Operand      = il.Body.Instructions.Last ();
		}
	}

	Instruction?        TryStart;
	List<Instruction>?  FixupLeaveOffsets;


	/// <summary>
	/// Visits the children of the <see cref="TryExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitTry (
			TryExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitTry: {node}");

		var prevTryStart        = TryStart;
		var pFixupLeaveOffsets  = FixupLeaveOffsets;
		try {
			var startTryBlock   = il.Body.Instructions.Count;
			FixupLeaveOffsets   = new ();

			Visit (node.Body);
			EmitLeave ();
			TryStart            = il.Body.Instructions [startTryBlock];

			Visit (node.Handlers, VisitCatchBlock);

			if (node.Finally != null) {
				var startFinallyBlock   = il.Body.Instructions.Count;
				Visit (node.Finally);
				il.Emit (OpCodes.Endfinally);

				var finallyDef = new ExceptionHandler (ExceptionHandlerType.Finally) {
					TryStart        = TryStart,
					HandlerStart    = il.Body.Instructions [startFinallyBlock],
				};
				body.ExceptionHandlers.Add (finallyDef);
			}

			// Visit (node.Fault);

			// ECMA 335 Partition X § 19 Exception Handling
			//  HandlerBlock ::= `handler` Label to Label
			// Handler range is from first label ***prior to*** second (emphasis @jonpryor)
			// Therefore we need to append `NOP` to the IL stream so that the fixupTarget is
			// one-past-the-end, as nothing afterward has yet been emitted.

			il.Emit (OpCodes.Nop);
			var fixupTarget = il.Body.Instructions.Last ();

			for (int i = 0; i < (body.ExceptionHandlers.Count-1); ++i) {
				var c = body.ExceptionHandlers [i];
				var n = body.ExceptionHandlers [i+1];
				c.TryEnd        = c.FilterStart ?? c.HandlerStart;
				c.HandlerEnd    = n.FilterStart ?? n.HandlerStart;
			}
			if (body.ExceptionHandlers.Count > 0) {
				var f           = body.ExceptionHandlers [body.ExceptionHandlers.Count-1];
				f.TryEnd        = f.HandlerStart;
				f.HandlerEnd    = fixupTarget;
			}
			foreach (var fixup in FixupLeaveOffsets) {
				fixup.Operand   = fixupTarget;
			}
		}
		finally {
			TryStart            = prevTryStart;
			FixupLeaveOffsets   = pFixupLeaveOffsets;
		}

		return node;
	}

	void EmitLeave ()
	{
		// keep in sync w/ VisitGoto()
		// Prevent multiple `leave OFFSET`s in the output
		if (il.Body.Instructions.Last ().OpCode.Code != Code.Ret) {
			il.Emit (OpCodes.Leave, il.Body.Instructions.Last ());
			FixupLeaveOffsets!.Add (il.Body.Instructions.Last ());
		}
	}

	/// <summary>
	/// Visits the children of the <see cref="TypeBinaryExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitTypeBinary (
			TypeBinaryExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitTypeBinary: {node}");
		return base.VisitTypeBinary (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="UnaryExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitUnary (
			UnaryExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitUnary: {node}");
		return base.VisitUnary (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="MemberInitExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitMemberInit (
			MemberInitExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitMemberInit: {node}");
		return base.VisitMemberInit (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="ListInitExpression"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitListInit (
			ListInitExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitListInit: {node}");
		return base.VisitListInit (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="ElementInit"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override ElementInit VisitElementInit (
			ElementInit node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitElementInit: {node}");
		return base.VisitElementInit (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="MemberBinding"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override MemberBinding VisitMemberBinding (
			MemberBinding node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitMemberBinding: {node}");
		return base.VisitMemberBinding (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="MemberAssignment"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override MemberAssignment VisitMemberAssignment (
			MemberAssignment node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitMemberAssignment: {node}");
		return base.VisitMemberAssignment (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="MemberMemberBinding"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override MemberMemberBinding VisitMemberMemberBinding (
			MemberMemberBinding node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitMemberMemberBinding: {node}");
		return base.VisitMemberMemberBinding (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="MemberListBinding"/>.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override MemberListBinding VisitMemberListBinding (
			MemberListBinding node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitMemberListBinding: {node}");
		return base.VisitMemberListBinding (node);
	}

	/// <summary>
	/// Visits the children of the <see cref="DynamicExpression" />.
	/// </summary>
	/// <param name="node">The expression to visit.</param>
	/// <returns>The modified expression, if it or any subexpression was modified;
	/// otherwise, returns the original expression.</returns>
	protected override Expression VisitDynamic (
			DynamicExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: CecilCompilerExpressionVisitor.VisitDynamic: {node}");
		return base.VisitDynamic (node);
	}
}
