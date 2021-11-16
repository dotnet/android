using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

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
		public  MethodAccessFlags   AccessFlags     {get; set;}
		public  AttributeCollection Attributes      {get; private set;}
		public  string?             KotlinReturnType {get; set;}

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

		public bool IsPubliclyVisible => AccessFlags.HasFlag (MethodAccessFlags.Public) || AccessFlags.HasFlag (MethodAccessFlags.Protected);

		public TypeInfo ReturnType {
			get {
				int endParams;
				GetParametersFromDescriptor (out endParams);
				endParams++;
				var r = new TypeInfo (Descriptor.Substring (endParams));
				r.TypeSignature = r.BinaryName;
				var s = GetSignature ();
				if (s != null)
					r.TypeSignature = s.ReturnTypeSignature;
				return r;
			}
		}

		bool    IsEnumCtor  => IsConstructor && DeclaringType.IsEnum;

		ParameterInfo[]? parameters = null;

		public ParameterInfo[] GetParameters ()
		{
			if (parameters != null)
				return parameters;
			int _;
			parameters      = GetParametersFromDescriptor (out _).ToArray ();
			UpdateParametersFromLocalVariables (parameters);
			UpdateParametersFromSignature (parameters);
			UpdateParametersFromMethodParametersAttribute (parameters);
			return parameters;
		}

		static IEnumerable<string> ExtractTypesFromSignature (string signature)
		{
			if (signature == null || signature.Length < "()V".Length)
				throw new InvalidOperationException (string.Format ("Invalid method descriptor '{0}'.", signature));
			if (signature [0] != '(')
				throw new InvalidOperationException (string.Format ("Invalid method descriptor '{0}'; expected '(' at index 0.", signature));

			int index   = 1;

			while (index < signature.Length && signature [index] != ')') {
				yield return Signature.ExtractType (signature, ref index);
			}
		}

		List<ParameterInfo> GetParametersFromDescriptor (out int endParams)
		{
			var signature   = Descriptor;
			if (signature == null || signature.Length < "()V".Length)
				throw new InvalidOperationException (string.Format ("Invalid method descriptor '{0}'.", signature));
			if (signature [0] != '(')
				throw new InvalidOperationException (string.Format ("Invalid method descriptor '{0}'; expected '(' at index 0.", signature));

			int index   = 1;

			int c       = 0;
			var first   = true;
			var ps      = new List<ParameterInfo> ();
			while (index < signature.Length && signature [index] != ')') {
				var type    = Signature.ExtractType (signature, ref index);

				if (first) {
					first   = false;
					if (IsConstructor &&
							!DeclaringType.IsStatic &&
							DeclaringType.TryGetEnclosingMethodInfo (out var declaringClass, out var _, out var _) &&
							type == "L" + declaringClass + ";") {
						continue;
					}
					if (IsConstructor &&
							!DeclaringType.IsStatic &&
							DeclaringType.InnerClass?.OuterClassName != null &&
							type == "L" + DeclaringType.InnerClass.OuterClassName + ";") {
						continue;
					}
				}

				if (first &&
						IsConstructor &&
						DeclaringType.InnerClass?.OuterClassName != null &&
						type == "L" + DeclaringType.InnerClass.OuterClassName + ";") {
					first   = false;
					continue;
				}

				var p       = new ParameterInfo ("p" + c, type, type, c);
				c++;
				ps.Add (p);
			}
			endParams = index;
			return ps;
		}

		void UpdateParametersFromLocalVariables (ParameterInfo[] parameters)
		{
			var locals      = GetLocalVariables ();
			if (locals == null)
				return;

			var names = locals.LocalVariables.Where (p => p.StartPC == 0).ToList ();

			int parametersCount = GetDeclaredParametersCount (parameters);

			for (int pi = parametersCount-1; pi >= 0; --pi) {
				for (int ni = names.Count-1; ni >= 0; --ni) {
					if (parameters [pi].Type.BinaryName != names [ni].Descriptor) {
						continue;
					}
					parameters [pi].Name = names [ni].Name;
					names.RemoveAt (ni);
					break;
				}
			}
		}

		LocalVariableTableAttribute? GetLocalVariables ()
		{
			var code    = Attributes.Get<CodeAttribute> ();
			if (code == null)
				return null;
			var locals = (LocalVariableTableAttribute?) code.Attributes.FirstOrDefault (a => a.Name == "LocalVariableTable");
			return locals;
		}

		int GetDeclaredParametersCount (ParameterInfo[] parameters)
		{
			// Consider the `MyStringList` inner class declared within `JavaType.staticActionWithGenerics()`.
			// The inner class acts as a closure over method parameters; *some* parameters are stored as
			// fields within the class, and provided as "trailing parameters" to the constructor.
			//
			// The problem is that I can't see a "clean" way to tell which parameters are "closure trailing parameters"
			// vs. "real" parameters.
			//
			// Thus, a hacky way: a "closure trailing parameter" is a type:
			//  1. Declared in the enclosing method, which is also
			//  2. The type of a field in the current declaring type, and
			//  3. The field name starts with `val$`.

			int parametersEnd   = parameters.Length;
			if (parametersEnd == 0 ||
					!IsConstructor ||
					!DeclaringType.TryGetEnclosingMethodInfo (out var declaringClass, out var declaringMethodName, out var declaringMethodDescriptor) ||
					declaringMethodDescriptor == null ||
					string.IsNullOrEmpty (declaringMethodDescriptor))
				return parametersEnd;

			var enclosingMethodTypes    = ExtractTypesFromSignature (declaringMethodDescriptor).ToList ();
			var closureFieldsTypes      = DeclaringType.Fields
				.Where (f => f.Name.StartsWith ("val$", StringComparison.Ordinal) &&
					f.AccessFlags.HasFlag (FieldAccessFlags.Final) &&
					f.AccessFlags.HasFlag (FieldAccessFlags.Synthetic))
				.Select (f => f.Descriptor)
				.ToList ();

			for (int i = closureFieldsTypes.Count; i > 0; --i) {
				if (!enclosingMethodTypes.Contains (closureFieldsTypes [i-1])) {
					closureFieldsTypes.RemoveAt (i-1);
				}
			}

			for (int i = closureFieldsTypes.Count; i > 0; --i) {
				if (parametersEnd == 0)
					break;
				if (parameters [parametersEnd-1].Type.BinaryName != closureFieldsTypes [i-1])
					break;
				parametersEnd--;
			}
			return parametersEnd;
		}

		void UpdateParametersFromSignature (ParameterInfo[] parameters)
		{
			var sig = GetSignature ();
			if (sig == null)
				return;

			int parametersCount = GetDeclaredParametersCount (parameters);
			CheckDescriptorVariablesToSignatureParameters (parameters, parametersCount, sig);
			int max = Math.Min (parametersCount,    sig.Parameters.Count);
			for (int i = 0; i < max; ++i) {
				parameters [i].Type.TypeSignature  = sig.Parameters [i];
			}
		}

		void CheckDescriptorVariablesToSignatureParameters (ParameterInfo[] parameters, int parametersCount, MethodTypeSignature sig)
		{
			if (IsEnumCtor)
				return;
			if (sig.Parameters.Count == parametersCount)
				return;

			Log.Debug ($"class-parse: method {DeclaringType.ThisClass.Name.Value}.{Name}.{Descriptor}: " +
					$"Signature ('{Attributes.Get<SignatureAttribute>()}') has {sig.Parameters.Count} entries; " +
					$"Descriptor '{Descriptor}' has {parametersCount} entries!");
		}

		public List<TypeInfo> GetThrows ()
		{
			var throws  = new List<TypeInfo> ();
			foreach (var exceptions in Attributes.Where (a => a.Name == "Exceptions")) {
				var ex = (ExceptionsAttribute) exceptions;
				foreach (var t in ex.CheckedExceptions) {
					throws.Add (new TypeInfo (t.Name.Value));
				}
			}
			var signature   = GetSignature ();
			if (signature == null)
				return throws;

			if (signature.Throws.Count > 0 && throws.Count != signature.Throws.Count) {
				Log.Warning (1, $"class-parse: warning: differing number of `throws` declarations on `{Name}{Descriptor}`!");
			}
			int c = Math.Min (signature.Throws.Count, throws.Count);
			for (int i = 0; i < c; ++i)
				throws [i].TypeSignature    = signature.Throws [i];
			return throws;
		}

		public MethodTypeSignature? GetSignature ()
		{
			var signature = (SignatureAttribute?) Attributes.SingleOrDefault (a => a.Name == "Signature");
			return signature != null
				? new MethodTypeSignature (signature.Value)
				: null;
		}

		void UpdateParametersFromMethodParametersAttribute (ParameterInfo[] parameters)
		{
			var methodParams = (MethodParametersAttribute?) Attributes.SingleOrDefault (a => a.Name == AttributeInfo.MethodParameters);
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
			int end = Math.Min (parameters.Length, pinfo.Count - startIndex);
			for (int i = 0; i < end; ++i) {
				var p = pinfo [i + startIndex];

				parameters [i].AccessFlags = p.AccessFlags;
				if (p.Name != null) {
					parameters [i].Name = p.Name;
				}
			}
		}

		public override string ToString () => Name;
	}

	public sealed class TypeInfo : IEquatable<TypeInfo> {

		// "raw" name, as used in method descriptors and type references
		// http://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.2.1
		public  string  BinaryName;

		// FieldTypeSignature, as extracted from the Signature attribute, which contains
		// generic type information.
		public  string? TypeSignature;

		public TypeInfo (string binaryName, string? typeSignature = null)
		{
			BinaryName      = binaryName;
			TypeSignature   = typeSignature;
		}

		public override int GetHashCode ()
		{
			return (BinaryName ?? "").GetHashCode () ^ (TypeSignature ?? "").GetHashCode ();
		}

		public override bool Equals (object? obj)
		{
			var o = obj as TypeInfo;
			if (o == null)
				return false;
			return Equals (o);
		}

		public bool Equals (TypeInfo? other)
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
		public  TypeInfo    Type;
		public  string?     KotlinType;

		public  MethodParameterAccessFlags      AccessFlags;

		public ParameterInfo (string name, string binaryName, string? typeSignature = null, int position = 0)
		{
			Name                = name;
			Position            = position;
			Type                = new TypeInfo (binaryName, typeSignature);
		}

		public override int GetHashCode ()
		{
			return (Name ?? "").GetHashCode () ^ Position.GetHashCode () ^
				(Type ?? new TypeInfo ("")).GetHashCode ();
		}

		public override bool Equals (object? obj)
		{
			var o = obj as ParameterInfo;
			if (o == null)
				return false;
			return Equals (o);
		}

		public bool Equals (ParameterInfo? other)
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

		// This is not a real Java MethodAccessFlags, it is used to denote Kotlin "internal" access.
		Internal        = 0x10000000,
	}

	[Flags]
	public enum MethodParameterAccessFlags {
		None,
		Final           = 0x0010,
		Synthetic       = 0x1000,
		Mandated        = 0x8000,
	}
}
