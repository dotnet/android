using System;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVM.IR;

// TODO: remove these aliases once the refactoring is done
using LlvmIrVariableOptions = LLVMIR.LlvmIrVariableOptions;

abstract class LlvmIrVariable : IEquatable<LlvmIrVariable>
{
	public abstract bool Global { get; }
	public abstract string NamePrefix { get; }

	public string? Name { get; protected set; }
	public Type Type { get; protected set; }
	public object? Value { get; set; }
	public string? Comment { get; set; }

	/// <summary>
	/// Both global and local variables will want their names to matter in equality checks, but function
	/// parameters must not take it into account, thus this property.  If set to <c>false</c>, <see cref="Equals(LlvmIrVariable)"/>
	/// will ignore name when checking for equality.
	protected bool NameMatters { get; set; } = true;

	/// <summary>
	/// Returns a string which constitutes a reference to a local (using the <c>%</c> prefix character) or a global
	/// (using the <c>@</c> prefix character) variable, ready for use in the generated code wherever variables are
	/// referenced.
	/// </summary>
	public string Reference {
		get {
			if (String.IsNullOrEmpty (Name)) {
				throw new InvalidOperationException ("Variable doesn't have a name, it cannot be referenced");
			}

			return $"{NamePrefix}{Name}";
		}
	}

	/// <summary>
	/// Certain data must be calculated when the target architecture is known, because it may depend on certain aspects of
	/// the target (e.g. its bitness).  This callback, if set, will be invoked before the variable is written to the output
	/// stream, allowing updating of any such data as described above.
	/// </summary>
	public Action<LlvmIrVariable, LlvmIrModuleTarget>? BeforeWriteCallback { get; set; }

	/// <summary>
	/// Constructs an abstract variable. <paramref name="type"/> is translated to one of the LLVM IR first class types (see
	/// https://llvm.org/docs/LangRef.html#t-firstclass) only if it's an integral or floating point type.  In all other cases it
	/// is treated as an opaque pointer type.
	/// </summary>
	protected LlvmIrVariable (Type type, string? name = null)
	{
		Type = type;
		Name = name;
	}

	public override int GetHashCode ()
	{
		return Type.GetHashCode () ^ (Name?.GetHashCode () ?? 0);
	}

	public override bool Equals (object obj)
	{
		var irVar = obj as LlvmIrVariable;
		if (irVar == null) {
			return false;
		}

		return Equals (irVar);
	}

	public virtual bool Equals (LlvmIrVariable other)
	{
		if (other == null) {
			return false;
		}

		return
			Global == other.Global &&
			Type == other.Type &&
			String.Compare (NamePrefix, other.NamePrefix, StringComparison.Ordinal) == 0 &&
			(!NameMatters || String.Compare (Name, other.Name, StringComparison.Ordinal) == 0);
	}
}

class LlvmIrLocalVariable : LlvmIrVariable
{
	public override bool Global => false;
	public override string NamePrefix => "%";

	/// <summary>
	/// Constructs a local variable. <paramref name="type"/> is translated to one of the LLVM IR first class types (see
	/// https://llvm.org/docs/LangRef.html#t-firstclass) only if it's an integral or floating point type.  In all other cases it
	/// is treated as an opaque pointer type.  <paramref name="name"/> is optional because local variables can be unnamed, in
	/// which case they will be assigned a sequential number when function code is generated.
	/// </summary>
	public LlvmIrLocalVariable (Type type, string? name = null)
		: base (type, name)
	{}

	public void AssignNumber (uint n)
	{
		Name = n.ToString (CultureInfo.InvariantCulture);
	}
}

class LlvmIrGlobalVariable : LlvmIrVariable
{
	/// <summary>
	/// By default a global variable is constant and exported.
	/// </summary>
	public static readonly LlvmIrVariableOptions DefaultOptions = LlvmIrVariableOptions.GlobalConstant;

	public override bool Global => true;
	public override string NamePrefix => "@";

	/// <summary>
	/// Specify variable options. If omitted, it defaults to <see cref="DefaultOptions"/>.
	/// <seealso href="https://llvm.org/docs/LangRef.html#global-variables"/>
	/// </summary>
	public LlvmIrVariableOptions? Options { get; set; }

	/// <summary>
	/// Constructs a local variable. <paramref name="type"/> is translated to one of the LLVM IR first class types (see
	/// https://llvm.org/docs/LangRef.html#t-firstclass) only if it's an integral or floating point type.  In all other cases it
	/// is treated as an opaque pointer type.  <paramref name="name"/> is required because global variables must be named.
	/// </summary>
	public LlvmIrGlobalVariable (Type type, string name, LlvmIrVariableOptions? options = null)
		: base (type, name)
	{
		if (String.IsNullOrEmpty (name)) {
			throw new ArgumentException ("must not be null or empty", nameof (name));
		}

		Options = options;
	}

	/// <summary>
	/// Supports instances where a variable value must be processed by <see cref="LlvmIrModule"/> (for instance for arrays).
	/// Should **not** be used by code other than LlvmIrModule.
	/// <summary>
	public void OverrideValue (Type newType, object? newValue)
	{
		if (newValue != null && !newType.IsAssignableFrom (newValue.GetType ())) {
			throw new ArgumentException ($"Must be exactly, or derived from, the '{newType}' type.", nameof (newValue));
		}

		Type = newType;
		Value = newValue;
	}
}

class LlvmIrStringVariable : LlvmIrGlobalVariable
{
	public LlvmIrStringVariable (string name, string value)
		: base (typeof(string), name, LlvmIrVariableOptions.LocalString)
	{
		Value = value;
	}
}
