using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools.Bytecode {

	public sealed class Methods : Collection<MethodInfo> {

		public  ConstantPool    ConstantPool        {get; private set;}

		public Methods (ConstantPool constantPool, ClassFile declaringClass, Stream stream)
		{
			ConstantPool    = constantPool;
			var count       = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				Add (new MethodInfo (constantPool, declaringClass, stream));
			}
		}
	}

	// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.6
	public sealed class MethodInfo {

		ushort              nameIndex;
		ushort              descriptorIndex;

		public  ConstantPool        ConstantPool    {get; private set;}
		public  ClassFile           DeclaringType   {get; private set;}
		public  MethodAccessFlags   AccessFlags     {get; private set;}
		public  AttributeCollection Attributes      {get; private set;}

		public MethodInfo (ConstantPool constantPool, ClassFile declaringType, Stream stream)
		{
			ConstantPool    = constantPool;
			DeclaringType   = declaringType;
			AccessFlags     = (MethodAccessFlags) stream.ReadNetworkUInt16 ();
			nameIndex       = stream.ReadNetworkUInt16 ();
			descriptorIndex = stream.ReadNetworkUInt16 ();
			Attributes      = new AttributeCollection (constantPool, stream);
		}

		public string Name {
			get {
				return ((ConstantPoolUtf8Item) ConstantPool [nameIndex]).Value;
			}
		}

		public string Descriptor {
			get {
				return ((ConstantPoolUtf8Item) ConstantPool [descriptorIndex]).Value;
			}
		}

		public bool IsConstructor {
			get {return Name == "<init>";}
		}

		public TypeInfo ReturnType {
			get {
				int endParams;
				GetParametersFromDescriptor (out endParams);
				endParams++;
				var r = new TypeInfo {
					BinaryName = Descriptor.Substring (endParams),
				};
				r.TypeSignature = r.BinaryName;
				var s = GetSignature ();
				if (s != null)
					r.TypeSignature = s.ReturnTypeSignature;
				return r;
			}
		}

		ParameterInfo[] parameters = null;

		public ParameterInfo[] GetParameters ()
		{
			if (parameters != null)
				return parameters;
			int _;
			parameters      = GetParametersFromDescriptor (out _).ToArray ();
			var locals      = GetLocalVariables ();
			var enumCtor    = IsConstructor && DeclaringType.IsEnum;
			if (locals != null) {
				var names = locals.LocalVariables.Where (p => p.StartPC == 0).ToList ();
				int start = 0;
				if ((AccessFlags & MethodAccessFlags.Static) == 0)
					start++;    // skip `this` parameter
				if (!DeclaringType.IsStatic &&
						names.Count > start &&
						(parameters.Length == 0 || parameters [0].Type.BinaryName != names [start].Descriptor)) {
					start++;    // JDK 8?
				}
				if (((AccessFlags & MethodAccessFlags.Synthetic) != MethodAccessFlags.Synthetic) &&
						((names.Count - start) != parameters.Length) &&
						!enumCtor) {
					Log.Warning (1,"class-parse: warning: method {0}.{1}{2}: " +
							"Local variables array has {3} entries ('{4}'); descriptor has {5} entries!",
							DeclaringType.ThisClass.Name.Value, Name, Descriptor,
							names.Count - start,
							Attributes.Get<CodeAttribute>().Attributes.Get<LocalVariableTableAttribute> (),
							parameters.Length);
				}
				int max = Math.Min (parameters.Length,  names.Count - start);
				for (int i = 0; i < max; ++i) {
					parameters [i].Name = names [start+i].Name;
					if (parameters [i].Type.BinaryName != names [start + i].Descriptor) {
						Log.Warning (1, "class-parse: warning: method {0}.{1}{2}: " +
								"Local variable type descriptor mismatch! Got '{3}'; expected '{4}'.",
								DeclaringType.ThisClass.Name.Value, Name, Descriptor,
								parameters [i].Type.BinaryName,
								names [start + i].Descriptor);
					}
				}
			}
			var sig = GetSignature ();
			if (sig != null) {
				if ((sig.Parameters.Count != parameters.Length) && !enumCtor) {
					Log.Warning (1,"class-parse: warning: method {0}.{1}{2}: " +
							"Signature ('{3}') has {4} entries; Descriptor '{5}' has {6} entries!",
							DeclaringType.ThisClass.Name.Value, Name, Descriptor,
							Attributes.Get<SignatureAttribute>(),
							sig.Parameters.Count,
							Descriptor,
							parameters.Length);
				}
				int max = Math.Min (parameters.Length,  sig.Parameters.Count);
				for (int i = 0; i < max; ++i) {
					parameters [i].Type.TypeSignature  = sig.Parameters [i];
				}
			}
			UpdateParametersFromMethodParametersAttribute (parameters);
			return parameters;
		}

		LocalVariableTableAttribute GetLocalVariables ()
		{
			var code    = Attributes.Get<CodeAttribute> ();
			if (code == null)
				return null;
			var locals = (LocalVariableTableAttribute) code.Attributes.FirstOrDefault (a => a.Name == "LocalVariableTable");
			return locals;
		}

		List<ParameterInfo> GetParametersFromDescriptor (out int endParams)
		{
			var signature   = Descriptor;
			if (signature == null || signature.Length < "()V".Length)
				throw new InvalidOperationException (string.Format ("Invalid method descriptor '{0}'.", signature));
			if (signature [0] != '(')
				throw new InvalidOperationException (string.Format ("Invalid method descriptor '{0}'; expected '(' at index 0.", signature));

			int index   = 1;

			// non-static inner classes have a "hidden" parameter. Skip it.
			if (IsConstructor && DeclaringType.InnerClass != null && !DeclaringType.IsStatic && signature [index] != ')')
				Signature.ExtractType (signature, ref index);

			int c       = 0;
			var ps      = new List<ParameterInfo> ();
			while (index < signature.Length && signature [index] != ')') {
				var type    = Signature.ExtractType (signature, ref index);
				var p       = new ParameterInfo {
					Position    = c,
					Name        = "p" + (c++),
				};
				p.Type.BinaryName       = type;
				p.Type.TypeSignature    = type;
				ps.Add (p);
			}
			endParams = index;
			return ps;
		}

		public List<TypeInfo> GetThrows ()
		{
			var throws  = new List<TypeInfo> ();
			foreach (var exceptions in Attributes.Where (a => a.Name == "Exceptions")) {
				var ex = (ExceptionsAttribute) exceptions;
				foreach (var t in ex.Exceptions) {
					throws.Add (new TypeInfo (t.Name.Value));
				}
			}
			var signature   = GetSignature ();
			if (signature == null)
				return throws;

			if (signature.Throws.Count > 0 && throws.Count != signature.Throws.Count) {
				Log.Warning (1, "class-parse: warning: differing number of `throws` declarations on `{0}{1}`!",
						Name, Descriptor);
			}
			int c = Math.Min (signature.Throws.Count, throws.Count);
			for (int i = 0; i < c; ++i)
				throws [i].TypeSignature    = signature.Throws [i];
			return throws;
		}

		public MethodTypeSignature GetSignature ()
		{
			var signature = (SignatureAttribute) Attributes.SingleOrDefault (a => a.Name == "Signature");
			return signature != null
				? new MethodTypeSignature (signature.Signature)
				: null;
		}

		void UpdateParametersFromMethodParametersAttribute (ParameterInfo[] parameters)
		{
			var methodParams = (MethodParametersAttribute) Attributes.SingleOrDefault (a => a.Name == AttributeInfo.MethodParameters);
			if (methodParams == null)
				return;

			const MethodParameterAccessFlags OuterThis =
				MethodParameterAccessFlags.Mandated | MethodParameterAccessFlags.Final;
			var pinfo = methodParams.ParameterInfo;
			int startIndex = 0;
			while (startIndex < pinfo.Count &&
				   (pinfo [startIndex].AccessFlags & OuterThis) == OuterThis) {
				startIndex++;
			}
			Debug.Assert (
					parameters.Length == pinfo.Count - startIndex,
					$"Unexpected number of method parameters; expected {parameters.Length}, got {pinfo.Count - startIndex}");
			for (int i = 0; i < parameters.Length; ++i) {
				var p = pinfo [i + startIndex];

				parameters [i].AccessFlags = p.AccessFlags;
				if (p != null) {
					parameters [i].Name = p.Name;
				}
			}
		}
	}

	public sealed class TypeInfo : IEquatable<TypeInfo> {

		// "raw" name, as used in method descriptors and type references
		// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.2.1
		public  string  BinaryName;

		// FieldTypeSignature, as extracted from the Signature attribute, which contains
		// generic type information.
		public  string  TypeSignature;

		public TypeInfo (string binaryName = null, string typeSignature = null)
		{
			BinaryName      = binaryName;
			TypeSignature   = typeSignature;
		}

		public override int GetHashCode ()
		{
			return (BinaryName ?? "").GetHashCode () ^ (TypeSignature ?? "").GetHashCode ();
		}

		public override bool Equals (object obj)
		{
			var o = obj as TypeInfo;
			if (o == null)
				return false;
			return Equals (o);
		}

		public bool Equals (TypeInfo other)
		{
			if (other == null)
				return false;
			if (object.ReferenceEquals (this, other))
				return true;
			return object.Equals (BinaryName, other.BinaryName) &&
				object.Equals (TypeSignature, other.TypeSignature);
		}

		public override string ToString ()
		{
			if (TypeSignature != null)
				return string.Format ("TypeInfo({0}={1})", BinaryName, TypeSignature);
			return string.Format ("TypeInfo({0})", BinaryName);
		}
	}

	public sealed class ParameterInfo : IEquatable<ParameterInfo> {

		public  string      Name;
		public  int         Position;
		public  TypeInfo    Type    = new TypeInfo ();

		public  MethodParameterAccessFlags      AccessFlags;

		public ParameterInfo (string name = null, string binaryName = null, string typeSignature = null, int position = 0)
		{
			Name                = name;
			Type.BinaryName     = binaryName;
			Type.TypeSignature  = typeSignature;
			Position            = position;
		}

		public override int GetHashCode ()
		{
			return (Name ?? "").GetHashCode () ^ Position.GetHashCode () ^
				(Type ?? new TypeInfo ()).GetHashCode ();
		}

		public override bool Equals (object obj)
		{
			var o = obj as ParameterInfo;
			if (o == null)
				return false;
			return Equals (o);
		}

		public bool Equals (ParameterInfo other)
		{
			if (other == null)
				return false;
			if (object.ReferenceEquals (this, other))
				return true;
			return object.Equals (Name, other.Name) &&
				Position == other.Position &&
				object.Equals (Type, other.Type);
		}

		public override string ToString ()
		{
			return $"ParameterInfo(Name={Name}, Position={Position}, Type={Type}, AccessFlags={AccessFlags})";
		}
	}

	[Flags]
	public enum MethodAccessFlags {
		Public          = 0x0001,
		Private         = 0x0002,
		Protected       = 0x0004,
		Static          = 0x0008,
		Final           = 0x0010,
		Synchronized    = 0x0020,
		Bridge          = 0x0040,
		Varargs         = 0x0080,
		Native          = 0x0100,
		Abstract        = 0x0400,
		Strict          = 0x0800,
		Synthetic       = 0x1000,
	}

	[Flags]
	public enum MethodParameterAccessFlags {
		None,
		Final           = 0x0010,
		Synthetic       = 0x1000,
		Mandated        = 0x8000,
	}
}
