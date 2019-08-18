using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools.Bytecode {

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.1
	public sealed class ClassFile {

		public ushort               MinorVersion;
		public ushort               MajorVersion;
		public ConstantPool         ConstantPool;
		public ClassAccessFlags     AccessFlags;

		ushort          thisClass;
		ushort          superClass;

		public Interfaces           Interfaces;
		public Fields               Fields;
		public Methods              Methods;
		public AttributeCollection  Attributes;

		ClassSignature              signature;


		public ClassFile (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");

			uint magic;
			if (!IsClassFile (stream, out magic))
				throw new BadImageFormatException ("Stream doesn't start with valid 0xCAFEBABE header! Found: 0x" + magic.ToString ("x"));

			MinorVersion    = stream.ReadNetworkUInt16 ();
			MajorVersion    = stream.ReadNetworkUInt16 ();
			ConstantPool    = new ConstantPool (stream);
			AccessFlags     = (ClassAccessFlags)stream.ReadNetworkUInt16 ();
			thisClass       = stream.ReadNetworkUInt16 ();
			superClass      = stream.ReadNetworkUInt16 ();
			Interfaces      = new Interfaces (ConstantPool, stream);
			Fields          = new Fields (ConstantPool, stream);
			Methods         = new Methods (ConstantPool, this, stream);
			Attributes      = new AttributeCollection (ConstantPool, stream);

			int e = stream.ReadByte ();
			if (e >= 0)
				throw new BadImageFormatException ("Stream has trailing data?!");
		}

		public static bool IsClassFile (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");
			try {
				var magic = stream.ReadNetworkUInt32 ();
				if (magic != unchecked ((uint) 0xcafebabe))
					return false;
				return true;
			} catch  (BadImageFormatException) {
				return false;
			}
		}

		static bool IsClassFile (Stream stream, out uint magic)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");
			try {
				magic = stream.ReadNetworkUInt32 ();
				if (magic != unchecked ((uint) 0xcafebabe))
					return false;
				return true;
			} catch (BadImageFormatException) {
				magic = 0;
				return false;
			}
		}

		public ConstantPoolClassItem ThisClass {
			get {return (ConstantPoolClassItem) ConstantPool [thisClass];}
		}

		public ConstantPoolClassItem SuperClass {
			get {return (ConstantPoolClassItem) ConstantPool [superClass];}
		}

		public string PackageName {
			get {
				var name    = ThisClass.Name.Value;
				var slash   = name.LastIndexOf ('/');
				if (slash < 0)
					return "";
				return name.Substring (0, slash).Replace ('/', '.');
			}
		}

		public string SourceFileName {
			get {
				var sourceFile  = Attributes.Get<SourceFileAttribute> ();
				return sourceFile == null ? null : sourceFile.FileName;
			}
		}

		public bool TryGetEnclosingMethodInfo (out string declaringClass, out string declaringMethod, out string declaringDescriptor)
		{
			declaringClass = declaringMethod = declaringDescriptor = null;

			var enclosingMethod     = Attributes.Get<EnclosingMethodAttribute> ();
			if (enclosingMethod == null) {
				return false;
			}

			declaringClass          = enclosingMethod.Class.Name.Value;
			declaringMethod         = enclosingMethod.Method?.Name.Value;
			declaringDescriptor     = enclosingMethod.Method?.Descriptor.Value;
			return true;
		}

		public ClassSignature GetSignature ()
		{
			if (this.signature != null)
				return this.signature;
			var sig = Attributes.Get<SignatureAttribute> ();
			return sig != null
				? (this.signature = new ClassSignature (sig.Value))
				: null;
		}

		public List<TypeInfo> GetInterfaces ()
		{
			var interfaces  = new List<TypeInfo> (Interfaces.Count);
			for (int i = 0; i < Interfaces.Count; ++i)
				interfaces.Add (new TypeInfo (Interfaces [i].Name.Value));

			var sig = GetSignature ();
			if (sig == null)
				return interfaces;

			if (interfaces.Count != sig.SuperinterfaceSignatures.Count)
				Console.Error.WriteLine ("class-parse: warning: Interfaces count ({0}) differs from Signature's Superinterfaces count ({1})!",
						Interfaces.Count, sig.SuperinterfaceSignatures.Count);
			int c = Math.Min (interfaces.Count, sig.SuperinterfaceSignatures.Count);
			for (int i = 0; i < c; ++i)
				interfaces [i].TypeSignature    = sig.SuperinterfaceSignatures [i];

			return interfaces;
		}

		public IList<InnerClassInfo> InnerClasses {
			get {
				var inner   = Attributes.Get<InnerClassesAttribute> ();
				if (inner == null)
					return new InnerClassInfo [0];
				return inner.Classes;
			}
		}

		public InnerClassInfo InnerClass {
			get {
				return InnerClasses.SingleOrDefault (c => c.InnerClass == ThisClass);
			}
		}

		public ClassAccessFlags Visibility {
			get {
				var inner = InnerClass;
				if (inner == null)
					return AccessFlags;
				return inner.InnerClassAccessFlags;
			}
		}

		public bool IsStatic {
			get {
				var inner = InnerClass;
				if (inner == null)
					return false;
				return (inner.InnerClassAccessFlags & ClassAccessFlags.Static) != 0;
			}
		}

		public bool IsEnum {
			get {return (AccessFlags & ClassAccessFlags.Enum) != 0;}
		}
	}

	[Flags]
	public enum ClassAccessFlags {
		Public      = 0x0001,

		// Begin --only valid on inner types
		Private     = 0x0002,
		Protected   = 0x0004,
		Static      = 0x0008,
		// End   --only valid on inner types

		Final       = 0x0010,
		Super       = 0x0020,
		Interface   = 0x0200,
		Abstract    = 0x0400,
		Synthetic   = 0x1000,
		Annotation  = 0x2000,
		Enum        = 0x4000,
	}
}

