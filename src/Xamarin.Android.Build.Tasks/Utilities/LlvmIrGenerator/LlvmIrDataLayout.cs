using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Xamarin.Android.Tasks.LLVMIR;

abstract class LlvmIrDataLayoutField
{
	public const char Separator = ':';

	public string Id { get; }

	protected LlvmIrDataLayoutField (string id)
	{
		if (String.IsNullOrEmpty (id)) {
			throw new ArgumentException (nameof (id), "must not be null or empty");
		}

		Id = id;
	}

	public virtual void Render (StringBuilder sb)
	{
		sb.Append (Id);
	}

	public static string ConvertToString (uint v)
	{
		return v.ToString (CultureInfo.InvariantCulture);
	}

	protected void Append (StringBuilder sb, uint v, bool needSeparator = true)
	{
		if (needSeparator) {
			sb.Append (Separator);
		}

		sb.Append (ConvertToString (v));
	}

	protected void Append (StringBuilder sb, uint? v, bool needSeparator = true)
	{
		if (!v.HasValue) {
			return;
		}

		Append (sb, v.Value, needSeparator);
	}
}

class LlvmIrDataLayoutPointerSize : LlvmIrDataLayoutField
{
	public uint? AddressSpace { get; set; }
	public uint Abi           { get; }
	public uint Size          { get; }
	public uint? Pref         { get; set; }
	public uint? Idx          { get; set; }

	public LlvmIrDataLayoutPointerSize (uint size, uint abi)
		: base ("p")
	{
		Size = size;
		Abi = abi;
	}

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

abstract class LlvmIrDataLayoutTypeAlignment : LlvmIrDataLayoutField
{
	public uint Size  { get; }
	public uint Abi   { get; }
	public uint? Pref { get; set; }

	protected LlvmIrDataLayoutTypeAlignment (string id, uint size, uint abi)
		: base (id)
	{
		Size = size;
		Abi = abi;
	}

	public override void Render (StringBuilder sb)
	{
		base.Render (sb);

		Append (sb, Size, needSeparator: false);
		Append (sb, Abi);
		Append (sb, Pref);
	}
}

class LlvmIrDataLayoutIntegerAlignment : LlvmIrDataLayoutTypeAlignment
{
	public LlvmIrDataLayoutIntegerAlignment (uint size, uint abi, uint? pref = null)
		: base ("i", size, abi)
	{
		if (size == 8 && abi != 8) {
			throw new ArgumentOutOfRangeException (nameof (abi), "Must equal 8 for i8");
		}

		Pref = pref;
	}
}

class LlvmIrDataLayoutVectorAlignment : LlvmIrDataLayoutTypeAlignment
{
	public LlvmIrDataLayoutVectorAlignment (uint size, uint abi, uint? pref = null)
		: base ("v", size, abi)
	{
		Pref = pref;
	}
}

class LlvmIrDataLayoutFloatAlignment : LlvmIrDataLayoutTypeAlignment
{
	public LlvmIrDataLayoutFloatAlignment (uint size, uint abi, uint? pref = null)
		: base ("f", size, abi)
	{
		Pref = pref;
	}
}

class LlvmIrDataLayoutAggregateObjectAlignment : LlvmIrDataLayoutField
{
	public uint Abi   { get; }
	public uint? Pref { get; set; }

	public LlvmIrDataLayoutAggregateObjectAlignment (uint abi, uint? pref = null)
		: base ("a")
	{
		Abi = abi;
		Pref = pref;
	}

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
	public List<LlvmIrDataLayoutFloatAlignment>? FloatAlignment                     { get; set; }
	public LlvmIrDataLayoutFunctionPointerAlignment? FunctionPointerAlignment { get; set; }
	public List<LlvmIrDataLayoutIntegerAlignment>? IntegerAlignment                 { get; set; }
	public List<LlvmIrDataLayoutVectorAlignment>? VectorAlignment                   { get; set; }
	public List<LlvmIrDataLayoutPointerSize>? PointerSize                           { get; set; }

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
