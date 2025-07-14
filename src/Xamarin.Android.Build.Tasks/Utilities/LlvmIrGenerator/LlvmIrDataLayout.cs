using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Abstract base class for LLVM IR data layout fields. Data layout fields specify
/// alignment and size information for various data types in LLVM IR.
/// See: https://llvm.org/docs/LangRef.html#data-layout
/// </summary>
abstract class LlvmIrDataLayoutField
{
	/// <summary>
	/// The separator character used between data layout field components.
	/// </summary>
	public const char Separator = ':';

	/// <summary>
	/// Gets the identifier for this data layout field.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrDataLayoutField"/> class.
	/// </summary>
	/// <param name="id">The identifier for the data layout field.</param>
	/// <exception cref="ArgumentException">Thrown when id is null or empty.</exception>
	protected LlvmIrDataLayoutField (string id)
	{
		if (String.IsNullOrEmpty (id)) {
			throw new ArgumentException (nameof (id), "must not be null or empty");
		}

		Id = id;
	}

	/// <summary>
	/// Renders this data layout field to a string builder.
	/// </summary>
	/// <param name="sb">The string builder to render to.</param>
	public virtual void Render (StringBuilder sb)
	{
		sb.Append (Id);
	}

	/// <summary>
	/// Converts an unsigned integer to its string representation using invariant culture.
	/// </summary>
	/// <param name="v">The value to convert.</param>
	/// <returns>The string representation of the value.</returns>
	public static string ConvertToString (uint v)
	{
		return v.ToString (CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Appends an unsigned integer value to the string builder with optional separator.
	/// </summary>
	/// <param name="sb">The string builder to append to.</param>
	/// <param name="v">The value to append.</param>
	/// <param name="needSeparator">Whether to prepend the separator character.</param>
	protected void Append (StringBuilder sb, uint v, bool needSeparator = true)
	{
		if (needSeparator) {
			sb.Append (Separator);
		}

		sb.Append (ConvertToString (v));
	}

	/// <summary>
	/// Appends a nullable unsigned integer value to the string builder with optional separator.
	/// If the value is null, nothing is appended.
	/// </summary>
	/// <param name="sb">The string builder to append to.</param>
	/// <param name="v">The nullable value to append.</param>
	/// <param name="needSeparator">Whether to prepend the separator character.</param>
	protected void Append (StringBuilder sb, uint? v, bool needSeparator = true)
	{
		if (!v.HasValue) {
			return;
		}

		Append (sb, v.Value, needSeparator);
	}
}

/// <summary>
/// Represents pointer size and alignment information in LLVM IR data layout.
/// Specifies the size and alignment of pointers for a given address space.
/// </summary>
class LlvmIrDataLayoutPointerSize : LlvmIrDataLayoutField
{
	/// <summary>
	/// Gets or sets the address space for this pointer specification. If null, applies to address space 0.
	/// </summary>
	public uint? AddressSpace { get; set; }
	/// <summary>
	/// Gets the ABI alignment for pointers in bits.
	/// </summary>
	public uint Abi           { get; }
	/// <summary>
	/// Gets the size of pointers in bits.
	/// </summary>
	public uint Size          { get; }
	/// <summary>
	/// Gets or sets the preferred alignment for pointers in bits.
	/// </summary>
	public uint? Pref         { get; set; }
	/// <summary>
	/// Gets or sets the index size for GEP operations in bits.
	/// </summary>
	public uint? Idx          { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrDataLayoutPointerSize"/> class.
	/// </summary>
	/// <param name="size">The size of pointers in bits.</param>
	/// <param name="abi">The ABI alignment for pointers in bits.</param>
	public LlvmIrDataLayoutPointerSize (uint size, uint abi)
		: base ("p")
	{
		Size = size;
		Abi = abi;
	}

	/// <summary>
	/// Renders this pointer specification to the string builder.
	/// </summary>
	/// <param name="sb">The string builder to render to.</param>
	public override void Render (StringBuilder sb)
	{
		base.Render (sb);

		if (AddressSpace.HasValue && AddressSpace.Value > 0) {
			Append (sb, AddressSpace.Value, needSeparator: false);
		}
		Append (sb, Size);
		Append (sb, Abi);
		Append (sb, Pref);
		Append (sb, Idx);
	}
}

/// <summary>
/// Abstract base class for LLVM IR data layout type alignment specifications.
/// Defines common properties for type alignment including size, ABI alignment, and preferred alignment.
/// </summary>
abstract class LlvmIrDataLayoutTypeAlignment : LlvmIrDataLayoutField
{
	/// <summary>
	/// Gets the size of the type in bits.
	/// </summary>
	public uint Size  { get; }
	/// <summary>
	/// Gets the ABI alignment for the type in bits.
	/// </summary>
	public uint Abi   { get; }
	/// <summary>
	/// Gets or sets the preferred alignment for the type in bits.
	/// </summary>
	public uint? Pref { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrDataLayoutTypeAlignment"/> class.
	/// </summary>
	/// <param name="id">The identifier for the alignment specification.</param>
	/// <param name="size">The size of the type in bits.</param>
	/// <param name="abi">The ABI alignment for the type in bits.</param>
	protected LlvmIrDataLayoutTypeAlignment (string id, uint size, uint abi)
		: base (id)
	{
		Size = size;
		Abi = abi;
	}

	/// <summary>
	/// Renders this type alignment specification to the string builder.
	/// </summary>
	/// <param name="sb">The string builder to render to.</param>
	public override void Render (StringBuilder sb)
	{
		base.Render (sb);

		Append (sb, Size, needSeparator: false);
		Append (sb, Abi);
		Append (sb, Pref);
	}
}

/// <summary>
/// Represents integer type alignment specification for LLVM IR data layout.
/// Specifies alignment requirements for integer types of various sizes.
/// </summary>
class LlvmIrDataLayoutIntegerAlignment : LlvmIrDataLayoutTypeAlignment
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrDataLayoutIntegerAlignment"/> class.
	/// </summary>
	/// <param name="size">The size of the integer type in bits.</param>
	/// <param name="abi">The ABI alignment for the integer type in bits.</param>
	/// <param name="pref">The preferred alignment for the integer type in bits.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when ABI alignment for i8 is not 8.</exception>
	public LlvmIrDataLayoutIntegerAlignment (uint size, uint abi, uint? pref = null)
		: base ("i", size, abi)
	{
		if (size == 8 && abi != 8) {
			throw new ArgumentOutOfRangeException (nameof (abi), "Must equal 8 for i8");
		}

		Pref = pref;
	}
}

/// <summary>
/// Represents vector type alignment specification for LLVM IR data layout.
/// Specifies alignment requirements for vector types of various sizes.
/// </summary>
class LlvmIrDataLayoutVectorAlignment : LlvmIrDataLayoutTypeAlignment
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrDataLayoutVectorAlignment"/> class.
	/// </summary>
	/// <param name="size">The size of the vector type in bits.</param>
	/// <param name="abi">The ABI alignment for the vector type in bits.</param>
	/// <param name="pref">The preferred alignment for the vector type in bits.</param>
	public LlvmIrDataLayoutVectorAlignment (uint size, uint abi, uint? pref = null)
		: base ("v", size, abi)
	{
		Pref = pref;
	}
}

/// <summary>
/// Represents floating-point type alignment specification for LLVM IR data layout.
/// Specifies alignment requirements for floating-point types of various sizes.
/// </summary>
class LlvmIrDataLayoutFloatAlignment : LlvmIrDataLayoutTypeAlignment
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrDataLayoutFloatAlignment"/> class.
	/// </summary>
	/// <param name="size">The size of the floating-point type in bits.</param>
	/// <param name="abi">The ABI alignment for the floating-point type in bits.</param>
	/// <param name="pref">The preferred alignment for the floating-point type in bits.</param>
	public LlvmIrDataLayoutFloatAlignment (uint size, uint abi, uint? pref = null)
		: base ("f", size, abi)
	{
		Pref = pref;
	}
}

/// <summary>
/// Represents aggregate object alignment specification for LLVM IR data layout.
/// Specifies alignment requirements for aggregate objects (structures, arrays, etc.).
/// </summary>
class LlvmIrDataLayoutAggregateObjectAlignment : LlvmIrDataLayoutField
{
	/// <summary>
	/// Gets the ABI alignment for aggregate objects in bits.
	/// </summary>
	public uint Abi   { get; }
	/// <summary>
	/// Gets or sets the preferred alignment for aggregate objects in bits.
	/// </summary>
	public uint? Pref { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrDataLayoutAggregateObjectAlignment"/> class.
	/// </summary>
	/// <param name="abi">The ABI alignment for aggregate objects in bits.</param>
	/// <param name="pref">The preferred alignment for aggregate objects in bits.</param>
	public LlvmIrDataLayoutAggregateObjectAlignment (uint abi, uint? pref = null)
		: base ("a")
	{
		Abi = abi;
		Pref = pref;
	}

	/// <summary>
	/// Renders this aggregate object alignment specification to the string builder.
	/// </summary>
	/// <param name="sb">The string builder to render to.</param>
	public override void Render (StringBuilder sb)
	{
		base.Render (sb);

		Append (sb, Abi);
		Append (sb, Pref);
	}
}

enum LlvmIrDataLayoutFunctionPointerAlignmentType
{
	Independent,
	Multiple,
}

class LlvmIrDataLayoutFunctionPointerAlignment : LlvmIrDataLayoutField
{
	public uint Abi                                          { get; }
	public LlvmIrDataLayoutFunctionPointerAlignmentType Type { get; }

	public LlvmIrDataLayoutFunctionPointerAlignment (LlvmIrDataLayoutFunctionPointerAlignmentType type, uint abi)
		: base ("F")
	{
		Type = type;
		Abi = abi;
	}

	public override void Render (StringBuilder sb)
	{
		base.Render (sb);

		char type = Type switch {
			LlvmIrDataLayoutFunctionPointerAlignmentType.Independent => 'i',
			LlvmIrDataLayoutFunctionPointerAlignmentType.Multiple => 'n',
			_ => throw new InvalidOperationException ($"Unsupported function pointer alignment type '{Type}'")
		};
		sb.Append (type);
		Append (sb, Abi, needSeparator: false);
	}
}

enum LlvmIrDataLayoutManglingOption
{
	ELF,
	GOFF,
	MIPS,
	MachO,
	WindowsX86COFF,
	WindowsCOFF,
	XCOFF
}

class LlvmIrDataLayoutMangling : LlvmIrDataLayoutField
{
	public LlvmIrDataLayoutManglingOption Option { get; }

	public LlvmIrDataLayoutMangling (LlvmIrDataLayoutManglingOption option)
		: base ("m")
	{
		Option = option;
	}

	public override void Render (StringBuilder sb)
	{
		base.Render (sb);

		sb.Append (Separator);

		char opt = Option switch {
			LlvmIrDataLayoutManglingOption.ELF => 'e',
			LlvmIrDataLayoutManglingOption.GOFF => 'l',
			LlvmIrDataLayoutManglingOption.MIPS => 'm',
			LlvmIrDataLayoutManglingOption.MachO => 'o',
			LlvmIrDataLayoutManglingOption.WindowsX86COFF => 'x',
			LlvmIrDataLayoutManglingOption.WindowsCOFF => 'w',
			LlvmIrDataLayoutManglingOption.XCOFF => 'a',
			_ => throw new InvalidOperationException ($"Unsupported mangling option '{Option}'")
		};

		sb.Append (opt);
	}
}

// See: https://llvm.org/docs/LangRef.html#data-layout
class LlvmIrDataLayout
{
	bool bigEndian;
	bool littleEndian = true;

	public bool BigEndian {
		get => bigEndian;
		set {
			bigEndian = value;
			littleEndian = !bigEndian;
		}
	}

	public bool LittleEndian {
		get => littleEndian;
		set {
			littleEndian = value;
			bigEndian = !littleEndian;
		}
	}

	public uint? AllocaAddressSpaceId                                         { get; set; }
	public uint? GlobalsAddressSpaceId                                        { get; set; }
	public LlvmIrDataLayoutMangling? Mangling                                 { get; set; }
	public uint? ProgramAddressSpaceId                                        { get; set; }
	public uint? StackAlignment                                               { get; set; }

	public LlvmIrDataLayoutAggregateObjectAlignment? AggregateObjectAlignment { get; set; }
	public List<LlvmIrDataLayoutFloatAlignment>? FloatAlignment               { get; set; }
	public LlvmIrDataLayoutFunctionPointerAlignment? FunctionPointerAlignment { get; set; }
	public List<LlvmIrDataLayoutIntegerAlignment>? IntegerAlignment           { get; set; }
	public List<LlvmIrDataLayoutVectorAlignment>? VectorAlignment             { get; set; }
	public List<LlvmIrDataLayoutPointerSize>? PointerSize                     { get; set; }

	public List<uint>? NativeIntegerWidths                                    { get; set; }
	public List<uint>? NonIntegralPointerTypeAddressSpaces                    { get; set; }

	public string Render ()
	{
		var sb = new StringBuilder ();

		sb.Append ("target datalayout = \"");

		sb.Append (LittleEndian ? 'e' : 'E');

		if (Mangling != null) {
			sb.Append ('-');
			Mangling.Render (sb);
		}

		AppendFieldList (PointerSize);

		if (FunctionPointerAlignment != null) {
			sb.Append ('-');
			FunctionPointerAlignment.Render (sb);
		}

		AppendFieldList (IntegerAlignment);
		AppendFieldList (FloatAlignment);
		AppendFieldList (VectorAlignment);

		Append ('P', ProgramAddressSpaceId);
		Append ('G', GlobalsAddressSpaceId);
		Append ('A', AllocaAddressSpaceId);

		if (AggregateObjectAlignment != null) {
			sb.Append ('-');
			AggregateObjectAlignment.Render (sb);
		}

		AppendList ("n", NativeIntegerWidths);
		AppendList ("ni", NonIntegralPointerTypeAddressSpaces);
		Append ('S', StackAlignment);

		sb.Append ('"');

		return sb.ToString ();

		void AppendFieldList<T> (List<T>? list) where T: LlvmIrDataLayoutField
		{
			if (list == null || list.Count == 0) {
				return;
			}

			foreach (LlvmIrDataLayoutField field in list) {
				sb.Append ('-');
				field.Render (sb);
			}
		}

		void AppendList (string id, List<uint>? list)
		{
			if (list == null || list.Count == 0) {
				return;
			}

			sb.Append ('-');
			sb.Append (id);

			bool first = true;
			foreach (uint v in list) {
				if (first) {
					first = false;
				} else {
					sb.Append (LlvmIrDataLayoutField.Separator);
				}

				sb.Append (LlvmIrDataLayoutField.ConvertToString (v));
			}
		}

		void Append (char id, uint? v)
		{
			if (!v.HasValue) {
				return;
			}

			sb.Append ('-');
			sb.Append (id);
			sb.Append (LlvmIrDataLayoutField.ConvertToString (v.Value));
		}
	}
}
