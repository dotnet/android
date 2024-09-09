using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Xamarin.Android.Tasks.LLVMIR;

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
	public string? Comment   { get; set; }

	public void Write (GeneratorWriteContext context, LlvmIrGenerator generator)
	{
		DoWrite (context, generator);
		if (!String.IsNullOrEmpty (Comment)) {
			context.Output.Write (' ');
			generator.WriteComment (context, Comment);
		}
		context.Output.WriteLine ();
	}

	protected abstract void DoWrite (GeneratorWriteContext context, LlvmIrGenerator generator);
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

	protected bool NameIsSet () => !String.IsNullOrEmpty (name);
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
		if (NameIsSet ()) {
			return;
		}

		SetName (state.NextTemporary ());
	}

	protected override void DoWrite (GeneratorWriteContext context, LlvmIrGenerator generator)
	{
		context.DecreaseIndent ();

		context.Output.WriteLine ();
		context.Output.Write (context.CurrentIndent);
		context.Output.Write (Name);
		context.Output.Write (':');

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

	protected override void DoWrite (GeneratorWriteContext context, LlvmIrGenerator generator)
	{
		context.Output.Write (context.CurrentIndent);
		generator.WriteCommentLine (context, Text);
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

	List<LlvmIrFunctionBodyItem> items;
	HashSet<string> definedLabels;
	LlvmIrFunction ownerFunction;
	LlvmIrFunction.FunctionState functionState;
	LlvmIrFunctionLabelItem implicitStartBlock;

	LlvmIrFunctionLabelItem? precedingBlock1;
	LlvmIrFunctionLabelItem? precedingBlock2;
	LlvmIrFunctionLabelItem? previousLabel;

	public IList<LlvmIrFunctionBodyItem> Items => items.AsReadOnly ();
	public LlvmIrFunctionLabelItem? PrecedingBlock1 => precedingBlock1;
	public LlvmIrFunctionLabelItem? PrecedingBlock2 => precedingBlock2;

	public LlvmIrFunctionBody (LlvmIrFunction func, LlvmIrFunction.FunctionState functionState)
	{
		ownerFunction = func;
		this.functionState = functionState;
		definedLabels = new HashSet<string> (StringComparer.Ordinal);
		items = new List<LlvmIrFunctionBodyItem> ();
		previousLabel = implicitStartBlock = new LlvmIrFunctionImplicitStartLabel (functionState.StartingBlockNumber);
	}

	public void Add (LlvmIrFunctionLabelItem label, string? comment = null)
	{
		label.WillAddToBody (this, functionState);
		if (definedLabels.Contains (label.Name)) {
			throw new InvalidOperationException ($"Internal error: label with name '{label.Name}' already added to function '{ownerFunction.Signature.Name}' body");
		}
		items.Add (label);
		definedLabels.Add (label.Name);

		// Rotate preceding blocks
		if (precedingBlock2 != null) {
			precedingBlock2 = null;
		}

		precedingBlock2 = precedingBlock1;
		precedingBlock1 = previousLabel;
		previousLabel = label;

		if (comment == null) {
			var sb = new StringBuilder (" preds = %");
			sb.Append (precedingBlock1.Name);
			if (precedingBlock2 != null) {
				sb.Append (", %");
				sb.Append (precedingBlock2.Name);
			}
			comment = sb.ToString ();
		}
		label.Comment = comment;
	}

	public void Add (LlvmIrFunctionBodyItem item)
	{
		items.Add (item);
	}

	public void AddComment (string text)
	{
		Add (new LlvmIrFunctionBodyComment (text));
	}

	public LlvmIrInstructions.Alloca Alloca (LlvmIrVariable result)
	{
		var ret = new LlvmIrInstructions.Alloca (result);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Br Br (LlvmIrFunctionLabelItem label)
	{
		var ret = new LlvmIrInstructions.Br (label);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Br Br (LlvmIrVariable cond, LlvmIrFunctionLabelItem ifTrue, LlvmIrFunctionLabelItem ifFalse)
	{
		var ret = new LlvmIrInstructions.Br (cond, ifTrue, ifFalse);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Call Call (LlvmIrFunction function, LlvmIrVariable? result = null, ICollection<object?>? arguments = null, LlvmIrVariable? funcPointer = null)
	{
		var ret = new LlvmIrInstructions.Call (function, result, arguments, funcPointer);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Ext Ext (LlvmIrVariable source, Type targetType, LlvmIrVariable result)
	{
		var ret = new LlvmIrInstructions.Ext (source, targetType, result);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Icmp Icmp (LlvmIrIcmpCond cond, LlvmIrVariable op1, object? op2, LlvmIrVariable result)
	{
		var ret = new LlvmIrInstructions.Icmp (cond, op1, op2, result);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Load Load (LlvmIrVariable source, LlvmIrVariable result, LlvmIrMetadataItem? tbaa = null)
	{
		var ret = new LlvmIrInstructions.Load (source, result) {
			TBAA = tbaa,
		};
		Add (ret);
		return ret;
	}

	/// <summary>
	/// Creates the `phi` instruction form we use the most throughout marshal methods generator - one which refers to an if/else block and where
	/// **both** value:label pairs are **required**.  Parameters <paramref name="label1"/> and <paramref name="label2"/> are nullable because, in theory,
	/// it is possible that <see cref="LlvmIrFunctionBody"/> hasn't had the required blocks defined prior to adding the `phi` instruction and, thus,
	/// we must check for the possibility here.
	/// </summary>
	public LlvmIrInstructions.Phi Phi (LlvmIrVariable result, LlvmIrVariable val1, LlvmIrFunctionLabelItem? label1, LlvmIrVariable val2, LlvmIrFunctionLabelItem? label2)
	{
		var ret = new LlvmIrInstructions.Phi (result, val1, label1, val2, label2);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Ret Ret (Type retvalType, object? retval = null)
	{
		var ret = new LlvmIrInstructions.Ret (retvalType, retval);
		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Store Store (LlvmIrVariable from, LlvmIrVariable to, LlvmIrMetadataItem? tbaa = null)
	{
		var ret = new LlvmIrInstructions.Store (from, to) {
			TBAA = tbaa,
		};

		Add (ret);
		return ret;
	}

	/// <summary>
	/// Stores `null` in the indicated variable
	/// </summary>
	public LlvmIrInstructions.Store Store (LlvmIrVariable to, LlvmIrMetadataItem? tbaa = null)
	{
		var ret = new LlvmIrInstructions.Store (to) {
			TBAA = tbaa,
		};

		Add (ret);
		return ret;
	}

	public LlvmIrInstructions.Unreachable Unreachable ()
	{
		var ret = new LlvmIrInstructions.Unreachable ();

		Add (ret);
		return ret;
	}
}
