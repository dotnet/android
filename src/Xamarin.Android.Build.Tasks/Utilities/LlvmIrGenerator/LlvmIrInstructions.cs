using System;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVM.IR;

// TODO: remove these aliases once the refactoring is done
using LlvmIrIcmpCond = LLVMIR.LlvmIrIcmpCond;

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

	protected virtual void WriteValueAssignment (GeneratorWriteContext context)
	{}

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
		public LlvmIrVariable Source { get; }
		public LlvmIrVariable Result { get; }

		public Load (LlvmIrVariable source, LlvmIrVariable result)
			: base ("load")
		{
			Source = source;
			Result = result;
		}

		protected override void WriteValueAssignment (GeneratorWriteContext context)
		{
			context.Output.Write (Result.Reference);
			context.Output.Write (" = ");
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (Result.Type, out ulong size, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (", ptr ");
			WriteValue (context, Result.Type, Source, isPointer);
			WriteAlignment (context, size, isPointer);
		}
	}

	public class Ret : LlvmIrInstruction
	{
		public Type RetvalType { get; }
		public object? Value   { get; }

		public Ret (Type retvalType, object? retval = null)
			: base ("ret")
		{
			RetvalType = retvalType;
			Value = retval;
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			if (RetvalType == typeof(void)) {
				context.Output.Write ("void");
				return;
			}

			string irType = LlvmIrGenerator.MapToIRType (RetvalType, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, RetvalType, Value, isPointer);
		}
	}

	public class Store : LlvmIrInstruction
	{
		public object? From        { get; }
		public LlvmIrVariable To   { get; }

		public Store (LlvmIrVariable from, LlvmIrVariable to)
			: base ("store")
		{
			From = from;
			To = to;
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (To.Type, out ulong size, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, To.Type, From, isPointer);

			context.Output.Write (", ptr ");
			context.Output.Write (To.Reference);

			WriteAlignment (context, size, isPointer);
		}
	}
}
