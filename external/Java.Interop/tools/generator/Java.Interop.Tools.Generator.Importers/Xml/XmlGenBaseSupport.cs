using System;
using System.Linq;

using Xamarin.Android.Tools;

using MonoDroid.Utils;
using System.Xml.Linq;

namespace MonoDroid.Generation
{
	public class XmlGenBaseSupport : GenBaseSupport
	{
		public XmlGenBaseSupport (XElement pkg, XElement elem)
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
			if (pkg.Attribute ("managedName") != null) {
				ns = pkg.XGetAttribute ("managedName");
			} else {
				ns = StringRocks.PackageToPascalCase (PackageName);
			}
			
			var tpn = elem.Element ("typeParameters");
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
 
			if (elem.Attribute ("managedName") != null) {
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

			obfuscated = IsObfuscatedName (pkg.Elements ().Count (), java_name) && elem.XGetAttribute ("obfuscated") != "false";
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

		bool obfuscated;
		public override bool IsObfuscated {
			get { return obfuscated; }
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

		public override bool OnValidate (CodeGenerationOptions opt)
		{
			if (!base.OnValidate (opt))
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

		bool IsObfuscatedName (int threshold, string name)
		{
			if (name.StartsWith ("R.", StringComparison.Ordinal))
				return false;
			int idx = name.LastIndexOf ('.');
			string last = idx < 0 ? name : name.Substring (idx + 1);
			// probably new proguard within Gradle tasks, used in recent GooglePlayServices in 2016 or later.
			if (last.StartsWith ("zz", StringComparison.Ordinal))
				return true;
			// do not expect any name with more than 3 letters is an 'obfuscated' one.
			if (last.Length > 3)
				return false;
			// Only short ones ('a', 'b', 'c' ... 'aa', 'ab', ... 'zzz') are the targets.
			if (!(last.Length == 3 && threshold > 26*26 || last.Length == 2 && threshold > 26 || last.Length == 1))
				return false;
			if (last.Any (c => (c < 'a' || 'z' < c) && (c < '0' || '9' < c)))
					return false;
			return true;
		}
	}
}


