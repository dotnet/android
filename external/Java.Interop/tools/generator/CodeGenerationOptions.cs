using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Java.Interop.Tools.Generator;
using Java.Interop.Tools.JavaCallableWrappers;

using Xamarin.Android.Binder;
using Xamarin.AndroidTools.AnnotationSupport;

namespace MonoDroid.Generation
{
    public class CodeGenerationOptions
	{
		CodeGenerationTarget    codeGenerationTarget;
		public      CodeGenerationTarget    CodeGenerationTarget    {
			get { return codeGenerationTarget; }
			set {
				switch (value) {
				case CodeGenerationTarget.XamarinAndroid:
				case CodeGenerationTarget.XAJavaInterop1:
				case CodeGenerationTarget.JavaInterop1:
					codeGenerationTarget    = value;
					break;
				default:
					throw new NotSupportedException ("Don't know what to do for target '" + value + "'.");
				}
			}
		}

		internal CodeGenerator CreateCodeGenerator (TextWriter writer)
		{
			switch (codeGenerationTarget) {
				case CodeGenerationTarget.JavaInterop1:
					return new JavaInteropCodeGenerator (writer, this);
				case CodeGenerationTarget.XAJavaInterop1:
					return new XAJavaInteropCodeGenerator (writer, this);
				case CodeGenerationTarget.XamarinAndroid:
				default:
					return new XamarinAndroidCodeGenerator (writer, this);
			}
		}

		SymbolTable symbolTable;
		public      SymbolTable             SymbolTable             {
			get {
				if (symbolTable != null)
					return symbolTable;
				return symbolTable = new SymbolTable (CodeGenerationTarget);
			}
		}

		readonly SortedSet<string> jni_marshal_delegates = new SortedSet<string> ();
		readonly object jni_marshal_delegates_lock = new object ();

		public string ApiXmlFile { get; set; }
		public bool UseGlobal { get; set; }
		public bool IgnoreNonPublicType { get; set; }
		public string AssemblyName { get; set; }
		public bool UseShortFileNames { get; set; }
		public int ProductVersion { get; set; }
		public bool SupportInterfaceConstants { get; set; }
		public bool SupportDefaultInterfaceMethods { get; set; }
		public bool SupportNestedInterfaceTypes { get; set; }
		public bool SupportNullableReferenceTypes { get; set; }
		public bool UseShallowReferencedTypes { get; set; }
		public bool RemoveConstSugar => BuildingCoreAssembly;

		bool? buildingCoreAssembly;
		// Basically this means "Are we building Mono.Android.dll?"
		public bool BuildingCoreAssembly {
			get {
				return buildingCoreAssembly ?? (buildingCoreAssembly = (SymbolTable.Lookup ("java.lang.Object") is ClassGen gen && gen.FromXml)).Value;
			}
		}

		public string NullableOperator => SupportNullableReferenceTypes ? "?" : string.Empty;

		public string NullForgivingOperator => SupportNullableReferenceTypes ? "!" : string.Empty;

		public List<NamespaceTransform> NamespaceTransforms { get; } = new List<NamespaceTransform> ();

		public string GetTypeReferenceName (Field field)
		{
			var name = GetOutputName (field.Symbol.FullName);

			if (field.NotNull || field.Symbol.IsEnum)
				return name;

			return name + GetNullable (field.Symbol.FullName);
		}

		public string GetTypeReferenceName (Parameter symbol)
		{
			var name = GetOutputName (symbol.Type);

			if (symbol.NotNull || symbol.Symbol.IsEnum)
				return name;

			return name + GetNullable (symbol.Type);
		}

		public string GetTypeReferenceName (ReturnValue symbol)
		{
			var name = GetOutputName (symbol.FullName);

			if (symbol.NotNull || symbol.Symbol.IsEnum)
				return name;

			return name + GetNullable (symbol.FullName);
		}

		public string GetTypeReferenceName (Property symbol)
		{
			if (symbol.Getter != null)
				return GetTypeReferenceName (symbol.Getter.RetVal);

			return GetTypeReferenceName (symbol.Setter.Parameters [0]);
		}


		public string GetNullForgiveness (Field field)
		{
			if (field.NotNull || field.Symbol.IsEnum)
				return NullForgivingOperator;

			return string.Empty;
		}

		public string GetNullForgiveness (ReturnValue symbol)
		{
			if (symbol.NotNull || symbol.Symbol.IsEnum)
				return NullForgivingOperator;

			return string.Empty;
		}

		public string GetNullForgiveness (Parameter symbol)
		{
			if (symbol.NotNull || symbol.Symbol.IsEnum)
				return NullForgivingOperator;

			return string.Empty;
		}

		string GetNullable (string s)
		{
			switch (s) {
				case "void":
				case "int":
				case "bool":
				case "float":
				case "sbyte":
				case "long":
				case "char":
				case "double":
				case "short":
				case "uint":
				case "ushort":
				case "ulong":
				case "byte":
				case "System.Boolean":
				case "System.Byte":
				case "System.Char":
				case "System.Double":
				case "System.Int16":
				case "System.Int32":
				case "System.Int64":
				case "System.Single":
				case "System.SByte":
				case "System.UInt16":
				case "System.UInt32":
				case "System.UInt64":
				case "System.Void":
				case "Android.Graphics.Color":
					return string.Empty;
			}

			return NullableOperator;
		}

		// Encoding format:
		// - Type name prefix: _JniMarshal_PP
		// - Parameter types, using JNI encoding, e.g. Z is boolean, I is int, etc.
		//   - Exception: Reference types, normally encoded as L…;, are instead just L.
		//   - Lowercase JNI encoding indicates unsigned type, e.g. i is uint.
		// - Another _.
		// - Return type, encoded as with parameters. A void return type is V.
		internal string GetJniMarshalDelegate (Method method)
		{
			var sb = new StringBuilder ("_JniMarshal_PP");

			foreach (var p in method.Parameters)
				sb.Append (GetJniTypeCode (p.Symbol));

			sb.Append ("_");
			sb.Append (GetJniTypeCode (method.RetVal.Symbol));

			var result = sb.ToString ();

			lock (jni_marshal_delegates_lock)
				jni_marshal_delegates.Add (result);

			return result;
		}

		string GetJniTypeCode (ISymbol symbol)
		{
			// The JniName for our Kotlin unsigned types is the same
			// as the Java signed types, so check the original symbol
			// name and encode lowercase for unsigned version.
			switch (symbol.JavaName) {
				case "ubyte": return "b";
				case "uint": return "i";
				case "ulong": return "j";
				case "ushort": return "s";
			}

			var jni_name = symbol.JniName;

			if (jni_name.StartsWith ("L", StringComparison.Ordinal) || jni_name.StartsWith ("[", StringComparison.Ordinal))
				return "L";

			return symbol.JniName;
		}

		internal IEnumerable<string> GetJniMarshalDelegates ()
		{
			lock (jni_marshal_delegates_lock)
				return jni_marshal_delegates;
		}

		public string GetOutputName (string type)
		{
			// Handle a few special cases
			if (type == "System.Void")
				return "void";
			if (type.StartsWith ("params ", StringComparison.Ordinal))
				return "params " + GetOutputName (type.Substring ("params ".Length));
			if (type.StartsWith ("global::", StringComparison.Ordinal))
				Report.LogCodedErrorAndExit (Report.ErrorUnexpectedGlobal);
			if (!UseGlobal)
				return type;

			// Add "global::" in front of types
			return ParsedType.Parse (type).ToString (UseGlobal);
		}

		public string GetSafeIdentifier (string name)
		{
			if (string.IsNullOrEmpty (name))
				return name;

			// NOTE: "partial" differs in behavior on macOS vs. Windows, Windows reports "partial" as a valid identifier
			//	This check ensures the same output on both platforms
			switch (name) {
				case "partial": return name;
				// `this` isn't in TypeNameUtilities.reserved_keywords; special-case.
				case "this": return "this_";
			}

			// In the ideal world, it should not be applied twice.
			// Sadly that is not true in reality, so we need to exclude non-symbols
			// when replacing the argument name with a valid identifier.
			// (ReturnValue.ToNative() takes an argument which could be either an expression or mere symbol.)
			if (name [name.Length-1] != ')' && !name.Contains ('.') && !name.StartsWith ("@", StringComparison.Ordinal)) {
				if (!IdentifierValidator.IsValidIdentifier (name) ||
						Array.BinarySearch (TypeNameUtilities.reserved_keywords, name) >= 0) {
					name = name + "_";
				}
			}
			return name.Replace ('$', '_');
		}

		readonly Dictionary<string,string> short_file_names = new Dictionary<string, string> ();

		public string GetFileName (string fullName)
		{
			if (!UseShortFileNames)
				return fullName;

			lock (short_file_names) {
				if (short_file_names.TryGetValue (fullName, out var s))
					return s;

				s = short_file_names.Count.ToString ();
				short_file_names [fullName] = s;

				return s;
			}
		}

		public string GetTransformedNamespace (string value)
		{
			if (!value.HasValue () || !NamespaceTransforms.Any ())
				return value;

			foreach (var nt in NamespaceTransforms)
				value = nt.Apply (value);

			return value;
		}

		public string GetStringArrayToCharSequenceArrayMethodName ()
		{
			return CodeGenerationTarget == CodeGenerationTarget.JavaInterop1
				? "ICharSequenceExtensions.ToCharSequenceArray"
				: "CharSequence.ArrayFromStringArray";
		}
	}
}

