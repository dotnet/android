using System;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVM.IR;

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
			context.Output.Write (' ');
			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, RetvalType, Value, isPointer);
		}
	}
}
