using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Mono.Cecil;

using Xamarin.Android.Tools;

using MonoDroid.Utils;
using System.Xml.Linq;

namespace MonoDroid.Generation {
#if USE_CECIL
	public class ManagedField : Field {
		FieldDefinition f;
		string java_name;
		bool is_acw;

		public ManagedField (FieldDefinition f)
		{
			this.f = f;
			var regatt = f.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			java_name = regatt != null ? ((string) regatt.ConstructorArguments [0].Value).Replace ('/', '.') : f.Name;
		}

		public override bool IsAcw {
			get { return is_acw; }
		}

		public override bool IsDeprecated {
			get { return f.CustomAttributes.Any (a => a.AttributeType.FullNameCorrected () == "System.ObsoleteAttribute"); }
		}

		public override string DeprecatedComment {
			get {
				if (!IsDeprecated)
					return null;
				var ca = f.CustomAttributes.First (a => a.AttributeType.FullNameCorrected () == "System.ObsoleteAttribute");
				return ca.ConstructorArguments.Any () ? (string) ca.ConstructorArguments [0].Value : string.Empty;
			}
		}
		
		public override bool IsEnumified {
			get { return f.CustomAttributes.Any (c => c.AttributeType.FullName == "Android.Runtime.GeneratedEnumAttribute"); }
		}

		public override bool IsFinal {
			get { return f.Constant != null; }
		}

		public override bool IsStatic {
			get { return f.IsStatic; }
		}

		public override string JavaName {
			get { return java_name; }
		}

		public override string TypeName {
			get { return f.FieldType.FullNameCorrected (); }
		}

		public override string Name {
			get { return f.Name; }
			set { throw new NotSupportedException (); }
		}

		public override string Value {
			get { return f.Constant == null ? null : f.FieldType.FullName == "System.String" ? '"' + f.Constant.ToString () + '"' : f.Constant.ToString (); }
		}

		public override string Visibility {
			get { return f.IsPublic ? "public" : f.IsFamilyOrAssembly ? "protected internal" : f.IsFamily ? "protected" : f.IsAssembly ? "internal" : "private"; }
		}

		protected override Parameter SetterParameter {
			get {
				var p = Parameter.FromManagedType (f.FieldType.Resolve (), null);
				p.Name = "value";
				return p;
			}
		}
	}
#endif

	public class XmlField : Field {

		XElement elem;
		string java_name;
		string name;
		string enum_type;

		public XmlField (XElement elem)
		{
			this.elem = elem;
			java_name = elem.XGetAttribute ("name");
			if (elem.Attribute ("managedName") != null)
				name = elem.XGetAttribute ("managedName");
			else
				name = SymbolTable.StudlyCase (Char.IsLower (java_name [0]) || java_name.ToLower ().ToUpper () != java_name ? java_name : java_name.ToLower ());
			if (elem.Attribute ("enumType") != null)
				enum_type = elem.XGetAttribute ("enumType");
		}

		public override bool IsDeprecated {
			get { return elem.XGetAttribute ("deprecated") != "not deprecated"; }
		}

		public override string DeprecatedComment {
			get { return elem.XGetAttribute ("deprecated"); }
		}

		public override bool IsEnumified {
			get { return enum_type != null; }
		}

		public override bool IsFinal {
			get { return elem.XGetAttribute ("final") == "true"; }
		}

		public override bool IsStatic {
			get { return elem.XGetAttribute ("static") == "true"; }
		}

		public override string Name { 
			get { return name; }
			set { name = value; }
		}

		public override string JavaName { 
			get { return java_name; }
		}
		
		public override string TypeName {
			get { return enum_type ?? elem.XGetAttribute ("type"); }
		}

		public override string Value {
			get { 
				string val = elem.XGetAttribute ("value"); // do not trim
				if (!String.IsNullOrEmpty (val) && Symbol != null && Symbol.FullName == "char")
					val = "(char)" + val;
				return val;
			}
		}

		public override string Visibility {
			get { return elem.XGetAttribute ("visibility"); }
		}

		protected override Parameter SetterParameter {
			get {
				var p = Parameter.FromElement (elem);
				p.Name = "value";
				return p;
			}
		}
	}
	
	public abstract class Field : ApiVersionsSupport.IApiAvailability {

		public virtual bool IsAcw {
			get { return true; }
		}

		public abstract bool IsDeprecated { get; }

		public abstract string DeprecatedComment { get; }

		public abstract bool IsFinal { get; }

		public abstract bool IsStatic { get; }

		public abstract string JavaName { get; }

		public abstract bool IsEnumified { get; }

		public abstract string TypeName { get; }

		public abstract string Name { get; set; }

		public abstract string Value { get; }

		public abstract string Visibility { get; }

		public int ApiAvailableSince { get; set; }

		protected abstract Parameter SetterParameter { get; }

		static readonly HashSet<string> primitive_types = new HashSet<string> {
			"boolean",
			"char",
			"byte",
			"short",
			"int",
			"long",
			"float",
			"double",
			// while technically not a primitive type, Strings have the feature that
			// their value is stored within bytecode and their value is thus accessible
			// within the api.xml description.
			"java.lang.String",
		};
		
		ISymbol symbol;

		ParameterList setParameters;

		internal string GetMethodPrefix {
			get { return (Symbol is SimpleSymbol || Symbol.IsEnum) ? StringRocks.MemberToPascalCase (Symbol.JavaName) : "Object"; }
		}

		public bool IsConst {
			get { return IsFinal && IsStatic; }
		}

		public bool NeedsProperty {
			get { return !IsStatic || !IsFinal || String.IsNullOrEmpty (Value) || Symbol.IsArray || !primitive_types.Contains (Symbol.JavaName); }
		}

		public ISymbol Symbol {
			get { return symbol; }
		}

		internal string ID {
			get { return JavaName + "_jfieldId"; }
		}

		internal ParameterList SetParameters {
			get { return setParameters; }
		}

		public string Annotation { get; internal set; }

		void GenerateProperty (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase gen)
		{
			string type = Symbol.IsArray ? "IList<" + Symbol.ElementType + ">" : opt.GetOutputName (Symbol.FullName);
			opt.CodeGenerator.WriteFieldIdField (this, sw, indent, opt);
			sw.WriteLine ();
			sw.WriteLine ("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, gen.MetadataXPathReference, JavaName);
			sw.WriteLine ("{0}[Register (\"{1}\"{2})]", indent, JavaName, this.AdditionalAttributeString ());
			sw.WriteLine ("{0}{1} {2}{3} {4} {{", indent, Visibility, IsStatic ? "static " : String.Empty, type, Name);
			sw.WriteLine ("{0}\tget {{", indent);
			opt.CodeGenerator.WriteFieldGetBody (this, sw, indent + "\t\t", opt);
			sw.WriteLine ("{0}\t}}", indent);

			if (!IsConst) {
				sw.WriteLine ("{0}\tset {{", indent);
				opt.CodeGenerator.WriteFieldSetBody (this, sw, indent + "\t\t", opt);
				sw.WriteLine ("{0}\t}}", indent);
			}
			sw.WriteLine ("{0}}}", indent);
		}

		public void Generate (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase type)
		{
			if (IsEnumified)
				sw.WriteLine ("[global::Android.Runtime.GeneratedEnum]");
			if (NeedsProperty)
				GenerateProperty (sw, indent, opt, type);
			else {
				sw.WriteLine ("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, type.MetadataXPathReference, JavaName);
				sw.WriteLine ("{0}[Register (\"{1}\"{2})]", indent, JavaName, this.AdditionalAttributeString ());
				if (IsDeprecated)
					sw.WriteLine ("{0}[Obsolete (\"{1}\")]",  indent, DeprecatedComment);
				if (Annotation != null)
					sw.WriteLine ("{0}{1}", indent, Annotation);

				// the Value complication is due to constant enum from negative integer value (C# compiler requires explicit parenthesis).
				sw.WriteLine ("{0}{1} const {2} {3} = ({2}) {4};", indent, Visibility, opt.GetOutputName (Symbol.FullName), Name, Value.Contains ('-') && Symbol.FullName.Contains ('.') ? '(' + Value + ')' : Value);
			}
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			symbol = SymbolTable.Lookup (TypeName, type_params);
			
			if (symbol == null || !symbol.Validate (opt, type_params)) {
				Report.Warning (0, Report.WarningField + 0, "unexpected field type {0} {1}.", TypeName, opt.ContextString);
				return false;
			}

			setParameters = new ParameterList () {
				SetterParameter,
			};
			if (!setParameters.Validate (opt, type_params))
				throw new NotSupportedException (
					string.Format ("Unable to generate setter parameter list {0}", opt.ContextString));

			return true;
		}
	}
}
