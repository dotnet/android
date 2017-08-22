using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Mono.Cecil;

using Xamarin.Android.Binder;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation {

	public class Parameter {

		bool is_sender;
		string name;
		string type, managed_type, rawtype;
		ISymbol sym;
		bool is_enumified;

		private Parameter (string name, string type, string managedType, bool isEnumified, string rawtype = null)
		{
			this.name = name;
			this.type = type;
			this.rawtype = rawtype ?? type;
			this.managed_type = managedType;
			this.is_enumified = isEnumified;
		}
		
		public string GetCall (CodeGenerationOptions opt)
		{
			var rgm = sym as IRequireGenericMarshal;
			var c   = rgm != null
				? opt.GetSafeIdentifier (rgm.ToInteroperableJavaObject (Name))
				: ToNative (opt);
			if (opt.CodeGenerationTarget == CodeGenerationTarget.XamarinAndroid)
				return c;
			if (sym.NativeType != "IntPtr")
				return c;
			if (!NeedsPrep)
				return c;
			var h = sym.GetObjectHandleProperty (c);
			if (sym.PreCall (opt, Name).Length == 0)
				return string.Format ("({0} == null) ? IntPtr.Zero : {1}", c, h);
			return c;
		}

		public string ToNative (CodeGenerationOptions opt)
		{
			var safeName = opt.GetSafeIdentifier (Name);
			return NeedsPrep ? sym.Call (opt, safeName) : sym.ToNative (opt, safeName, null);
		}

		public string GenericType {
			get { return sym.GetGenericType (null) ?? Type; }
		}

		public bool IsArray {
			get { return sym.IsArray; }
		}

		public bool IsGeneric {
			get { return !string.IsNullOrEmpty (sym.GetGenericType (null)); }
		}

		public bool IsListener {
			get { return sym is InterfaceGen && (sym as InterfaceGen).IsListener; }
		}

		public bool IsSender {
			get { return is_sender; }
			set { is_sender = value; }
		}
		
		public bool IsEnumified {
			get { return is_enumified; }
		}

		public string JavaType { 
			get { return sym.JavaName; }
		}

		public string JniType { 
			get { return sym.JniName; }
		}

		public InterfaceGen ListenerType {
			get { return IsListener ? sym as InterfaceGen : null; }
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public string PropertyName {
			get {
				if (Name == "e")
					return "Event";
				string name = Name;
				return (name [0] == '@')
					? char.ToUpper (name [1]) + name.Substring (2)
					: char.ToUpper (name [0]) + name.Substring (1);
			}
		}

		public string UnsafeNativeName {
			get {
				if (Type == NativeType)
					return Name;
				return SymbolTable.GetNativeName (Name);
			}
		}

		public string JavaName {
			get {
				if (Name.StartsWith ("@"))
					return Name.Substring (1);
				return Name;
			}
		}

		public string NativeType { 
			get { return sym.NativeType; }
		}

		public string RawNativeType {
			get { return rawtype; }
		}

		public bool NeedsPrep {
			get { return sym.NeedsPrep; }
		}

		public string[] GetPostCall (CodeGenerationOptions opt)
		{
			return !NeedsPrep ? new string [0] : sym.PostCall (opt, Name);
		}

		public string[] GetPostCallback (CodeGenerationOptions opt)
		{
			return !NeedsPrep ? new string [0] : sym.PostCallback (opt, Name);
		}

		public string GetName (string prefix = null)
		{
			if (string.IsNullOrEmpty (prefix))
				return Name;
			return prefix + JavaName;
		}

		public string[] GetPreCall (CodeGenerationOptions opt)
		{
			return !NeedsPrep ? new string [0] : sym.PreCall (opt, Name);
		}
		
		public bool Equals (Parameter other)
		{
			if (this.IsGeneric == other.IsGeneric) {
				if (this.GenericType != other.GenericType)
					return false;
				if (sym is GenericSymbol && other.sym is GenericSymbol)
					if (!Equals ((GenericSymbol) sym, (GenericSymbol) other.sym))
						return false;
			}
			return this.IsArray == other.IsArray &&
				FilterCSharpType (this.Type) == FilterCSharpType (other.Type);
		}
		
		static bool Equals (GenericSymbol g1, GenericSymbol g2)
		{
			if (g1.IsConcrete != g2.IsConcrete)
				return false;
			if (g1.FullName != g2.FullName)
				return false;
			return true;
		}
		
		static string FilterCSharpType (string s)
		{
			switch (s) {
			case "bool":
				return "System.Boolean";
			case "char":
				return "System.Char";
			case "byte":
				return "System.Byte";
			case "short":
				return "System.Int16";
			case "int":
				return "System.Int32";
			case "long":
				return "System.Int64";
			case "float":
				return "System.Single";
			case "double":
				return "System.Double";
			case "string":
				return "System.String";
			default:
				return s;
			}
		}

		public string[] GetPreCallback (CodeGenerationOptions opt)
		{
			if (Type == NativeType)
				return new string [0];
			else if (NeedsPrep)
				return sym.PreCallback (opt, Name, false);
			else
				return new string[] { opt.GetOutputName (Type) + " " + opt.GetSafeIdentifier (Name) + " = " + FromNative (opt, false) + ";" };
		}

		public string Type {
			//get { return sym is GenBase && !String.IsNullOrEmpty ((sym as GenBase).Marshaler) ? (sym as GenBase).Marshaler : sym.FullName; }
			get { return managed_type ?? sym.FullName; }
		}

		public string Annotation { get; internal set; }

		public void SetGeneratedEnumType (string enumType)
		{
			sym = new GeneratedEnumSymbol (enumType);
			managed_type = null;
			type = sym.JavaName;
			is_enumified = true;
		}

		public string FromNative (CodeGenerationOptions opt, bool owned)
		{
			return sym.FromNative (opt, UnsafeNativeName, owned);
		}

		public string GetGenericCall (CodeGenerationOptions opt, Dictionary<string, string> mappings)
		{
			string targetType = sym.GetGenericType (mappings);
			if (string.IsNullOrEmpty (targetType))
				return name;
			if (targetType == "string")
				return string.Format ("{0}.ToString ()", name);
			if (targetType.EndsWith ("[]")) {
				return string.Format ("{0}.ToArray<{1}> ()", name, targetType.Replace ("[]",""));
			}
			var rgm = SymbolTable.Lookup (targetType) as IRequireGenericMarshal;
			return string.Format ("global::Java.Interop.JavaObjectExtensions.JavaCast<{0}>({1})",
					opt.GetOutputName (rgm != null ? (rgm.GetGenericJavaObjectTypeOverride () ?? targetType) : targetType),
					name); 
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			sym = SymbolTable.Lookup (type, type_params);
			if (sym == null) {
				Report.Warning (0, Report.WarningParameter + 0, "Unknown parameter type {0} {1}.", type, opt.ContextString);
				return false;
			}
			if (!sym.Validate (opt, type_params)) {
				Report.Warning (0, Report.WarningParameter + 1, "Invalid parameter type {0} {1}.", type, opt.ContextString);
				return false;
			}
			return true;
		}

		public static Parameter FromElement (XElement elem)
		{
			string managedName = elem.XGetAttribute ("managedName");
			string name = !string.IsNullOrEmpty (managedName) ? managedName : SymbolTable.MangleName (elem.XGetAttribute ("name"));
			string java_type = elem.XGetAttribute ("type");
			string enum_type = elem.Attribute ("enumType") != null ? elem.XGetAttribute ("enumType") : null;
			string managed_type = elem.Attribute ("managedType") != null ? elem.XGetAttribute ("managedType") : null;
			// FIXME: "enum_type ?? java_type" should be extraneous. Somewhere in generator uses it improperly.
			var result = new Parameter (name, enum_type ?? java_type, enum_type ?? managed_type, enum_type != null, java_type);
			if (elem.Attribute ("sender") != null)
				result.IsSender = true;
			return result;
		}

		public static Parameter FromClassElement (XElement elem)
		{
			string name          = "__self";
			string java_type     = elem.XGetAttribute ("name");
			string java_package  = elem.Parent.XGetAttribute ("name");
			return new Parameter (name, java_package + "." + java_type, null, false);
		}
		
#if HAVE_CECIL
		public static Parameter FromManagedParameter (ParameterDefinition p, string jnitype, string rawtype)
		{
			// FIXME: safe to use CLR type name? assuming yes as we often use it in metadatamap.
			// FIXME: IsSender?
			bool isEnumType = p.CustomAttributes.Any (c => c.AttributeType.Name == "GeneratedEnumAttribute");
			return new Parameter (SymbolTable.MangleName (p.Name), jnitype ?? p.ParameterType.FullNameCorrected (), null, isEnumType, rawtype);
		}
		
		public static Parameter FromManagedType (TypeDefinition t, string javaType)
		{
			return new Parameter ("__self", javaType ?? t.FullName, t.FullName, false);
		}
#endif	// HAVE_CECIL
	}
}
