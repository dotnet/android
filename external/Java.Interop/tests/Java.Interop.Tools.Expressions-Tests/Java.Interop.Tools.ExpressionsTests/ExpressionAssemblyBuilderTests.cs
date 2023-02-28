namespace Java.Interop.Tools.ExpressionsTests;

using System.IO;
using System.Linq.Expressions;
using System.Text;

using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.Expressions;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Mono.Linq.Expressions;

[TestFixture]
public class ExpressionAssemblyBuilderTests
{
	static  readonly    string      AssemblyModuleBaseName;

	static ExpressionAssemblyBuilderTests ()
	{
		AssemblyModuleBaseName  = typeof (ExpressionAssemblyBuilderTests).Assembly.GetName ().Name +
			"-" +
			nameof (ExpressionAssemblyBuilderTests);
	}

	ExpressionAssemblyBuilder?      ExpressionAssemblyBuilder;
	AssemblyDefinition?             AssemblyDefinition;
	TypeDefinition?                 TypeDefinition;

	[OneTimeSetUp]
	public void InitializeTestEnvironment ()
	{
		var moduleParams   = new ModuleParameters {
			Kind                            = ModuleKind.Dll,
		};
		AssemblyDefinition = AssemblyDefinition.CreateAssembly (
				assemblyName:   new AssemblyNameDefinition (AssemblyModuleBaseName, new Version (0, 0, 0, 0)),
				moduleName:     AssemblyModuleBaseName + ".dll",
				parameters:     moduleParams
		);
		TypeDefinition     = new TypeDefinition (
				@namespace: "Example",
				name:       "Output",
				attributes: TypeAttributes.Public | TypeAttributes.Sealed
		);
		TypeDefinition.BaseType     = AssemblyDefinition.MainModule.ImportReference (typeof (object));
		AssemblyDefinition.MainModule.Types.Add (TypeDefinition);

		ExpressionAssemblyBuilder   = new ExpressionAssemblyBuilder (AssemblyDefinition) {
			KeepTemporaryFiles  = true,
		};
	}

	[OneTimeTearDown]
	public void TearDownTestEnvironment ()
	{
		var path = Path.GetDirectoryName (typeof (ExpressionAssemblyBuilderTests).Assembly.Location)
			?? throw new InvalidOperationException ("`typeof (ExpressionAssemblyBuilderTests).Assembly.Location` is null?!");
		ExpressionAssemblyBuilder!.Write (Path.Combine (path, AssemblyModuleBaseName + ".dll"));
	}

	void AddMethod (MethodDefinition method, [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
	{
		method.Name     = methodName;
		method.IsPublic = true;
		TypeDefinition!.Methods.Add (method);
	}

	[Test]
	public void CreateMarshalMethodDelegateType ()
	{
		var t = ExpressionAssemblyBuilder!.CreateMarshalMethodDelegateType (
				"_Jonp_Demo",
				new [] {
					new ParameterDefinition ("jnienv",  default,    AssemblyDefinition!.MainModule.TypeSystem.IntPtr),
					new ParameterDefinition ("klass",   default,    AssemblyDefinition!.MainModule.TypeSystem.IntPtr),
					new ParameterDefinition ("value",   default,    AssemblyDefinition!.MainModule.TypeSystem.Int32),
				},
				AssemblyDefinition.MainModule.TypeSystem.IntPtr
		);
		AssemblyDefinition.MainModule.Types.Add (t);
	}

	[Test]
	public void Compile_MethodCall ()
	{
		Expression<Action> e = () => Console.WriteLine ("constant");
		var m = ExpressionAssemblyBuilder!.Compile (e);

		AddMethod (m);

		var expected = new[]{
			"Instruction_0000: ldstr \"constant\"",
			"Instruction_0001: call System.Void System.Console::WriteLine(System.String)",
			"Instruction_0002: ret",
		};
		var actual = m.Body.Instructions;
		Assert.AreEqual (expected.Length, actual.Count);
		for (int i = 0; i < expected.Length; ++i) {
			Assert.AreEqual (expected [i], GetDescription (actual, i));
		}
	}

	[Test]
	public void Compile_Condition_1 ()
	{
		Expression<Func<int, int, bool>> e = (a, b) => a == b;
		var m = ExpressionAssemblyBuilder!.Compile (e);

		AddMethod (m);

		var expected = new[]{
			"Instruction_0000: ldarg.0",
			"Instruction_0001: ldarg.1",
			"Instruction_0002: ceq",
			"Instruction_0003: ret",
		};
		var actual = m.Body.Instructions;
		Assert.AreEqual (expected.Length, actual.Count);
		for (int i = 0; i < expected.Length; ++i) {
			Assert.AreEqual (expected [i], GetDescription (actual, i));
		}
	}

	[Test]
	public void Compile_Condition_2 ()
	{
		Expression<Func<int, int, int>> e = (a, b) => a == b ? 1 : 2;
		var m = ExpressionAssemblyBuilder!.Compile (e);

		AddMethod (m);

		// Alas, branch targets d
		var expected = new[]{
			"Instruction_0000: ldarg.0",
			"Instruction_0001: ldarg.1",
			"Instruction_0002: ceq",
			"Instruction_0003: brfalse Instruction_0006",
			"Instruction_0004: ldc.i4 1",
			"Instruction_0005: br Instruction_0008",
			"Instruction_0006: nop",
			"Instruction_0007: ldc.i4 2",
			"Instruction_0008: nop",
			"Instruction_0009: ret",
		};
		var actual = m.Body.Instructions;
		Assert.AreEqual (expected.Length, actual.Count);
		for (int i = 0; i < expected.Length; ++i) {
			Assert.AreEqual (expected [i], GetDescription (actual, i));
		}
	}

	[Test]
	public void Compile_TryCatchFinally ()
	{
		var exit        = Expression.Label (typeof (int), "__exit");
		var tryBlock    = Expression.Block (typeof (int),
				E<Action>(() => Console.WriteLine ("try")).Body,
				Expression.Return (target: exit, value: Expression.Constant (1), type: typeof (int))
		);
		var finallyBlock = E<Action>(() => Console.WriteLine ("finally")).Body;
		var catchLog0   = E<Action<Exception>>(e => Console.WriteLine ("filtered"));
		var catchFilt0  = Expression.Equal (
			Expression.Constant (null, typeof (Exception)),
			Expression.Property (catchLog0.Parameters [0], "InnerException"));
		var catchBlock0 = Expression.Block (typeof (int),
				catchLog0.Body,
				Expression.Return (target: exit, value: Expression.Constant (3), type: typeof (int))
		);
		var catchLog1   = E<Action<Exception>>(e => Console.WriteLine (e.ToString ()));
		var catchBlock1 = Expression.Block (typeof (int),
				catchLog1.Body,
				Expression.Return (target: exit, value: Expression.Constant (4), type: typeof (int))
		);
		var block = new List<Expression> {
			Expression.TryCatchFinally (
				body:       tryBlock,
				@finally:   finallyBlock,
				handlers:   new[]{
					Expression.Catch (catchLog0.Parameters[0], catchBlock0, catchFilt0),
					Expression.Catch (catchLog1.Parameters[0], catchBlock1),
				}
			),
			Expression.Label (exit, Expression.Default (typeof (int))),
		};
		var e = Expression.Lambda (
				delegateType:   typeof (Func<int>),
				body:           Expression.Block (variables: Array.Empty<ParameterExpression>(), expressions: block),
				name:           nameof (Compile_TryCatchFinally),
				tailCall:       false,
				parameters:     Array.Empty<ParameterExpression>()
		);

		Assert.AreEqual (1, ((Func<int>) e.Compile ())());

		var expectedCsharp = @"int Compile_TryCatchFinally()
{
	try
	{
		Console.WriteLine(""try"");
		return 1;
	}
	catch (Exception e) if (null == e.InnerException)
	{
		Console.WriteLine(""filtered"");
		return 3;
	}
	catch (Exception e)
	{
		Console.WriteLine(e.ToString());
		return 4;
	}
	finally
	{
		Console.WriteLine(""finally"");
	}
}";
		Console.WriteLine ($"# jonp: expression tree as C#:");
		Console.WriteLine (e.ToCSharpCode ());
		Assert.AreEqual (expectedCsharp, e.ToCSharpCode ());

		var m = ExpressionAssemblyBuilder!.Compile (e);

		AddMethod (m);
		DumpInstructions (m);

		// Alas, branch targets d
		var expected = new[]{
		// .try
			"Instruction_0000: ldstr \"try\"",
			"Instruction_0001: call System.Void System.Console::WriteLine(System.String)",
			"Instruction_0002: ldc.i4 1",
			"Instruction_0003: stloc.0",
			"Instruction_0004: leave Instruction_0025",
		// }
		// filter {
			"Instruction_0005: isinst System.Exception",
			"Instruction_0006: dup",
			"Instruction_0007: brtrue.s Instruction_000b",
			"Instruction_0008: pop",
			"Instruction_0009: ldc.i4.0",
			"Instruction_000a: br.s Instruction_0012",
			"Instruction_000b: stloc.1",
			"Instruction_000c: ldnull",
			"Instruction_000d: ldloc.1",
			"Instruction_000e: callvirt System.Exception System.Exception::get_InnerException()",
			"Instruction_000f: ceq",
			"Instruction_0010: ldc.i4.0",
			"Instruction_0011: cgt.un",
			"Instruction_0012: endfilter",
		// }
		// { // handler
			"Instruction_0013: castclass System.Exception",
			"Instruction_0014: stloc.1",
			"Instruction_0015: ldstr \"filtered\"",
			"Instruction_0016: call System.Void System.Console::WriteLine(System.String)",
			"Instruction_0017: ldc.i4 3",
			"Instruction_0018: stloc.0",
			"Instruction_0019: leave Instruction_0025",
		// }
		// catch class System.Exception {
			"Instruction_001a: castclass System.Exception",
			"Instruction_001b: stloc.2",
			"Instruction_001c: ldloc.2",
			"Instruction_001d: callvirt System.String System.Object::ToString()",
			"Instruction_001e: call System.Void System.Console::WriteLine(System.String)",
			"Instruction_001f: ldc.i4 4",
			"Instruction_0020: stloc.0",
			"Instruction_0021: leave Instruction_0025",
		// }
		// finally {
			"Instruction_0022: ldstr \"finally\"",
			"Instruction_0023: call System.Void System.Console::WriteLine(System.String)",
			"Instruction_0024: endfinally",
		// }
			"Instruction_0025: nop",
			"Instruction_0026: nop",
			"Instruction_0027: ldloc.0",
			"Instruction_0028: ret",
		};
		var actual = m.Body.Instructions;
		Assert.AreEqual (expected.Length, actual.Count);
		for (int i = 0; i < expected.Length; ++i) {
			Assert.AreEqual (expected [i], GetDescription (actual, i));
		}
	}

	static Expression<TDelegate> E<TDelegate>(Expression<TDelegate> e)
		where TDelegate : Delegate
	{
		return e;
	}


	static void DumpInstructions (MethodDefinition method)
	{
		var body = method.Body;
		var instructions = body.Instructions;
		if (body.HasExceptionHandlers) {
			foreach (var h in method.Body.ExceptionHandlers) {
				Console.Error.WriteLine ($"// Handler: {h.HandlerType}");
				Console.Error.WriteLine( $"// \t" +
					$"  CatchType=`{h.CatchType}`");
				Console.Error.WriteLine ($"// \t" +
					$"  TryStart=`{GetDescription (body.Instructions, body.Instructions.IndexOf (h.TryStart))}` TryEnd=`{GetDescription (body.Instructions, body.Instructions.IndexOf (h.TryEnd))}`");
				Console.Error.WriteLine ($"// \t" +
					$"  FilterStart=`{GetDescription (body.Instructions, body.Instructions.IndexOf (h.FilterStart))}`");
				Console.Error.WriteLine ($"// \t" +
					$"  HandlerStart=`{GetDescription (body.Instructions, body.Instructions.IndexOf (h.HandlerStart))}` HandlerEnd=`{GetDescription (body.Instructions, body.Instructions.IndexOf (h.HandlerEnd))}`");
				Console.Error.WriteLine($"");
			}
		}
		int indent = 0;
		for (int i = 0; i < instructions.Count; ++i) {
			var instruction = instructions [i];
			DumpStartHandler (ref indent, body, instructions, i);
			Console.Error.WriteLine ("{0}{1,-40}\t; {2}",
					new string (' ', indent*2),
					GetDescription (instructions, i),
					instructions[i].ToString ());
			DumpEndHandler (ref indent, body, instructions, i);
		}
	}

	static void DumpStartHandler (ref int indent, MethodBody body, Mono.Collections.Generic.Collection<Instruction> instructions, int i)
	{
		var instruction = instructions [i];
		if (!body.HasExceptionHandlers) {
			return;
		}
		if (body.ExceptionHandlers.Any (e => e.TryStart == instruction)) {
			Console.Error.WriteLine ($"{new string (' ', indent*2)}.try {{");
			indent++;
			return;
		}
		var f = body.ExceptionHandlers.FirstOrDefault (e => e.FilterStart == instruction);
		if (f != null) {
			Console.Error.WriteLine ($"{new string(' ', indent*2)}filter {{");
			indent++;
			return;

		}
		var h = body.ExceptionHandlers.FirstOrDefault (e => e.HandlerStart == instruction);
		if (h != null) {
			switch (h.HandlerType) {
			case ExceptionHandlerType.Finally:
				Console.Error.WriteLine ($"{new string (' ', indent*2)}finally {{");
				break;
			case ExceptionHandlerType.Catch:
				Console.Error.WriteLine ($"{new string (' ', indent*2)}catch class {h.CatchType.FullName} {{");
				break;
			case ExceptionHandlerType.Filter:
				Console.Error.WriteLine ($"{new string(' ', indent * 2)}{{ // handler");
				break;
			case ExceptionHandlerType.Fault:
			default:
				Console.Error.WriteLine ($"{new string (' ', indent*2)}{h.HandlerType} {{");
				break;
			}
			indent++;
			return;
		}
	}

	static void DumpEndHandler (ref int indent, MethodBody body, Mono.Collections.Generic.Collection<Instruction> instructions, int i)
	{
		if (!body.HasExceptionHandlers) {
			return;
		}
		if ((i + 1) >= instructions.Count) {
			// End of instruction stream; clean up indentatino
			if (indent == 0)
				return;
			indent--;
			Console.Error.WriteLine ($"{new string (' ', indent)}}}");
			return;
		}
		// Handler range is from first label ***prior to*** second (emphasis @jonpryor)
		// Thus, look at *next* instruction.
		var instruction = instructions[i+1];
		if (body.ExceptionHandlers.Any (e => e.TryStart == instruction || e.FilterStart == instruction || e.HandlerStart == instruction ||
				e.TryEnd == instruction || e.HandlerEnd== instruction)) {
			indent--;
			Console.Error.WriteLine ($"{new string (' ', indent)}}}");
		}
	}

	// Cribbed with changes from `Instruction.ToString()`:
	// https://github.com/dotnet/cecil/blob/e069cd8d25d5b61b0e28fe65e75959c20af7aa80/Mono.Cecil.Cil/Instruction.cs#L95-L134
	//
	// Don't want to use `Instruction.ToString()` as `Instruction.Offset` isn't updated until after
	// `AssemblyDefinition.Write()`, and checking for `brfalse IL_0000` is not helpful.
	static string GetDescription (IList<Instruction> instructions, int index)
	{
		if (index < 0) {
			return "";
		}
		var instruction = instructions [index];
		var description = new StringBuilder ();

		AppendLabel (index)
			.Append (": ")
			.Append (instruction.OpCode.Name);

		if (instruction.Operand == null) {
			return description.ToString ();
		}

		description.Append (" ");

		switch (instruction.OpCode.OperandType) {
		case OperandType.ShortInlineBrTarget:
		case OperandType.InlineBrTarget:
			AppendLabel (instructions.IndexOf ((Instruction) instruction.Operand));
			break;
		case OperandType.InlineSwitch:
			var labels = (Instruction []) instruction.Operand;
			for (int i = 0; i < labels.Length; i++) {
				if (i > 0)
					description.Append (',');

				AppendLabel (instructions.IndexOf (labels [i]));
			}
			break;
		case OperandType.InlineString:
			description.Append ('\"');
			description.Append (instruction.Operand);
			description.Append ('\"');
			break;
		default:
			description.Append (instruction.Operand);
			break;
		}

		return description.ToString ();

		StringBuilder AppendLabel (int i)
		{
			return description.Append ("Instruction_")
				.AppendFormat ("{0:x4}", i);
		}
	}
}
