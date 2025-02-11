using System;
using System.Collections;
using System.Globalization;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR;

[Flags]
enum LlvmIrVariableWriteOptions
{
	None                    = 0x0000,
	ArrayWriteIndexComments = 0x0001,
	ArrayFormatInRows       = 0x0002,
}

enum LlvmIrVariableNumberFormat
{
	Default,
	Hexadecimal,
	Decimal,
}

abstract class LlvmIrVariable : IEquatable<LlvmIrVariable>
{
	public abstract bool Global                    { get; }
	public abstract string NamePrefix              { get; }

	public string? Name                            { get; protected set; }
	public Type Type                               { get; protected set; }
	public LlvmIrVariableWriteOptions WriteOptions { get; set; } = LlvmIrVariableWriteOptions.ArrayWriteIndexComments;

	/// <summary>
	/// Number of columns an array that is written in rows should have.  By default, arrays are written one item in a line, but
	/// when the <see cref="LlvmIrVariableWriteOptions.ArrayFormatInRows"/> flag is set in <see cref="WriteOptions"/>, then
	/// the value of this property dictates how many items are to be placed in a single row.
	/// </summary>
	public uint ArrayStride                        { get; set; } = 8;
	public object? Value                           { get; set; }
	public string? Comment                         { get; set; }

	public LlvmIrVariableNumberFormat NumberFormat { get; set; } = LlvmIrVariableNumberFormat.Decimal;

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
	public virtual string Reference {
		get {
			if (String.IsNullOrEmpty (Name)) {
				throw new InvalidOperationException ("Variable doesn't have a name, it cannot be referenced");
			}

			return $"{NamePrefix}{Name}";
		}
	}

	/// <summary>
	/// <para>
	/// Certain data must be calculated when the target architecture is known, because it may depend on certain aspects of
	/// the target (e.g. its bitness).  This callback, if set, will be invoked before the variable is written to the output
	/// stream, allowing updating of any such data as described above.
	/// </para>
	/// <para>
	/// First parameter passed to the callback is the variable itself, second parameter is the current
	/// <see cref="LlvmIrModuleTarget"/> and the third is the value previously assigned to <see cref="BeforeWriteCallbackCallerState"/>
	/// </para>
	/// </summary>
	public Action<LlvmIrVariable, LlvmIrModuleTarget, object?>? BeforeWriteCallback { get; set; }

	/// <summary>
	/// Object passed to the <see cref="BeforeWriteCallback"/> method, if any, as the caller state.
	/// </summary>
	public object? BeforeWriteCallbackCallerState { get; set; }

	/// <summary>
	/// <para>
	/// Callback used when processing array variables, called for each item of the array in order to obtain the item's comment, if any.
	/// </para>
	/// <para>
	/// The first argument is the variable which contains the array, second is the item index, third is the item value and fourth is
	/// the caller state object, previously assigned to the <see cref="GetArrayItemCommentCallbackCallerState"/> property.  The callback
	/// can return an empty string or <c>null</c>, in which case no comment is written.
	/// </para>
	/// </summary>
	public Func<LlvmIrVariable, LlvmIrModuleTarget, ulong, object?, object?, string?>? GetArrayItemCommentCallback { get; set; }

	/// <summary>
	/// Object passed to the <see cref="GetArrayItemCommentCallback"/> method, if any, as the caller state.
	/// </summary>
	public object? GetArrayItemCommentCallbackCallerState { get; set; }

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

	public void AssignNumber (ulong n)
	{
		Name = n.ToString (CultureInfo.InvariantCulture);
	}
}

enum LlvmIrStreamedArrayDataProviderState
{
	NextSection,
	LastSection,
	NextSectionNoData,
	LastSectionNoData,
}

abstract class LlvmIrStreamedArrayDataProvider
{
	/// <summary>
	/// Type of every member of the array returned by <see cref="GetData ()"/>.  Generator will check
	/// every member type against this property, allowing also derived types.
	/// </summary>
	public Type ArrayElementType { get; }

	protected LlvmIrStreamedArrayDataProvider (Type arrayElementType)
	{
		ArrayElementType = arrayElementType;
	}

	/// <summary>
	/// Whenever <see cref="GetData ()"/> returns the generator will call this method to obtain the new section
	/// comment, if any, to be output before the actual data.  Returning `String.Empty` prevents the comment
	/// from being added.
	/// </summary>
	public virtual string GetSectionStartComment (LlvmIrModuleTarget target) => String.Empty;

	/// <summary>
	/// Provide the next chunk of data for the specified target (ABI).  Implementations need to return at least one
	/// non-empty collection of data.  The returned collection **must** be exactly the size of contained data (e.g. it cannot be
	/// a byte array rented from a byte pool, because these can be bigger than requested.  When returning the last (or the only) section,
	/// <paramref name="status"/> must have a value of <see cref="LlvmIrStreamedArrayDataProviderState.LastSection"/>.
	/// Each section may be preceded by a comment, <see cref="GetSectionStartComment"/>.
	/// </summary>
	public abstract (LlvmIrStreamedArrayDataProviderState status, ICollection data) GetData (LlvmIrModuleTarget target);

	/// <summary>
	/// Provide the total data size for the specified target (ABI).  This needs to be used instead of <see cref="LlvmIrVariable.ArrayItemCount"/>
	/// because a variable instance is created once and shared by all targets, while per-target data sets might have different sizes.
	/// </summary>
	public abstract ulong GetTotalDataSize (LlvmIrModuleTarget target);
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
	public virtual LlvmIrVariableOptions? Options { get; set; }

	/// <summary>
	/// There are situations when a variable differs enough between architectures, that the difference cannot be
	/// handled with <seealso cref="BeforeWriteCallback"/>.  In such situations one can create a separate variable
	/// for each architecture and set this property.
	/// </summary>
	public AndroidTargetArch? TargetArch { get; set; }

	/// <summary>
	/// If set to `true`, initialize the array with a shortcut zero-initializer statement.  Useful when pre-allocating
	/// space for runtime use that won't be filled in with any data at the build time.
	/// </summary>
	public bool ZeroInitializeArray { get; set; }

	/// <summary>
	/// Specify number of items in an array. Used in cases when we want to pre-allocate an array without giving it any
	/// value, thus making it impossible for the generator to discover the number of items automatically.  This is useful
	/// when using <seealso cref="ZeroInitializeArray" />.  This property is used **only** if the variable <see cref="LlvmIrVariable.Value"/>
	/// is `null`.
	/// </summary>
	public ulong ArrayItemCount { get; set; }

	/// <summary>
	/// If set, it will override any automatically calculated alignment for this variable
	/// </summary>
	public ulong? Alignment                        { get; set; }

	/// <summary>
	/// If set, the provider will be called to obtain all the data to be placed in an array variable. The total amount
	/// of data that will be returned by the provider **must** be specified in the <see cref="ArrayItemCount"/> property,
	/// in order for the generator to properly declare the variable.  The generator will verify that the amount of data
	/// is exactly that much and throw an exception otherwise.
	/// </summary>
	public LlvmIrStreamedArrayDataProvider? ArrayDataProvider { get; set; }

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
	/// Constructs a local variable and sets the <see cref="Value"/> property to <paramref name="value"/> and <see cref="Type"/>
	/// property to its type.  For that reason, <paramref name="Value"/> **must not** be <c>null</c>.  <paramref name="name"/> is
	/// required because global variables must be named.
	/// </summary>
	public LlvmIrGlobalVariable (object value, string name, LlvmIrVariableOptions? options = null)
		: this ((value ?? throw new ArgumentNullException (nameof (value))).GetType (), name, options)
	{
		Value = value;
	}

	/// <summary>
	/// This is, unfortunately, needed to be able to address scenarios when a single symbol can have a different type when
	/// generating output for a specific target (e.g. 32-bit vs 64-bit integer variables).  If the variable requires such
	/// type changes, this should be done at generation time from within the <see cref="BeforeWriteCallback"/> method.
	/// </summary>
	public void OverrideTypeAndValue (Type newType, object? newValue)
	{
		Type = newType;
		Value = newValue;
	}

	public void OverrideName (string newName)
	{
		Name = newName;
	}
}

class LlvmIrStringVariable : LlvmIrGlobalVariable
{
	public LlvmIrStringEncoding Encoding { get; }
	public string IrType { get; }
	public bool IsConstantStringLiteral { get; }

	public LlvmIrStringVariable (string name, StringHolder value, LlvmIrVariableOptions? options = null)
		: base (typeof(string), name, options ?? LlvmIrVariableOptions.GlobalConstexprString)
	{
		Value = value;
		Encoding = value.Encoding;

		switch (value.Encoding) {
			case LlvmIrStringEncoding.UTF8:
				IrType = "i8";
				IsConstantStringLiteral = true;
				break;

			case LlvmIrStringEncoding.Unicode:
				IrType = "i16";
				break;

			default:
				throw new InvalidOperationException ($"Internal error: unsupported string encoding {value.Encoding}");
		}
	}

	public LlvmIrStringVariable (string name, string value, LlvmIrStringEncoding encoding, StringComparison comparison = StringComparison.Ordinal, LlvmIrVariableOptions? options = null)
		: this (name, new StringHolder (value, encoding, comparison), options)
	{}
}

/// <summary>
/// This is to address my dislike to have single-line variables separated by empty lines :P.
/// When an instance of this "variable" is first encountered, it enables variable grouping, that is
/// they will be followed by just a single newline.  The next instance of this "variable" turns
/// grouping off, meaning the following variables will be followed by two newlines.
/// </summary>
class LlvmIrGroupDelimiterVariable : LlvmIrGlobalVariable
{
	public LlvmIrGroupDelimiterVariable ()
		: base (typeof(void), ".:!GroupDelimiter!:.", LlvmIrVariableOptions.LocalConstant)
	{}
}
