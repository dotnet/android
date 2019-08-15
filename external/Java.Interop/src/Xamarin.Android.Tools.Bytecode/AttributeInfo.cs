using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode {

	public sealed class AttributeCollection : Collection<AttributeInfo> {

		public  ConstantPool    ConstantPool        {get; private set;}

		public AttributeCollection (ConstantPool constantPool, Stream stream)
		{
			ConstantPool    = constantPool;
			var count       = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				Add (AttributeInfo.CreateFromStream (constantPool, stream));
			}
		}

		public IEnumerable<AttributeInfo> GetInfos (string name)
		{
			return this.Where (a => a.Name == name);
		}

		public T Get<T> ()
			where T : AttributeInfo
		{
			return (T) GetInfos (AttributeInfo.GetAttributeName<T>()).SingleOrDefault ();
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7
	public class AttributeInfo {

		public  const   string  Code                    = "Code";
		public  const   string  ConstantValue           = "ConstantValue";
		public  const   string  Deprecated              = "Deprecated";
		public  const   string  Exceptions              = "Exceptions";
		public  const   string  EnclosingMethod         = "EnclosingMethod";
		public  const   string  InnerClasses            = "InnerClasses";
		public  const   string  LocalVariableTable      = "LocalVariableTable";
		public  const   string  MethodParameters        = "MethodParameters";
		public  const   string  Signature               = "Signature";
		public  const   string  SourceFile              = "SourceFile";
		public  const   string  StackMapTable           = "StackMapTable";

		ushort          nameIndex;

		public  ConstantPool    ConstantPool        {get; private set;}

		protected AttributeInfo (ConstantPool constantPool, ushort nameIndex, Stream stream)
		{
			if (constantPool == null)
				throw new ArgumentNullException ("constantPool");
			if (stream == null)
				throw new ArgumentNullException ("stream");

			ConstantPool    = constantPool;
			this.nameIndex  = nameIndex;
		}

		public string Name {
			get {return ((ConstantPoolUtf8Item) ConstantPool [nameIndex]).Value;}
		}

		static Dictionary<Type, string> AttributeNames = new Dictionary<Type, string> {
			{ typeof (CodeAttribute),                   Code },
			{ typeof (ConstantValueAttribute),          ConstantValue },
			{ typeof (DeprecatedAttribute),             Deprecated },
			{ typeof (EnclosingMethodAttribute),        EnclosingMethod },
			{ typeof (ExceptionsAttribute),             Exceptions },
			{ typeof (InnerClassesAttribute),           InnerClasses },
			{ typeof (LocalVariableTableAttribute),     LocalVariableTable },
			{ typeof (MethodParametersAttribute),       MethodParameters },
			{ typeof (SignatureAttribute),              Signature },
			{ typeof (SourceFileAttribute),             SourceFile },
			{ typeof (StackMapTableAttribute),          StackMapTable },
		};

		internal static string GetAttributeName<T>()
		{
			string value;
			if (AttributeNames.TryGetValue (typeof(T), out value)) {
				return value;
			}
			throw new InvalidOperationException ("No known name for type: " + typeof(T).Name);
		}

		public static AttributeInfo CreateFromStream (ConstantPool constantPool, Stream stream)
		{
			var nameIndex   = stream.ReadNetworkUInt16 ();
			var name        = ((ConstantPoolUtf8Item) constantPool [nameIndex]).Value;
			var attr        = CreateAttribute (name, constantPool, nameIndex, stream);
			return attr;
		}

		static AttributeInfo CreateAttribute (string name, ConstantPool constantPool, ushort nameIndex, Stream stream)
		{
			switch (name) {
			case Code:                  return new CodeAttribute (constantPool, nameIndex, stream);
			case ConstantValue:         return new ConstantValueAttribute (constantPool, nameIndex, stream);
			case Deprecated:            return new DeprecatedAttribute (constantPool, nameIndex, stream);
			case EnclosingMethod:       return new EnclosingMethodAttribute (constantPool, nameIndex, stream);
			case Exceptions:            return new ExceptionsAttribute (constantPool, nameIndex, stream);
			case InnerClasses:          return new InnerClassesAttribute (constantPool, nameIndex, stream);
			case LocalVariableTable:    return new LocalVariableTableAttribute (constantPool, nameIndex, stream);
			case MethodParameters:      return new MethodParametersAttribute (constantPool, nameIndex, stream);
			case Signature:             return new SignatureAttribute (constantPool, nameIndex, stream);
			case SourceFile:            return new SourceFileAttribute (constantPool, nameIndex, stream);
			case StackMapTable:         return new StackMapTableAttribute (constantPool, nameIndex, stream);
			default:                    return new UnknownAttribute (constantPool, nameIndex, stream);
			}
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.3
	public sealed class CodeAttribute : AttributeInfo {

		public byte[]       ByteCode;
		public ushort       MaxStack;
		public ushort       MaxLocals;

		public AttributeCollection  Attributes          {get; private set;}

		public Collection<CodeExceptionTableEntry>   ExceptionTable = new Collection<CodeExceptionTableEntry> ();

		public CodeAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length  = stream.ReadNetworkUInt32 ();
			MaxStack    = stream.ReadNetworkUInt16 ();
			MaxLocals   = stream.ReadNetworkUInt16 ();

			var code_length = stream.ReadNetworkUInt32 ();
			var code        = new byte[code_length];
			for (int i = 0; i < code.Length; ++i)
				code [i] = stream.ReadNetworkByte ();
			ByteCode = code;

			var exception_table_length  = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < exception_table_length; ++i) {
				var ex  = new CodeExceptionTableEntry (constantPool) {
					StartPC     = stream.ReadNetworkUInt16 (),
					EndPC       = stream.ReadNetworkUInt16 (),
					HandlerPC   = stream.ReadNetworkUInt16 (),
					catchType  = stream.ReadNetworkUInt16 (),
				};
				ExceptionTable.Add (ex);
			}

			Attributes  = new AttributeCollection (constantPool, stream);
		}

		public override string ToString ()
		{
			var sb = new StringBuilder ("Code(")
				.Append (ByteCode.Length);
			foreach (var attr in Attributes) {
				sb.Append (", ").Append (attr);
			}
			sb.Append (")");
			return sb.ToString ();
		}
	}

	public sealed class CodeExceptionTableEntry {
		public      ushort          StartPC;
		public      ushort          EndPC;
		public      ushort          HandlerPC;
		internal    ushort          catchType;

		public      ConstantPool    ConstantPool    {get; private set;}

		public CodeExceptionTableEntry (ConstantPool constantPool)
		{
			if (constantPool == null)
				throw new ArgumentNullException ("constantPool");

			ConstantPool    = constantPool;
		}

		public ConstantPoolClassItem    CatchType {
			get {return (ConstantPoolClassItem) ConstantPool [catchType];}
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.2
	public sealed class ConstantValueAttribute : AttributeInfo {

		ushort constantValueIndex;

		public ConstantValueAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length = stream.ReadNetworkUInt32 ();
			Debug.Assert (length == 2);
			constantValueIndex = stream.ReadNetworkUInt16 ();
		}

		public ConstantPoolItem Constant {
			get {return ConstantPool [constantValueIndex];}
		}

		public override string ToString ()
		{
			return string.Format ("ConstantValue({0})", Constant);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.15
	public sealed class DeprecatedAttribute : AttributeInfo {

		public DeprecatedAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length = stream.ReadNetworkUInt32 ();
			Debug.Assert (length == 0);
		}

		public override string ToString ()
		{
			return "Deprecated";
		}
	}

	// https://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.7
	public sealed class EnclosingMethodAttribute : AttributeInfo {

		ushort classIndex, methodIndex;

		public ConstantPoolClassItem            Class {
			get {return (ConstantPoolClassItem) ConstantPool [classIndex];}
		}

		public ConstantPoolNameAndTypeItem      Method {
			get {return methodIndex == 0 ? null : (ConstantPoolNameAndTypeItem) ConstantPool [methodIndex];}
		}

		public EnclosingMethodAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length      = stream.ReadNetworkUInt32 ();
			classIndex      = stream.ReadNetworkUInt16 ();
			methodIndex     = stream.ReadNetworkUInt16 ();
		}

		public override string ToString ()
		{
			return $"EnclosingMethod({Class}, {Method})";
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.5
	public sealed class ExceptionsAttribute : AttributeInfo {

		List<ushort>        exceptions  = new List<ushort> ();

		public ExceptionsAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length                  = stream.ReadNetworkUInt32 ();
			var number_of_exceptions    = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < number_of_exceptions; ++i) {
				exceptions.Add (stream.ReadNetworkUInt16 ());
			}
		}

		public IEnumerable<ConstantPoolClassItem> CheckedExceptions {
			get {return exceptions.Select (c => (ConstantPoolClassItem) ConstantPool [c]);}
		}

		public override string ToString ()
		{
			var sb = new StringBuilder ("Exceptions(");
			bool first = true;
			foreach (var e in CheckedExceptions) {
				if (!first)
					sb.Append (", ");
				first = false;
				sb.Append (e.Name.Value);
			}
			sb.Append (")");
			return sb.ToString ();
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.6
	public sealed class InnerClassesAttribute : AttributeInfo {

		public  Collection<InnerClassInfo>  Classes     = new Collection<InnerClassInfo> ();

		public InnerClassesAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length              = stream.ReadNetworkUInt32 ();
			var number_of_classes   = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < number_of_classes; ++i) {
				var info = new InnerClassInfo (ConstantPool) {
					innerClassInfoIndex     = stream.ReadNetworkUInt16 (),
					outerClassInfoIndex     = stream.ReadNetworkUInt16 (),
					innerNameIndex          = stream.ReadNetworkUInt16 (),
					InnerClassAccessFlags   = (ClassAccessFlags) stream.ReadNetworkUInt16 (),
				};
				Classes.Add (info);
			}
		}

		public override string ToString ()
		{
			return string.Format ("InnerClasses(Count={0}{1}{2})",
					Classes.Count,
					Classes.Count == 0 ? "" : ", ",
					string.Join (", ", Classes));
		}
	}

	public sealed class InnerClassInfo {

		internal    ushort              innerClassInfoIndex;
		internal    ushort              outerClassInfoIndex;
		internal    ushort              innerNameIndex;
		public      ClassAccessFlags    InnerClassAccessFlags;

		public      ConstantPool        ConstantPool            {get; private set;}

		public InnerClassInfo (ConstantPool constantPool)
		{
			if (constantPool == null)
				throw new ArgumentNullException ("constantPool");

			ConstantPool    = constantPool;
		}

		public ConstantPoolClassItem InnerClass {
			get {return (ConstantPoolClassItem) ConstantPool [innerClassInfoIndex];}
		}

		public ConstantPoolClassItem OuterClass {
			get {return (ConstantPoolClassItem) ConstantPool [outerClassInfoIndex];}
		}

		public string InnerName {
			get {
				if (innerNameIndex == 0)
					// anonymous class
					return null;
				return ((ConstantPoolUtf8Item) ConstantPool [innerNameIndex]).Value;
			}
		}

		public override string ToString ()
		{
			return string.Format ("InnerClass(InnerClass='{0}', OuterClass='{1}', InnerName='{2}', InnerClassAccessFlags={3})",
				InnerClass?.Name?.Value, OuterClass?.Name?.Value, InnerName, InnerClassAccessFlags);
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.13
	public sealed class LocalVariableTableAttribute : AttributeInfo {

		public  Collection<LocalVariableTableEntry> LocalVariables  = new Collection<LocalVariableTableEntry> ();

		public LocalVariableTableAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length                      = stream.ReadNetworkUInt32 ();
			var local_variable_table_length = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < local_variable_table_length; ++i) {
				var entry = new LocalVariableTableEntry (constantPool) {
					StartPC         = stream.ReadNetworkUInt16 (),
					Length          = stream.ReadNetworkUInt16 (),
					nameIndex       = stream.ReadNetworkUInt16 (),
					descriptorIndex = stream.ReadNetworkUInt16 (),
					Index           = stream.ReadNetworkUInt16 (),
				};
				LocalVariables.Add (entry);
			}
		}

		public override string ToString ()
		{
			if (LocalVariables.Count == 0)
				return "LocalVariableTableAttribute()";
			var sb = new StringBuilder ("LocalVariableTableAttribute(");
			sb.Append (LocalVariables [0]);
			for (int i = 1; i < LocalVariables.Count; ++i)
				sb.Append (", ").Append (LocalVariables [i]);
			sb.Append (")");
			return sb.ToString ();
		}
	}

	public sealed class LocalVariableTableEntry {
		public      ushort          StartPC;
		public      ushort          Length;
		internal    ushort          nameIndex;
		internal    ushort          descriptorIndex;
		public      ushort          Index;

		public      ConstantPool    ConstantPool    {get; private set;}

		public LocalVariableTableEntry (ConstantPool constantPool)
		{
			if (constantPool == null)
				throw new ArgumentNullException ("constantPool");

			ConstantPool    = constantPool;
		}

		public string Name {
			get {return ((ConstantPoolUtf8Item) ConstantPool [nameIndex]).Value;}
		}

		public string Descriptor {
			get {return ((ConstantPoolUtf8Item) ConstantPool [descriptorIndex]).Value;}
		}

		public override string ToString ()
		{
			return string.Format ("LocalVariableTableEntry(Name='{0}', Descriptor='{1}')", Name, Descriptor);
		}
	}

	public sealed class MethodParameterInfo {

		internal    ushort                          nameIndex;
		public      MethodParameterAccessFlags      AccessFlags;

		public      ConstantPool                    ConstantPool {get; private set;}

		public MethodParameterInfo (ConstantPool constantPool)
		{
			ConstantPool    = constantPool;
		}

		public string Name {
			get {
				if (nameIndex == 0)
					return null;
				return ((ConstantPoolUtf8Item) ConstantPool [nameIndex]).Value;
			}
		}

		public override string ToString ()
		{
			return string.Format ("MethodParameterInfo(Name='{0}', AccessFlags={1})", Name, AccessFlags);
		}
	}

	// https://docs.oracle.com/javase/specs/jvms/se8/html/jvms-4.html#jvms-4.7.24
	public sealed class MethodParametersAttribute : AttributeInfo {

		List<MethodParameterInfo>   parameters;

		public  IList<MethodParameterInfo>  ParameterInfo {
			get {return parameters;}
		}

		public MethodParametersAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length  = stream.ReadNetworkUInt32 ();
			var count   = stream.ReadNetworkByte ();
			Debug.Assert (
					length == (checked ((count * 4) + 1)),
					$"Unexpected `MethodParameters` length; expected {(count*4)+1}, got {length}!");
			parameters  = new List<MethodParameterInfo> (count);
			for (int i = 0; i < count; ++i) {
				var pNameIndex  = stream.ReadNetworkUInt16 ();
				var accessFlags = stream.ReadNetworkUInt16 ();
				var p           = new MethodParameterInfo (constantPool) {
					nameIndex   = pNameIndex,
					AccessFlags = (MethodParameterAccessFlags) accessFlags,
				};
				parameters.Add (p);
			}
		}

		public override string ToString ()
		{
			if (parameters.Count == 0)
				return "MethodParametersAttribute()";
			var sb = new StringBuilder ("MethodParametersAttribute(");
			sb.Append (parameters [0]);
			for (int i = 1; i < parameters.Count; ++i)
				sb.Append (", ").Append (parameters [i]);
			sb.Append (")");
			return sb.ToString ();
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.9
	public sealed class SignatureAttribute : AttributeInfo {

		ushort      signatureIndex;

		public SignatureAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length      = stream.ReadNetworkUInt32 ();
			Debug.Assert (length == 2);
			signatureIndex  = stream.ReadNetworkUInt16 ();
		}

		public string Value {
			get {return ((ConstantPoolUtf8Item) ConstantPool [signatureIndex]).Value;}
		}

		public override string ToString ()
		{
			return string.Format ("Signature({0})", Value);
		}
	}

	// https://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.10
	public sealed class SourceFileAttribute : AttributeInfo {

		ushort sourceFileIndex;

		public  string          FileName {
			get {return ((ConstantPoolUtf8Item) ConstantPool [sourceFileIndex]).Value;}
		}

		public SourceFileAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length      = stream.ReadNetworkUInt32 ();
			sourceFileIndex = stream.ReadNetworkUInt16 ();
		}

		public override string ToString ()
		{
			return $"SourceFile('{FileName}')";
		}
	}


	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.4
	public sealed class StackMapTableAttribute : AttributeInfo {

		public  byte[]  Data;

		public StackMapTableAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length      = stream.ReadNetworkUInt32 ();
			var data        = new byte[length];
			for (int i = 0; i < data.Length; ++i)
				data [i] = stream.ReadNetworkByte ();
			Data = data;
		}
	}

	public sealed class UnknownAttribute : AttributeInfo {

		public UnknownAttribute (ConstantPool constantPool, ushort nameIndex, Stream stream)
			: base (constantPool, nameIndex, stream)
		{
			var length      = stream.ReadNetworkUInt32 ();
			var data        = new byte[length];
			for (int i = 0; i < data.Length; ++i)
				data [i] = stream.ReadNetworkByte ();
			Data = data;
		}

		public readonly byte[] Data;

		public override string ToString ()
		{
			return string.Format ("Unknown[{0}]({1})", Name, Data.Length);
		}
	}
}
