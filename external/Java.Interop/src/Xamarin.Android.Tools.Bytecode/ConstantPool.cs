using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode {

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4
	public class ConstantPool : Collection<ConstantPoolItem> {

		public ConstantPool (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");

			// indexes are one-based;
			// "The constant_pool table is indexed from 1 to constant_pool_count-1."
			Add (null);
			int constant_pool_count = stream.ReadNetworkUInt16 ();
			for (int i = 1; i < constant_pool_count; ++i) {
				var entry   = ConstantPoolItem.CreateFromStream (this, stream);
				Add (entry);
				if (entry.EntryRequiresTwoSlots) {
					++i;
					Add (entry);
				}
			}
		}
	}

	public enum ConstantPoolItemType {
		Utf8                = 1,
		Integer             = 3,
		Float               = 4,
		Long                = 5,
		Double              = 6,
		Class               = 7,
		String              = 8,
		Fieldref            = 9,
		Methodref           = 10,
		InterfaceMethodref  = 11,
		NameAndType         = 12,
		MethodHandle        = 15,
		MethodType          = 16,
		InvokeDynamic       = 18,
	}

	public abstract class ConstantPoolItem {

		public      abstract    ConstantPoolItemType   Type            {get;}

		internal    virtual     bool                    EntryRequiresTwoSlots {
			get {return false;}
		}

		public                  ConstantPool            ConstantPool    {get; private set;}

		public ConstantPoolItem (ConstantPool constantPool, Stream stream)
		{
			if (constantPool == null)
				throw new ArgumentNullException ("constantPool");
			if (stream == null)
				throw new ArgumentNullException ("stream");

			ConstantPool    = constantPool;
		}

		public static ConstantPoolItem CreateFromStream (ConstantPool constantPool, Stream stream)
		{
			var type    = (ConstantPoolItemType) stream.ReadNetworkByte ();
			switch (type) {
			case ConstantPoolItemType.Utf8:                return new ConstantPoolUtf8Item (constantPool, stream);
			case ConstantPoolItemType.Integer:             return new ConstantPoolIntegerItem (constantPool, stream);
			case ConstantPoolItemType.Float:               return new ConstantPoolFloatItem (constantPool, stream);
			case ConstantPoolItemType.Long:                return new ConstantPoolLongItem (constantPool, stream);
			case ConstantPoolItemType.Double:              return new ConstantPoolDoubleItem (constantPool, stream);
			case ConstantPoolItemType.Class:               return new ConstantPoolClassItem (constantPool, stream);
			case ConstantPoolItemType.String:              return new ConstantPoolStringItem (constantPool, stream);
			case ConstantPoolItemType.Fieldref:            return new ConstantPoolFieldrefItem (constantPool, stream);
			case ConstantPoolItemType.Methodref:           return new ConstantPoolMethodrefItem (constantPool, stream);
			case ConstantPoolItemType.InterfaceMethodref:  return new ConstantPoolInterfaceMethodrefItem (constantPool, stream);
			case ConstantPoolItemType.NameAndType:         return new ConstantPoolNameAndTypeItem (constantPool, stream);
			case ConstantPoolItemType.MethodHandle:        return new ConstantPoolMethodHandleItem (constantPool, stream);
			case ConstantPoolItemType.MethodType:          return new ConstantPoolMethodTypeItem (constantPool, stream);
			case ConstantPoolItemType.InvokeDynamic:       return new ConstantPoolInvokeDynamicItem (constantPool, stream);
			default:
				throw new NotSupportedException (string.Format ("Unknown constant type 0x{0}.", type.ToString ("x")));
			}
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.1
	public sealed class ConstantPoolClassItem : ConstantPoolItem {

		ushort nameIndex;

		public ConstantPoolClassItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			nameIndex   = stream.ReadNetworkUInt16 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Class;}
		}

		public ConstantPoolUtf8Item Name {
			get {return ((ConstantPoolUtf8Item) ConstantPool [nameIndex]);}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "Class(nameIndex={0} Name=\"{1}\")", nameIndex, Name.Value);
		}
	}

	public abstract class ConstantPoolMemberrefItem : ConstantPoolItem {

		internal    ushort  classIndex;
		internal    ushort  nameAndTypeIndex;

		public ConstantPoolMemberrefItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			classIndex          = stream.ReadNetworkUInt16 ();
			nameAndTypeIndex    = stream.ReadNetworkUInt16 ();
		}

		public ConstantPoolClassItem Class {
			get {return (ConstantPoolClassItem) ConstantPool [classIndex];}
		}

		public ConstantPoolNameAndTypeItem NameAndType {
			get {return (ConstantPoolNameAndTypeItem) ConstantPool [nameAndTypeIndex];}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "{0}(classIndex={1} nameAndTypeIndex={2} Class='{3}' Name='{4}' Descriptor='{5}')",
					Type, classIndex, nameAndTypeIndex, Class.Name, NameAndType.Name.Value, NameAndType.Descriptor.Value);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.2
	public sealed class ConstantPoolFieldrefItem : ConstantPoolMemberrefItem {

		public ConstantPoolFieldrefItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Fieldref;}
		}
	}

	public sealed class ConstantPoolMethodrefItem : ConstantPoolMemberrefItem {

		public ConstantPoolMethodrefItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Methodref;}
		}
	}

	public sealed class ConstantPoolInterfaceMethodrefItem : ConstantPoolMemberrefItem {

		public ConstantPoolInterfaceMethodrefItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.InterfaceMethodref;}
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.3
	public sealed class ConstantPoolStringItem : ConstantPoolItem {

		ushort  stringIndex;

		public ConstantPoolStringItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			stringIndex = stream.ReadNetworkUInt16 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.String;}
		}

		public ConstantPoolUtf8Item StringData {
			get {return (ConstantPoolUtf8Item) ConstantPool [stringIndex];}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "String(stringIndex={0} Utf8=\"{1}\")",
					stringIndex, StringData.Value);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.4
	public sealed class ConstantPoolIntegerItem : ConstantPoolItem {

		int value;

		public ConstantPoolIntegerItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			value   = (int) stream.ReadNetworkUInt32 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Integer;}
		}

		public int Value {
			get {return value;}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "Integer({0})", Value);
		}
	}

	public sealed class ConstantPoolFloatItem : ConstantPoolItem {

		float value;

		public ConstantPoolFloatItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			var data = new byte[]{
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
			};
			if (BitConverter.IsLittleEndian)
				Array.Reverse (data);
			value   = BitConverter.ToSingle (data, 0);
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Float;}
		}

		public float Value {
			get {return value;}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "Float({0})", Value);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.5
	public sealed class ConstantPoolLongItem : ConstantPoolItem {

		long value;

		public ConstantPoolLongItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			value   = stream.ReadNetworkInt64 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Long;}
		}

		internal override bool EntryRequiresTwoSlots {
			get {return true;}
		}

		public long Value {
			get {return value;}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "Long({0})", Value);
		}
	}

	public sealed class ConstantPoolDoubleItem : ConstantPoolItem {

		double value;

		public ConstantPoolDoubleItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			var data = new byte[]{
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
				stream.ReadNetworkByte (),
			};
			if (BitConverter.IsLittleEndian)
				Array.Reverse (data);
			value   = BitConverter.ToDouble (data, 0);
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Double;}
		}

		internal override bool EntryRequiresTwoSlots {
			get {return true;}
		}

		public double Value {
			get {return value;}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "Double({0})", Value);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.6
	public sealed class ConstantPoolNameAndTypeItem : ConstantPoolItem {

		ushort  nameIndex;
		ushort  descriptorIndex;

		public ConstantPoolNameAndTypeItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			nameIndex       = stream.ReadNetworkUInt16 ();
			descriptorIndex = stream.ReadNetworkUInt16 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.NameAndType;}
		}

		public ConstantPoolUtf8Item Name {
			get {return (ConstantPoolUtf8Item) ConstantPool [nameIndex];}
		}

		public ConstantPoolUtf8Item Descriptor {
			get {return (ConstantPoolUtf8Item) ConstantPool [descriptorIndex];}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "NameAndType(nameIndex={0} descriptorIndex={1} Name=\"{2}\" Descriptor=\"{3}\")",
					nameIndex, descriptorIndex, Name.Value, Descriptor.Value);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.7
	public sealed class ConstantPoolUtf8Item : ConstantPoolItem {

		string value;

		public ConstantPoolUtf8Item (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			var length  = stream.ReadNetworkUInt16 ();
			var data    = new byte [length];
			for (int i = 0; i < data.Length; ++i)
				data [i] = stream.ReadNetworkByte ();

			// The .class file specially encodes NUL so that it takes 2 bytes, not 1.
			// http://en.wikipedia.org/wiki/UTF-8#Modified_UTF-8
			var fixup   = new List<byte> (data.Length);
			for (int i = 0; i < data.Length; ++i) {
				if (data [i] == 0xc0 && (i + 1) < data.Length && data [i + 1] == 0x80) {
					fixup.Add (0x00);
					i++;
					continue;
				}
				// ...and they couldn't be bothered with supporting 4-byte UTF-8 sequences,
				// needed for Emoji and chars off the Basic Multilingual Plane; instead, they're
				// encoded as a surrogate pair. (What is this I don't even...)
				if (data [i] == 0xed && i+6 < data.Length && data [i+3] == 0xed) {
					var surrogatePair = new char [] {
						(char) (0xD800 + (((data [i+1] & 0x0F) << 6) | (data [i+2] & 0x3F))),
						(char) (0xDC00 + (((data [i+4] & 0x0F) << 6) | (data [i+5] & 0x3F))),
					};
					fixup.AddRange (Encoding.UTF8.GetBytes (surrogatePair));
					i += 5;
					continue;

				}
				fixup.Add (data [i]);
			}
			value   = Encoding.UTF8.GetString (fixup.Count == data.Length ? data : fixup.ToArray ());
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.Utf8;}
		}

		public string Value {
			get {return value;}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "Utf8(\"{0}\")", Value);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.8
	public sealed class ConstantPoolMethodHandleItem : ConstantPoolItem {

		byte    referenceKind;
		ushort  referenceIndex;

		public ConstantPoolMethodHandleItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			referenceKind   = stream.ReadNetworkByte ();
			referenceIndex  = stream.ReadNetworkUInt16 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.MethodHandle;}
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.9
	public sealed class ConstantPoolMethodTypeItem : ConstantPoolItem {

		ushort  descriptorIndex;

		public ConstantPoolMethodTypeItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			descriptorIndex = stream.ReadNetworkUInt16 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.MethodType;}
		}

		public ConstantPoolUtf8Item Descriptor {
			get {return (ConstantPoolUtf8Item) ConstantPool [descriptorIndex];}
		}

		public override string ToString ()
		{
			return string.Format (CultureInfo.InvariantCulture, "MethodType(descriptorIndex={0} Descriptor=\"{1}\")",
					descriptorIndex, Descriptor.Value);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.4.10
	public sealed class ConstantPoolInvokeDynamicItem : ConstantPoolItem {

		ushort  boostrapMethodAttrIndex;
		ushort  nameAndTypeIndex;

		public ConstantPoolInvokeDynamicItem (ConstantPool constantPool, Stream stream)
			: base (constantPool, stream)
		{
			boostrapMethodAttrIndex = stream.ReadNetworkUInt16 ();
			nameAndTypeIndex        = stream.ReadNetworkUInt16 ();
		}

		public override ConstantPoolItemType Type {
			get {return ConstantPoolItemType.InvokeDynamic;}
		}
	}
}

