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

		internal Parameter (string name, string type, string managedType, bool isEnumified, string rawtype = null, bool notNull = false)
		{
			this.name = name;
			this.type = type;
			this.rawtype = rawtype ?? type;
			this.managed_type = managedType;
			this.is_enumified = isEnumified;
			NotNull = notNull;
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
			return NeedsPrep ? sym.Call (opt, Name) : sym.ToNative (opt, opt.GetSafeIdentifier (Name), null);
		}

		public string GenericType {
			get { return sym.GetGenericType (null) ?? Type; }
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return sym.GetGenericType (mappings) ?? Type;
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

		public bool NotNull { get; set; }

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
				return TypeNameUtilities.GetNativeName (Name);
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
				return new string[] { "var " + opt.GetSafeIdentifier (Name) + " = " + FromNative (opt, false) + ";" };
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
			var rgm = opt.SymbolTable.Lookup (targetType) as IRequireGenericMarshal;
			return string.Format ("global::Java.Interop.JavaObjectExtensions.JavaCast<{0}>({1}){2}",
					opt.GetOutputName (rgm != null ? (rgm.GetGenericJavaObjectTypeOverride () ?? targetType) : targetType),
					name,
					opt.NullForgivingOperator); 
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			sym = opt.SymbolTable.Lookup (type, type_params);
			if (sym == null) {
				Report.Warning (0, Report.WarningParameter + 0, "Unknown parameter type {0} {1}.", type, context.ContextString);
				return false;
			}
			if (!sym.Validate (opt, type_params, context)) {
				Report.Warning (0, Report.WarningParameter + 1, "Invalid parameter type {0} {1}.", type, context.ContextString);
				return false;
			}
			return true;
		}

		public ISymbol Symbol => sym;
	}
}
