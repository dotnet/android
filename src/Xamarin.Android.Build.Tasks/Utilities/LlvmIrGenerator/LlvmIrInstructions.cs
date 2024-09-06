using System;
using System.Collections.Generic;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVMIR;

abstract class LlvmIrInstruction : LlvmIrFunctionBodyItem
{
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

	protected override void DoWrite (GeneratorWriteContext context, LlvmIrGenerator generator)
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

class LlvmIrInstructionPointerSizeArgumentPlaceholder : LlvmIrInstructionArgumentValuePlaceholder
{
	public override object? GetValue (LlvmIrModuleTarget target)
	{
		return target.NativePointerSize;
	}
}

sealed class LlvmIrInstructions
{
	public class Alloca : LlvmIrInstruction
	{
		LlvmIrVariable result;

		public Alloca (LlvmIrVariable result)
			: base ("alloca")
		{
			this.result = result;
		}

		protected override void WriteValueAssignment (GeneratorWriteContext context)
		{
			if (result == null) {
				return;
			}

			context.Output.Write (result.Reference);
			context.Output.Write (" = ");
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (result.Type, context.TypeCache, out ulong size, out bool isPointer);

			context.Output.Write (irType);
			WriteAlignment (context, size, isPointer);
		}
	}

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

		/// <summary>
		/// This needs to be set if we want to call a function via a local or global variable.  <see cref="LlvmIrFunction"/> passed
		/// to the constructor is then used only to generate a type safe call, while function address comes from the variable assigned
		/// to this property.
		/// </summary>
		public LlvmIrVariable? FuncPointer { get; set; }

		public Call (LlvmIrFunction function, LlvmIrVariable? result = null, ICollection<object?>? arguments = null, LlvmIrVariable? funcPointer = null)
			: base ("call")
		{
			this.function = function;
			this.result = result;
			this.FuncPointer = funcPointer;

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

				if (function.UsesVarArgs) {
					if (arguments.Count < argCount) {
						throw new ArgumentException ($"Internal error: varargs function '{function.Signature.Name}' needs at least {argCount} fixed arguments, got {arguments.Count} instead");
					}
				} else if (arguments.Count != argCount) {
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
			if (function.ReturnsValue) {
				LlvmIrGenerator.WriteReturnAttributes (context, function.Signature.ReturnAttributes);
			}

			context.Output.Write (LlvmIrGenerator.MapToIRType (function.Signature.ReturnType, context.TypeCache));

			if (function.UsesVarArgs) {
				context.Output.Write (" (");
				for (int j = 0; j < function.Signature.Parameters.Count; j++) {
					if (j > 0) {
						context.Output.Write (", ");
					}

					LlvmIrFunctionParameter parameter = function.Signature.Parameters[j];
					string irType = parameter.IsVarArgs ? "..." : LlvmIrGenerator.MapToIRType (parameter.Type, context.TypeCache);
					context.Output.Write (irType);
				}
				context.Output.Write (')');
			}

			if (FuncPointer == null) {
				context.Output.Write (" @");
				context.Output.Write (function.Signature.Name);
			} else {
				context.Output.Write (' ');
				context.Output.Write (FuncPointer.Reference);
			}
			context.Output.Write ('(');

			bool isVararg = false;
			int i;
			for (i = 0; i < function.Signature.Parameters.Count; i++) {
				if (i > 0) {
					context.Output.Write (", ");
				}

				LlvmIrFunctionParameter parameter = function.Signature.Parameters[i];
				if (parameter.IsVarArgs) {
					isVararg = true;
				}

				WriteArgument (context, parameter, i, isVararg);
			}

			if (arguments != null) {
				for (; i < arguments.Count; i++) {
					context.Output.Write (", ");
					WriteArgument (context, null, i, isVararg: true);
				}
			}

			context.Output.Write (')');
		}

		void WriteArgument (GeneratorWriteContext context, LlvmIrFunctionParameter? parameter, int index, bool isVararg)
		{
			object? value = arguments[index];
			if (value is LlvmIrInstructionArgumentValuePlaceholder placeholder) {
				value = placeholder.GetValue (context.Target);
			}

			string irType;
			if (!isVararg) {
				irType = LlvmIrGenerator.MapToIRType (parameter.Type, context.TypeCache);
			} else if (value is LlvmIrVariable v1) {
				irType = LlvmIrGenerator.MapToIRType (v1.Type, context.TypeCache);
			} else {
				if (value == null) {
					// We have no way of verifying the vararg parameter type if value is null, so we'll assume it's a pointer.
					// If our assumption is wrong, llc will fail and signal the error
					irType = "ptr";
				} else {
					irType = LlvmIrGenerator.MapToIRType (value.GetType (), context.TypeCache);
				}
			}

			context.Output.Write (irType);
			if (parameter != null) {
				LlvmIrGenerator.WriteParameterAttributes (context, parameter);
			}
			context.Output.Write (' ');

			if (value == null) {
				if (!parameter.Type.IsNativePointer (context.TypeCache)) {
					throw new InvalidOperationException ($"Internal error: value for argument {index} to function '{function.Signature.Name}' must not be null");
				}

				context.Output.Write ("null");
				return;
			}

			if (value is LlvmIrVariable v2) {
				context.Output.Write (v2.Reference);
				return;
			}

			if (parameter != null && !parameter.Type.IsAssignableFrom (value.GetType ())) {
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

	public class Ext : LlvmIrInstruction
	{
		const string FpextOpCode = "fpext";
		const string SextOpCode  = "sext";
		const string ZextOpCode  = "zext";

		LlvmIrVariable result;
		LlvmIrVariable source;
		Type targetType;

		public Ext (LlvmIrVariable source, Type targetType, LlvmIrVariable result)
			: base (GetOpCode (targetType))
		{
			this.source = source;
			this.targetType = targetType;
			this.result = result;
		}

		protected override void WriteValueAssignment (GeneratorWriteContext context)
		{
			context.Output.Write (result.Reference);
			context.Output.Write (" = ");
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			context.Output.Write (LlvmIrGenerator.MapToIRType (source.Type, context.TypeCache));
			context.Output.Write (' ');
			context.Output.Write (source.Reference);
			context.Output.Write (" to ");
			context.Output.Write ( LlvmIrGenerator.MapToIRType (targetType, context.TypeCache));
		}

		static string GetOpCode (Type targetType)
		{
			if (targetType == typeof(double)) {
				return FpextOpCode;
			} else if (targetType == typeof(int)) {
				return SextOpCode;
			} else if (targetType == typeof(uint)) {
				return ZextOpCode;
			} else {
				throw new InvalidOperationException ($"Unsupported target type for upcasting: {targetType}");
			}
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
			string irType = LlvmIrGenerator.MapToIRType (op1.Type, context.TypeCache, out ulong size, out bool isPointer);
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
			string irType = LlvmIrGenerator.MapToIRType (result.Type, context.TypeCache, out ulong size, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (", ptr ");
			WriteValue (context, result.Type, source, isPointer);
			WriteAlignment (context, size, isPointer);
		}
	}

	public class Phi : LlvmIrInstruction
	{
		LlvmIrVariable result;
		LlvmIrVariable val1;
		LlvmIrFunctionLabelItem label1;
		LlvmIrVariable val2;
		LlvmIrFunctionLabelItem label2;

		/// <summary>
		/// Represents the `phi` instruction form we use the most throughout marshal methods generator - one which refers to an if/else block and where
		/// **both** value:label pairs are **required**.  Parameters <paramref name="label1"/> and <paramref name="label2"/> are nullable because, in theory,
		/// it is possible that <see cref="LlvmIrFunctionBody"/> hasn't had the required blocks defined prior to adding the `phi` instruction and, thus,
		/// we must check for the possibility here.
		/// </summary>
		public Phi (LlvmIrVariable result, LlvmIrVariable val1, LlvmIrFunctionLabelItem? label1, LlvmIrVariable val2, LlvmIrFunctionLabelItem? label2)
			: base ("phi")
		{
			this.result = result;
			this.val1 = val1;
			this.label1 = label1 ?? throw new ArgumentNullException (nameof (label1));
			this.val2 = val2;
			this.label2 = label2 ?? throw new ArgumentNullException (nameof (label2));
		}

		protected override void WriteValueAssignment (GeneratorWriteContext context)
		{
			context.Output.Write (result.Reference);
			context.Output.Write (" = ");
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			context.Output.Write (LlvmIrGenerator.MapToIRType (result.Type, context.TypeCache));
			context.Output.Write (" [");
			context.Output.Write (val1.Reference);
			context.Output.Write (", %");
			context.Output.Write (label1.Name);
			context.Output.Write ("], [");
			context.Output.Write (val2.Reference);
			context.Output.Write (", %");
			context.Output.Write (label2.Name);
			context.Output.Write (']');
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

			string irType = LlvmIrGenerator.MapToIRType (retvalType, context.TypeCache, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, retvalType, retVal, isPointer);
		}
	}

	public class Store : LlvmIrInstruction
	{
		const string Opcode = "store";

		object? from;
		LlvmIrVariable to;

		public Store (LlvmIrVariable from, LlvmIrVariable to)
			: base (Opcode)
		{
			this.from = from;
			this.to = to;
		}

		/// <summary>
		/// Stores `null` in the indicated variable
		/// </summary>
		public Store (LlvmIrVariable to)
			: base (Opcode)
		{
			this.to = to;
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (to.Type, context.TypeCache, out ulong size, out bool isPointer);
			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, to.Type, from, isPointer);

			context.Output.Write (", ptr ");
			context.Output.Write (to.Reference);

			WriteAlignment (context, size, isPointer);
		}
	}

	public class Unreachable : LlvmIrInstruction
	{
		public Unreachable ()
			: base ("unreachable")
		{}
	}

	public class Switch<T> : LlvmIrInstruction where T: struct
	{
		// Since we can't use System.Numerics.IBinaryInteger<T>, this is the poor man's verification that T is acceptable for us
		static readonly HashSet<Type> acceptedTypes = new () {
			typeof (byte),
			typeof (sbyte),
			typeof (short),
			typeof (ushort),
			typeof (int),
			typeof (uint),
			typeof (long),
			typeof (ulong),
		};

		readonly LlvmIrVariable value;
		readonly LlvmIrFunctionLabelItem defaultDest;
		readonly string? automaticLabelPrefix;
		ulong automaticLabelCounter = 0;
		List<(T constant, LlvmIrFunctionLabelItem label, string? comment)>? items;

		public Switch (LlvmIrVariable value, LlvmIrFunctionLabelItem defaultDest, string? automaticLabelPrefix = null)
			: base ("switch")
		{
			if (!acceptedTypes.Contains (typeof(T))) {
				throw new NotSupportedException ($"Type '{typeof(T)}' is unsupported, only integer types are accepted");
			}

			if (value.Type != typeof (T)) {
				throw new ArgumentException ($"Must refer to value of type '{typeof(T)}'", nameof (value));
			}

			this.value = value;
			this.defaultDest = defaultDest;
			this.automaticLabelPrefix = automaticLabelPrefix;

			if (!String.IsNullOrEmpty (automaticLabelPrefix)) {
				items = new ();
			}
		}

		protected override void WriteBody (GeneratorWriteContext context)
		{
			string irType = LlvmIrGenerator.MapToIRType (value.Type, context.TypeCache, out _, out bool isPointer);

			context.Output.Write (irType);
			context.Output.Write (' ');

			WriteValue (context, value.Type, value, isPointer);

			context.Output.Write (", label %");
			context.Output.Write (defaultDest.Name);
			context.Output.WriteLine (" [");
			context.IncreaseIndent ();

			foreach ((T constant, LlvmIrFunctionLabelItem label, string? comment) in items) {
				context.Output.Write (context.CurrentIndent);
				context.Output.Write (irType);
				context.Output.Write (' ');
				context.Generator.WriteValue (context, value.Type, constant);
				context.Output.Write (", label %");
				context.Output.Write (label.Name);
				if (!String.IsNullOrEmpty (comment)) {
					context.Generator.WriteCommentLine (context, comment);
				} else {
					context.Output.WriteLine ();
				}
			}

			context.DecreaseIndent ();
			context.Output.Write (context.CurrentIndent);
			context.Output.Write (']');
		}

		public LlvmIrFunctionLabelItem Add (T val, LlvmIrFunctionLabelItem? dest = null, string? comment = null)
		{
			var label = MakeLabel (dest);
			items.Add ((val, label, comment));
			return label;
		}

		void EnsureValidity (LlvmIrFunctionLabelItem? dest)
		{
			if (dest != null) {
				return;
			}

			if (String.IsNullOrEmpty (automaticLabelPrefix)) {
				throw new InvalidOperationException ($"Internal error: automatic label management requested, but prefix not defined");
			}
		}

		LlvmIrFunctionLabelItem MakeLabel (LlvmIrFunctionLabelItem? maybeDest)
		{
			EnsureValidity (maybeDest);
			if (maybeDest != null) {
				return maybeDest;
			}

			var ret = new LlvmIrFunctionLabelItem (automaticLabelCounter == 0 ? automaticLabelPrefix : $"{automaticLabelPrefix}{automaticLabelCounter}");
			automaticLabelCounter++;

			return ret;
		}
	}
}
