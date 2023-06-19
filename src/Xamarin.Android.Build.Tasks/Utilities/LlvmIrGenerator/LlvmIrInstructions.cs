using System;
using System.Collections.Generic;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVM.IR;

// TODO: remove these aliases once the refactoring is done
using LlvmIrIcmpCond = LLVMIR.LlvmIrIcmpCond;
using LlvmIrCallMarker = LLVMIR.LlvmIrCallMarker;

abstract class LlvmIrInstruction : LlvmIrFunctionBodyItem
{
	// TODO: add support for metadata
	public string Mnemonic                          { get; }
	public LlvmIrFunctionAttributeSet? AttributeSet { get; set; }

	/// <summary>
	/// TBAA (Type Based Alias Analysis) metadata item the instruction references, if any.
	/// <see cref="LlvmIrModule.TbaaAnyPointer"/> for more information about TBAA.
	/// </summary>
	public LlvmIrMetadataItem? TBAA                 { get; set; }

	protected LlvmIrInstruction (string mnemonic)
	{
		if (String.IsNullOrEmpty (mnemonic)) {
			throw new ArgumentException ("must not be null or empty", nameof (mnemonic));
		}

		Mnemonic = mnemonic;
	}

	public override void Write (GeneratorWriteContext context)
	{
		context.Output.Write (context.CurrentIndent);
		WriteValueAssignment (context);
		WritePreamble (context);
		context.Output.Write (Mnemonic);
		context.Output.Write (' ');
		WriteBody (context);

		if (TBAA != null) {
			context.Output.Write (", !tbaa !");
			context.Output.Write (TBAA.Name);
		}

		if (AttributeSet != null) {
			context.Output.Write (" #");
			context.Output.Write (AttributeSet.Number.ToString (CultureInfo.InvariantCulture));
		}
		context.Output.WriteLine ();
	}

	/// <summary>
	/// Write the '&lt;variable_reference&gt; = ' part of the instruction line.
	/// </summary>
	protected virtual void WriteValueAssignment (GeneratorWriteContext context)
	{}

	/// <summary>
	/// Write part of the instruction that comes between the optional value assignment and the instruction
	/// mnemonic.  If any text is written, it must end with a whitespace.
	/// </summary>
	protected virtual void WritePreamble (GeneratorWriteContext context)
	{}

	/// <summary>
	/// Write the "body" of the instruction, that is the part that follows instruction mnemonic but precedes the
	/// metadata and attribute set references.
	/// </summary>
	protected virtual void WriteBody (GeneratorWriteContext context)
	{}

	protected void WriteValue (GeneratorWriteContext context, Type type, object? value, bool isPointer)
	{
		if (value == null) {
			if (!isPointer) {
				throw new InvalidOperationException ($"Internal error: non-pointer type '{type}' must not have a `null` value");
			}
			context.Output.Write ("null");
		} else if (value is LlvmIrVariable variable) {
			context.Output.Write (variable.Reference);
		} else {
			context.Output.Write (MonoAndroidHelper.CultureInvariantToString (value));
		}
	}

	protected void WriteAlignment (GeneratorWriteContext context, ulong typeSize, bool isPointer)
	{
		context.Output.Write (", align ");

		ulong alignment;
		if (isPointer) {
			alignment = context.Target.NativePointerSize;
		} else {
			alignment = typeSize;
		}
		context.Output.Write (alignment.ToString (CultureInfo.InvariantCulture));
	}
}

abstract class LlvmIrInstructionArgumentValuePlaceholder
{
	protected LlvmIrInstructionArgumentValuePlaceholder ()
	{}

	public abstract object? GetValue (LlvmIrModuleTarget target);
}

sealed class LlvmIrInstructions
{
	public class Br : LlvmIrInstruction
	{
		const string OpName = "br";

		LlvmIrVariable? cond;
		LlvmIrFunctionLabelItem ifTrue;
		LlvmIrFunctionLabelItem? ifFalse;

		/// <summary>
		/// Outputs a conditional branch to label <paramref name="ifTrue"/> if condition <paramref name="cond"/> is
		/// <c>true</c>, and to label <paramref name="ifFalse"/> otherwise.  <paramref name="cond"/> must be a variable
		/// of type <c>bool</c>
		/// </summary>
		public Br (LlvmIrVariable cond, LlvmIrFunctionLabelItem ifTrue, LlvmIrFunctionLabelItem ifFalse)
			: base (OpName)
		{
			if (cond.Type != typeof(bool)) {
				throw new ArgumentException ($"Internal error: condition must refer to a variable of type 'bool', was 'cond.Type' instead", nameof (cond));
			}

			this.cond = cond;
			this.ifTrue = ifTrue;
			this.ifFalse = ifFalse;
		}

		/// <summary>
		/// Outputs an unconditional branch to label <paramref name="label"/>
		/// </summary>
		public Br (LlvmIrFunctionLabelItem label)
			: base (OpName)
		{
			ifTrue = label;
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			if (cond == null) {
				context.Output.Write ("label %");
				context.Output.Write (ifTrue.Name);
				return;
			}

			context.Output.Write ("i1 ");
			context.Output.Write (cond.Reference);
			context.Output.Write (", label %");
			context.Output.Write (ifTrue.Name);
			context.Output.Write (", label %");
			context.Output.Write (ifFalse.Name);
		}
	}

	public class Call : LlvmIrInstruction
	{
		LlvmIrFunction function;
		IList<object?>? arguments;
		LlvmIrVariable? result;

		public LlvmIrCallMarker CallMarker { get; set; } = LlvmIrCallMarker.None;
		public LlvmIrVariable? FuncPointer { get; set; }

		public Call (LlvmIrFunction function, LlvmIrVariable? result = null, ICollection<object?>? arguments = null)
			: base ("call")
		{
			this.function = function;
			this.result = result;

			if (function.Signature.ReturnType != typeof(void)) {
				if (result == null) {
					throw new ArgumentNullException ($"Internal error: function '{function.Signature.Name}' returns '{function.Signature.ReturnType} and thus requires a result variable", nameof (result));
				}
			} else if (result != null) {
				throw new ArgumentException ($"Internal error: function '{function.Signature.Name}' returns no value and yet a result variable was provided", nameof (result));
			}

			int argCount = function.Signature.Parameters.Count;
			if (argCount != 0) {
				if (arguments == null) {
					throw new ArgumentNullException ($"Internal error: function '{function.Signature.Name}' requires {argCount} arguments", nameof (arguments));
				}

				if (arguments.Count != argCount) {
					throw new ArgumentException ($"Internal error: function '{function.Signature.Name}' requires {argCount} arguments, but {arguments.Count} were provided", nameof (arguments));
				}

				this.arguments = new List<object> (arguments).AsReadOnly ();
			}
		}

		protected override void WriteValueAssignment (GeneratorWriteContext context)
		{
			if (result == null) {
				return;
			}

			context.Output.Write (result.Reference);
			context.Output.Write (" = ");
		}

		protected override void WritePreamble (GeneratorWriteContext context)
		{
			string? callMarker = CallMarker switch {
				LlvmIrCallMarker.None     => null,
				LlvmIrCallMarker.Tail     => "tail",
				LlvmIrCallMarker.NoTail   => "notail",
				LlvmIrCallMarker.MustTail => "musttail",
				_ => throw new InvalidOperationException ($"Internal error: call marker '{CallMarker}' not supported"),
			};

			if (!String.IsNullOrEmpty (callMarker)) {
				context.Output.Write (callMarker);
				context.Output.Write (' ');
			}
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			context.Output.Write (LlvmIrGenerator.MapToIRType (function.Signature.ReturnType));
			if (FuncPointer == null) {
				context.Output.Write (" @");
				context.Output.Write (function.Signature.Name);
			} else {
				context.Output.Write (' ');
				context.Output.Write (FuncPointer.Reference);
			}
			context.Output.Write ('(');

			for (int i = 0; i < function.Signature.Parameters.Count; i++) {
				if (i > 0) {
					context.Output.Write (", ");
				}

				WriteArgument (context, function.Signature.Parameters[i], i);
			}

			context.Output.Write (')');
		}

		void WriteArgument (GeneratorWriteContext context, LlvmIrFunctionParameter parameter, int index)
		{
			context.Output.Write (LlvmIrGenerator.MapToIRType (parameter.Type));
			LlvmIrGenerator.WriteParameterAttributes (context, parameter);
			context.Output.Write (' ');

			object? value = arguments[index];
			if (value is LlvmIrInstructionArgumentValuePlaceholder placeholder) {
				value = placeholder.GetValue (context.Target);
			}

			if (value == null) {
				if (!parameter.Type.IsNativePointer ()) {
					throw new InvalidOperationException ($"Internal error: value for argument {index} to function '{function.Signature.Name}' must not be null");
				}

				context.Output.Write ("null");
				return;
			}

			if (value is LlvmIrVariable variable) {
				context.Output.Write (variable.Reference);
				return;
			}

			if (!parameter.Type.IsAssignableFrom (value.GetType ())) {
				throw new InvalidOperationException ($"Internal error: value type '{value.GetType ()}' for argument {index} to function '{function.Signature.Name}' is invalid. Expected '{parameter.Type}' or compatible");
			}

			if (value is string str) {
				context.Output.Write (context.Module.LookupRequiredVariableForString (str).Reference);
				return;
			}

			if (LlvmIrGenerator.IsFirstClassNonPointerType (value.GetType ())) {
				context.Output.Write (MonoAndroidHelper.CultureInvariantToString (value));
				return;
			}

			throw new InvalidOperationException ($"Internal error: unsupported type '{value.GetType ()}' in call to function '{function.Signature.Name}'");
		}
	}

	public class Icmp : LlvmIrInstruction
	{
		LlvmIrIcmpCond cond;
		LlvmIrVariable op1;
		object? op2;
		LlvmIrVariable result;

		public Icmp (LlvmIrIcmpCond cond, LlvmIrVariable op1, object? op2, LlvmIrVariable result)
			: base ("icmp")
		{
			if (result.Type != typeof(bool)) {
				throw new ArgumentException ($"Internal error: result must be a variable of type 'bool', was '{result.Type}' instead", nameof (result));
			}

			this.cond = cond;
			this.op1 = op1;
			this.op2 = op2;
			this.result = result;
		}

		protected override void WriteValueAssignment (GeneratorWriteContext context)
		{
			context.Output.Write (result.Reference);
			context.Output.Write (" = ");
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (op1.Type, out ulong size, out bool isPointer);
			string condOp = cond switch {
				LlvmIrIcmpCond.Equal => "eq",
				LlvmIrIcmpCond.NotEqual => "ne",
				LlvmIrIcmpCond.UnsignedGreaterThan => "ugt",
				LlvmIrIcmpCond.UnsignedGreaterOrEqual => "uge",
				LlvmIrIcmpCond.UnsignedLessThan => "ult",
				LlvmIrIcmpCond.UnsignedLessOrEqual => "ule",
				LlvmIrIcmpCond.SignedGreaterThan => "sgt",
				LlvmIrIcmpCond.SignedGreaterOrEqual => "sge",
				LlvmIrIcmpCond.SignedLessThan => "slt",
				LlvmIrIcmpCond.SignedLessOrEqual => "sle",
				_ => throw new InvalidOperationException ($"Unsupported `icmp` conditional '{cond}'"),
			};

			context.Output.Write (condOp);
			context.Output.Write (' ');
			context.Output.Write (irType);
			context.Output.Write (' ');
			context.Output.Write (op1.Reference);
			context.Output.Write (", ");
			WriteValue (context, op1.Type, op2, isPointer);
		}
	}

	public class Load : LlvmIrInstruction
	{
		LlvmIrVariable source;
		LlvmIrVariable result;

		public Load (LlvmIrVariable source, LlvmIrVariable result)
			: base ("load")
		{
			this.source = source;
			this.result = result;
		}

		protected override void WriteValueAssignment (GeneratorWriteContext context)
		{
			context.Output.Write (result.Reference);
			context.Output.Write (" = ");
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (result.Type, out ulong size, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (", ptr ");
			WriteValue (context, result.Type, source, isPointer);
			WriteAlignment (context, size, isPointer);
		}
	}

	public class Ret : LlvmIrInstruction
	{
		Type retvalType;
		object? retVal;

		public Ret (Type retvalType, object? retval = null)
			: base ("ret")
		{
			this.retvalType = retvalType;
			retVal = retval;
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			if (retvalType == typeof(void)) {
				context.Output.Write ("void");
				return;
			}

			string irType = LlvmIrGenerator.MapToIRType (retvalType, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, retvalType, retVal, isPointer);
		}
	}

	public class Store : LlvmIrInstruction
	{
		object? from;
		LlvmIrVariable to;

		public Store (LlvmIrVariable from, LlvmIrVariable to)
			: base ("store")
		{
			this.from = from;
			this.to = to;
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (to.Type, out ulong size, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, to.Type, from, isPointer);

			context.Output.Write (", ptr ");
			context.Output.Write (to.Reference);

			WriteAlignment (context, size, isPointer);
		}
	}
}
