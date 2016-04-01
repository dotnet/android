using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Mono.Cecil;

using Xamarin.Android.Tools;

using MonoDroid.Utils;

namespace MonoDroid.Generation
{
	public abstract class GenBaseSupport
	{
		public abstract bool IsAcw { get; }
		public abstract bool IsDeprecated { get; }
		public abstract string DeprecatedComment { get; }
		public abstract bool IsGeneratable { get; }
		public abstract bool IsGeneric { get; }
		public abstract string FullName { get; set; }
		public abstract string Name { get; set; }
		public abstract string Namespace { get; }
		public abstract string JavaSimpleName { get; }
		public abstract string PackageName { get; set; }
		//public abstract string Marshaler { get; }
		public abstract string Visibility { get; }
		public abstract GenericParameterDefinitionList TypeParameters { get; }

		public virtual string TypeNamePrefix {
			get { return String.Empty; }
		}
		
		public virtual bool ValidateNamespace ()
		{
			// See com.google.inject.internal.util package for this case.
			// Some Java compiler-generated internals are named as $foobar (dollar prefixed).
			// Since our jar2xml replaces all '$' with '.', it results in ".." namespace.
			if (this.FullName.Contains (".."))
				return false;
			return true;
		}

		public static bool IsPrefixableName (string name)
		{
			// IBlahBlah is not prefixed with 'I'
			return name.Length <= 2 || name [0] != 'I' || !Char.IsUpper (name [1]);
		}
	}
	
#if USE_CECIL
	public class ManagedGenBaseSupport : GenBaseSupport
	{
		TypeDefinition t;
		string pkg_name, java_name, full_name;
		GenericParameterDefinitionList type_parameters;
		bool deprecated, is_acw;
		string deprecatedComment;

		public ManagedGenBaseSupport (TypeDefinition t)
		{
			this.t = t;
			var regatt = t.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			string jn = regatt != null ? ((string) regatt.ConstructorArguments [0].Value).Replace ('/', '.') : t.FullNameCorrected ();
			int idx = jn.LastIndexOf ('.');
			pkg_name = idx < 0 ? String.Empty : jn.Substring (0, idx);
			java_name = SymbolTable.FilterPrimitiveFullName (t.FullNameCorrected ());
			if (java_name == null) {
				java_name = idx < 0 ? jn : jn.Substring (idx + 1);
				full_name = t.FullNameCorrected ();
			} else {
				var sym = SymbolTable.Lookup (java_name);
				full_name = sym != null ? sym.FullName : t.FullNameCorrected ();
			}
			java_name = java_name.Replace ('$', '.');
			type_parameters = GenericParameterDefinitionList.FromMetadata (t.GenericParameters);

			var obsolete    = t.CustomAttributes.FirstOrDefault (ca => ca.AttributeType.FullName == "System.ObsoleteAttribute");
			if (obsolete != null) {
				deprecated        = true;
				deprecatedComment = obsolete.HasConstructorArguments
					? obsolete.ConstructorArguments [0].Value.ToString ()
					: "This class is obsoleted in this android platform";
			}
		}

		public override bool IsAcw {
			get { return is_acw; }
		}
		
		public override bool IsDeprecated {
			get { return deprecated; }
		}
		
		public override string DeprecatedComment {
			get { return deprecatedComment; }
		}

		public override bool IsGeneratable {
			get { return false; }
		}

		public override string FullName {
			get { return full_name; }
			set { throw new NotImplementedException (); }
		}

		public override bool IsGeneric {
			get { return t.HasGenericParameters; }
		}

		public override string JavaSimpleName {
			get { return java_name; }
		}
		
		/*
		public override string Marshaler {
			get { return null; }
		}
		*/

		public override string Name {
			get { return t.Name; }
			set { throw new NotImplementedException (); }
		}

		public override string Namespace {
			get { return t.Namespace; }
		}

		public override string PackageName {
			get { return pkg_name; }
			set { throw new NotImplementedException (); }
		}

		public override string TypeNamePrefix {
			get { return String.Empty; }
		}

		public override GenericParameterDefinitionList TypeParameters {
			get { return type_parameters; }
		}

		public override string Visibility {
			get { return t.IsPublic ? "public" : "protected internal"; }
		}
	}
#endif

	public class XmlGenBaseSupport : GenBaseSupport
	{
		public XmlGenBaseSupport (XmlElement pkg, XmlElement elem)
		{
			deprecated = elem.XGetAttribute ("deprecated") != "not deprecated";
			if (deprecated) {
				deprecatedComment = elem.XGetAttribute ("deprecated");
				if (deprecatedComment == "deprecated")
					deprecatedComment = "This class is obsoleted in this android platform";
			}
			visibility = elem.XGetAttribute ("visibility");
			if (visibility == "protected")
				visibility = "protected internal";

			pkg_name = pkg.XGetAttribute ("name");
			java_name = elem.XGetAttribute ("name");
			if (pkg.HasAttribute ("managedName")) {
				ns = pkg.XGetAttribute ("managedName");
			} else {
				ns = StringRocks.PackageToPascalCase (PackageName);
			}
			
			var tpn = elem.SelectSingleNode ("typeParameters");
			if (tpn != null) {
				type_params = GenericParameterDefinitionList.FromXml (tpn);
				is_generic = true;
				int idx = java_name.IndexOf ('<');
				if (idx > 0)
					java_name = java_name.Substring (0, idx);
			} else {
				int idx = java_name.IndexOf ('<');
				if (idx > 0)
					throw new NotSupportedException ("Looks like old API XML is used, which we don't support anymore.");
			}
 
			if (elem.HasAttribute ("managedName")) {
				name = elem.XGetAttribute ("managedName");
				full_name = String.Format ("{0}.{1}", ns, name);
				int idx = name.LastIndexOf ('.');
				name = idx > 0 ? name.Substring (idx + 1) : name;
				raw_name = name;
			} else {
				int idx = java_name.LastIndexOf ('.');
				name = idx > 0 ? java_name.Substring (idx + 1) : java_name;
				if (Char.IsLower (name[0]))
					name = StringRocks.TypeToPascalCase (name);
				raw_name = name;
				name = TypeNamePrefix + raw_name;
				full_name = String.Format ("{0}.{1}{2}", ns, idx > 0 ? StringRocks.TypeToPascalCase (java_name.Substring (0, idx + 1)) : String.Empty, name);
			}
			//marshaler = elem.XGetAttribute ("marshaler");
			//if (!string.IsNullOrEmpty (marshaler)) throw new Exception ();
		}

		public override bool IsAcw {
			get { return true; }
		}
		
		bool deprecated;
		public override bool IsDeprecated {
			get { return deprecated; }
		}

		string deprecatedComment;
		public override string DeprecatedComment {
			get { return deprecatedComment; }
		}

		public override bool IsGeneratable {
			get { return true; }
		}
		
		string java_name;
		public override string JavaSimpleName {
			get { return java_name; }
		}
		
		string pkg_name;
		public override string PackageName {
			get { return pkg_name; }
			set { pkg_name = value; }
		}
		
		string full_name;
		public override string FullName {
			get { return full_name; }
			set { full_name = value; }
		}

		string name;
		public override string Name {
			get { return name; }
			set { name = value; }
		}
		string ns;
		public override string Namespace {
			get { return ns; }
		}

		/*
		string marshaler;
		public override string Marshaler {
			get { return marshaler; }
		}
		*/

		string raw_name;
		internal string RawName {
			get { return raw_name; }
		}

		GenericParameterDefinitionList type_params;
		public override GenericParameterDefinitionList TypeParameters {
			get { return type_params; }
		}

		bool is_generic;
		public override bool IsGeneric {
			get { return is_generic; }
		}

		string visibility;
		public override string Visibility {
			get { return visibility; }
		}
		
		public override bool ValidateNamespace ()
		{
			if (!base.ValidateNamespace ())
				return false;
			string topmost;
			int split = ns.LastIndexOf ('.');
			if (split < 0)
				topmost = ns;
			else
				topmost = ns.Substring (split + 1);
			if (topmost.Length == name.Length && string.Compare (topmost, 0, name, 0, topmost.Length, StringComparison.OrdinalIgnoreCase) == 0) {
				// FIXME: this should really be prohibited. See https://app.asana.com/0/77259014259/1186053910891
				Report.Warning (0, Report.WarningGenBaseSupport + 0, "Type {0}.{1}: FxDG naming violation: Type name '{1}' matches namespace part '{2}'.", pkg_name, java_name, topmost);
				// return false;
			}
			return true;
		}
	}
	
	public class InterfaceXmlGenBaseSupport : XmlGenBaseSupport
	{
		public InterfaceXmlGenBaseSupport (XmlElement pkg, XmlElement elem)
			: base (pkg, elem)
		{
		}
		
		public override string TypeNamePrefix {
			get { return (IsPrefixableName (RawName) ? "I" : string.Empty); }
		}
	}
}


