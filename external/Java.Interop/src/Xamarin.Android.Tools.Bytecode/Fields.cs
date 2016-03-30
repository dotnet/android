using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools.Bytecode {

	public sealed class Fields : Collection<FieldInfo> {

		public  ConstantPool    ConstantPool        {get; private set;}

		public Fields (ConstantPool constantPool, Stream stream)
		{
			if (constantPool == null)
				throw new ArgumentNullException ("constantPool");
			if (stream == null)
				throw new ArgumentNullException ("stream");

			ConstantPool    = constantPool;
			var count       = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				Add (new FieldInfo (constantPool, stream));
			}
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.5
	public sealed class FieldInfo {
		ushort              nameIndex;
		ushort              descriptorIndex;

		public  ConstantPool        ConstantPool    {get; private set;}
		public  FieldAccessFlags    AccessFlags     {get; private set;}
		public  AttributeCollection Attributes      {get; private set;}

		public FieldInfo (ConstantPool constantPool, Stream stream)
		{
			ConstantPool    = constantPool;
			AccessFlags     = (FieldAccessFlags) stream.ReadNetworkUInt16 ();
			nameIndex       = stream.ReadNetworkUInt16 ();
			descriptorIndex = stream.ReadNetworkUInt16 ();
			Attributes      = new AttributeCollection (constantPool, stream);
		}

		public  string Name {
			get {
				return ((ConstantPoolUtf8Item) ConstantPool [nameIndex]).Value;
			}
		}

		public  string Descriptor {
			get {
				return ((ConstantPoolUtf8Item) ConstantPool [descriptorIndex]).Value;
			}
		}

		public string GetSignature ()
		{
			var signature   = Attributes.Get<SignatureAttribute> ();
			return signature != null ? signature.Signature : null;
		}
	}

	[Flags]
	public enum FieldAccessFlags {
		Public      = 0x0001,
		Private     = 0x0002,
		Protected   = 0x0004,
		Static      = 0x0008,
		Final       = 0x0010,
		Volatile    = 0x0040,
		Transient   = 0x0080,
		Synthetic   = 0x1000,
		Enum        = 0x4000,
	}
}
