using System;
using System.Collections.Generic;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	/// <summary>
	/// Abstract class from which all of the items (labels, function parameters,
	/// local variables and instructions) derive.
	/// </summary>
	abstract class LlvmIrFunctionBodyItem
	{
		/// <summary>
		/// If an item has this property set to <c>true</c>, it won't be written to output when
		/// code is generated.  This is used for implicit items that don't need to be part of
		/// the generated code (e.g. the starting block label)
		/// </summary>
		public bool SkipInOutput { get; protected set; }
		public abstract void Write (GeneratorWriteContext context);
	}

	/// <summary>
	/// Base class for function labels and local variables (including parameters), which
	/// obtain automatic names derived from a shared counter, unless explicitly named.
	/// </summary>
	abstract class LlvmIrFunctionLocalItem : LlvmIrFunctionBodyItem
	{
		string? name;

		public string Name {
			get {
				if (String.IsNullOrEmpty (name)) {
					throw new InvalidOperationException ("Internal error: name hasn't been set yet");
				}
				return name;
			}

			protected set {
				if (String.IsNullOrEmpty (value)) {
					throw new InvalidOperationException ("Internal error: value must not be null or empty");
				}
				name = value;
			}
		}

		protected LlvmIrFunctionLocalItem (string? name)
		{
			if (name != null) {
				Name = name;
			}
		}

		protected LlvmIrFunctionLocalItem (LlvmIrFunction.FunctionState state, string? name)
		{
			if (name != null) {
				if (name.Length == 0) {
					throw new ArgumentException ("must not be an empty string", nameof (name));
				}

				Name = name;
				return;
			}

			SetName (state.NextTemporary ());
		}

		protected void SetName (ulong num)
		{
			Name = num.ToString (CultureInfo.InvariantCulture);
		}
	}

	class LlvmIrFunctionLabelItem : LlvmIrFunctionLocalItem
	{
		/// <summary>
		/// Labels are a bit peculiar in that they must not have their name set to the automatic value (based on
		/// a counter shared with function parameters) at creation time, but only when they are actually added to
		/// the function body.  The reason is that LLVM IR requires all the unnamed temporaries (function parameters and
		/// labels) to be named sequentially, but sometimes a label must be referenced before it is added to the instruction
		/// stream, e.g. in the <c>br</c> instruction.  On the other hand, it is perfectly fine to assign label a name that
		/// isn't an integer at **instantiation** time, which is why we have the <paramref name="name"/> parameter here.
		/// </summary>
		public LlvmIrFunctionLabelItem (string? name = null)
			: base (name)
		{}

		public void WillAddToBody (LlvmIrFunctionBody functionBody, LlvmIrFunction.FunctionState state)
		{
			if (!String.IsNullOrEmpty (Name)) {
				return;
			}

			SetName (state.NextTemporary ());
		}

		public override void Write (GeneratorWriteContext context)
		{
			context.DecreaseIndent ();

			context.Output.Write (context.CurrentIndent);
			context.Output.Write (Name);
			context.Output.WriteLine (':');

			context.IncreaseIndent ();
		}
	}

	class LlvmIrFunctionBodyComment : LlvmIrFunctionBodyItem
	{
		public string Text     { get; }

		public LlvmIrFunctionBodyComment (string comment)
		{
			Text = comment;
		}

		public override void Write (GeneratorWriteContext context)
		{
			context.Output.Write (context.CurrentIndent);
			context.Output.Write (';');
			context.Output.WriteLine (Text);
		}
	}

	class LlvmIrFunctionBody
	{
		sealed class LlvmIrFunctionImplicitStartLabel : LlvmIrFunctionLabelItem
		{
			public LlvmIrFunctionImplicitStartLabel (ulong num)
			{
				SetName (num);
				SkipInOutput = true;
			}
		}

		sealed class LlvmIrFunctionParameterItem : LlvmIrFunctionLocalItem
		{
			public LlvmIrFunctionParameter Parameter { get; }

			public LlvmIrFunctionParameterItem (LlvmIrFunction.FunctionState state, LlvmIrFunctionParameter parameter)
				: base (state, parameter.Name)
			{
				Parameter = parameter;
				SkipInOutput = true;
			}

			public override void Write (GeneratorWriteContext context)
			{
				throw new NotSupportedException ("Internal error: writing not supported for this item");
			}
		}

		List<LlvmIrFunctionBodyItem> items;
		HashSet<string> definedLabels;
		LlvmIrFunction function;
		LlvmIrFunction.FunctionState functionState;
		LlvmIrFunctionLabelItem implicitStartBlock;

		public IList<LlvmIrFunctionBodyItem> Items => items.AsReadOnly ();

		public LlvmIrFunctionBody (LlvmIrFunction func, LlvmIrFunction.FunctionState functionState)
		{
			function = func;
			this.functionState = functionState;
			definedLabels = new HashSet<string> (StringComparer.Ordinal);
			items = new List<LlvmIrFunctionBodyItem> ();
			implicitStartBlock = new LlvmIrFunctionImplicitStartLabel (functionState.StartingBlockNumber);
		}

		public void Add (LlvmIrFunctionLabelItem label)
		{
			label.WillAddToBody (this, functionState);
			if (definedLabels.Contains (label.Name)) {
				throw new InvalidOperationException ($"Internal error: label with name '{label.Name}' already added to function '{function.Signature.Name}' body");
			}
			items.Add (label);
			definedLabels.Add (label.Name);
		}

		public void Add (LlvmIrFunctionBodyItem item)
		{
			items.Add (item);
		}
	}
}
