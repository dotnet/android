using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MonoDroid.Utils;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	class XmlApiImporter
	{
		static readonly Regex api_level = new Regex (@"api-(\d+).xml");

		public static ClassGen CreateClass (XElement pkg, XElement elem)
		{
			var klass = new ClassGen (CreateGenBaseSupport (pkg, elem, false)) {
				BaseType = elem.XGetAttribute ("extends"),
				FromXml = true,
				IsAbstract = elem.XGetAttribute ("abstract") == "true",
				IsFinal = elem.XGetAttribute ("final") == "true"
			};

			foreach (var child in elem.Elements ()) {
				switch (child.Name.LocalName) {
					case "implements":
						var iname = child.XGetAttribute ("name-generic-aware");
						iname = iname.Length > 0 ? iname : child.XGetAttribute ("name");
						klass.AddImplementedInterface (iname);
						break;
					case "method":
						klass.AddMethod (CreateMethod (klass, child));
						break;
					case "constructor":
						klass.Ctors.Add (CreateCtor (klass, child));
						break;
					case "field":
						klass.AddField (CreateField (child));
						break;
					case "typeParameters":
						break; // handled at GenBaseSupport
					default:
						Report.Warning (0, Report.WarningClassGen + 1, "unexpected class child {0}.", child.Name);
						break;
				}
			}

			return klass;
		}

		public static Ctor CreateCtor (GenBase declaringType, XElement elem)
		{
			var ctor = new Ctor (declaringType) {
				CustomAttributes = elem.XGetAttribute ("customAttributes"),
				Deprecated = elem.Deprecated (),
				GenericArguments = elem.GenericArguments (),
				Name = elem.XGetAttribute ("name"),
				Visibility = elem.Visibility ()
			};

			var idx = ctor.Name.LastIndexOf ('.');

			if (idx > 0)
				ctor.Name = ctor.Name.Substring (idx + 1);

			// If 'elem' is a constructor for a non-static nested type, then
			// the type of the containing class must be inserted as the first argument
			ctor.IsNonStaticNestedType = idx > 0 && elem.Parent.Attribute ("static").Value == "false";

			if (ctor.IsNonStaticNestedType) {
				string declName = elem.Parent.XGetAttribute ("name");
				string expectedEnclosingName = declName.Substring (0, idx);
				XElement enclosingType = GetPreviousClass (elem.Parent.PreviousNode, expectedEnclosingName);

				if (enclosingType == null) {
					ctor.MissingEnclosingClass = true;
					Report.Warning (0, Report.WarningCtor + 0, "For {0}, could not find enclosing type '{1}'.", ctor.Name, expectedEnclosingName);
				} else
					ctor.Parameters.AddFirst (CreateParameterFromClassElement (enclosingType));
			}

			foreach (var child in elem.Elements ()) {
				if (child.Name == "parameter")
					ctor.Parameters.Add (CreateParameter (child));
			}

			return ctor;
		}

		public static Field CreateField (XElement elem)
		{
			var field = new Field {
				DeprecatedComment = elem.XGetAttribute ("deprecated"),
				IsAcw = true,
				IsDeprecated = elem.XGetAttribute ("deprecated") != "not deprecated",
				IsFinal = elem.XGetAttribute ("final") == "true",
				IsStatic = elem.XGetAttribute ("static") == "true",
				JavaName = elem.XGetAttribute ("name"),
				SetterParameter = CreateParameter (elem),
				TypeName = elem.XGetAttribute ("type"),
				Value = elem.XGetAttribute ("value"), // do not trim
				Visibility = elem.XGetAttribute ("visibility")
			};

			field.SetterParameter.Name = "value";

			if (elem.XGetAttribute ("enumType") != null) {
				field.IsEnumified = true;
				field.TypeName = elem.XGetAttribute ("enumType");
			}

			if (elem.Attribute ("managedName") != null)
				field.Name = elem.XGetAttribute ("managedName");
			else
				field.Name = SymbolTable.StudlyCase (char.IsLower (field.JavaName [0]) || field.JavaName.ToLower ().ToUpper () != field.JavaName ? field.JavaName : field.JavaName.ToLower ());

			return field;
		}

		public static GenBaseSupport CreateGenBaseSupport (XElement pkg, XElement elem, bool isInterface)
		{
			var support = new GenBaseSupport {
				IsAcw = true,
				IsDeprecated = elem.XGetAttribute ("deprecated") != "not deprecated",
				IsGeneratable = true,
				JavaSimpleName = elem.XGetAttribute ("name"),
				PackageName = pkg.XGetAttribute ("name"),
				Visibility = elem.XGetAttribute ("visibility")
			};
			
			if (support.IsDeprecated) {
				support.DeprecatedComment = elem.XGetAttribute ("deprecated");

				if (support.DeprecatedComment == "deprecated")
					support.DeprecatedComment = "This class is obsoleted in this android platform";
			}

			if (support.Visibility == "protected")
				support.Visibility = "protected internal";

			if (pkg.Attribute ("managedName") != null)
				support.Namespace = pkg.XGetAttribute ("managedName");
			else
				support.Namespace = StringRocks.PackageToPascalCase (support.PackageName);

			var tpn = elem.Element ("typeParameters");

			if (tpn != null) {
				support.TypeParameters = GenericParameterDefinitionList.FromXml (tpn);
				support.IsGeneric = true;
				int idx = support.JavaSimpleName.IndexOf ('<');
				if (idx > 0)
					support.JavaSimpleName = support.JavaSimpleName.Substring (0, idx);
			} else {
				int idx = support.JavaSimpleName.IndexOf ('<');
				if (idx > 0)
					throw new NotSupportedException ("Looks like old API XML is used, which we don't support anymore.");
			}

			string raw_name;

			if (elem.Attribute ("managedName") != null) {
				support.Name = elem.XGetAttribute ("managedName");
				support.FullName = string.Format ("{0}.{1}", support.Namespace, support.Name);
				int idx = support.Name.LastIndexOf ('.');
				support.Name = idx > 0 ? support.Name.Substring (idx + 1) : support.Name;
				raw_name = support.Name;
			} else {
				int idx = support.JavaSimpleName.LastIndexOf ('.');
				support.Name = idx > 0 ? support.JavaSimpleName.Substring (idx + 1) : support.JavaSimpleName;
				if (char.IsLower (support.Name [0]))
					support.Name = StringRocks.TypeToPascalCase (support.Name);
				raw_name = support.Name;
				support.TypeNamePrefix = isInterface ? IsPrefixableName (raw_name) ? "I" : string.Empty : string.Empty;
				support.Name = support.TypeNamePrefix + raw_name;
				support.FullName = string.Format ("{0}.{1}{2}", support.Namespace, idx > 0 ? StringRocks.TypeToPascalCase (support.JavaSimpleName.Substring (0, idx + 1)) : string.Empty, support.Name);
			}

			support.IsObfuscated = IsObfuscatedName (pkg.Elements ().Count (), support.JavaSimpleName) && elem.XGetAttribute ("obfuscated") != "false";

			return support;
		}

		public static InterfaceGen CreateInterface (XElement pkg, XElement elem)
		{
			var iface = new InterfaceGen (CreateGenBaseSupport (pkg, elem, true)) {
				ArgsType = elem.XGetAttribute ("argsType"),
				HasManagedName = elem.Attribute ("managedName") != null
			};

			foreach (var child in elem.Elements ()) {
				switch (child.Name.LocalName) {
					case "implements":
						string iname = child.XGetAttribute ("name-generic-aware");
						iname = iname.Length > 0 ? iname : child.XGetAttribute ("name");
						iface.AddImplementedInterface (iname);
						break;
					case "method":
						iface.AddMethod (CreateMethod (iface, child));
						break;
					case "field":
						iface.AddField (CreateField (child));
						break;
					case "typeParameters":
						break; // handled at GenBaseSupport
					default:
						Report.Warning (0, Report.WarningInterfaceGen + 0, "unexpected interface child {0}.", child);
						break;
				}
			}

			return iface;
		}

		public static Method CreateMethod (GenBase declaringType, XElement elem)
		{
			var method = new Method (declaringType) {
				ArgsType = elem.Attribute ("argsType")?.Value,
				CustomAttributes = elem.XGetAttribute ("customAttributes"),
				Deprecated = elem.Deprecated (),
				EventName = elem.Attribute ("eventName")?.Value,
				GenerateAsyncWrapper = elem.Attribute ("generateAsyncWrapper") != null,
				GenerateDispatchingSetter = elem.Attribute ("generateDispatchingSetter") != null,
				GenericArguments = elem.GenericArguments (),
				IsAbstract = elem.XGetAttribute ("abstract") == "true",
				IsAcw = true,
				IsFinal = elem.XGetAttribute ("final") == "true",
				IsReturnEnumified = elem.Attribute ("enumReturn") != null,
				IsStatic = elem.XGetAttribute ("static") == "true",
				JavaName = elem.XGetAttribute ("name"),
				ManagedReturn = elem.XGetAttribute ("managedReturn"),
				PropertyNameOverride = elem.XGetAttribute ("propertyName"),
				Return = elem.XGetAttribute ("return"),
				SourceApiLevel = GetApiLevel (elem.XGetAttribute ("merge.SourceFile")),
				Visibility = elem.Visibility ()
			};

			method.IsVirtual = !method.IsStatic && elem.XGetAttribute ("final") == "false";

			if (elem.Attribute ("managedName") != null)
				method.Name = elem.XGetAttribute ("managedName");
			else
				method.Name = StringRocks.MemberToPascalCase (method.JavaName);

			if (method.IsReturnEnumified) {
				method.ManagedReturn = elem.XGetAttribute ("enumReturn");

				// FIXME: this should not require enumReturn. Somewhere in generator uses this property improperly.
				method.Return = method.ManagedReturn;
			}

			if (declaringType is InterfaceGen)
				method.IsInterfaceDefaultMethod = !method.IsAbstract && !method.IsStatic;

			foreach (var child in elem.Elements ()) {
				if (child.Name == "parameter")
					method.Parameters.Add (CreateParameter (child));
			}

			method.FillReturnType ();

			return method;
		}

		public static Parameter CreateParameter (XElement elem)
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

		public static Parameter CreateParameterFromClassElement (XElement elem)
		{
			string name = "__self";
			string java_type = elem.XGetAttribute ("name");
			string java_package = elem.Parent.XGetAttribute ("name");
			return new Parameter (name, java_package + "." + java_type, null, false);
		}

		static int GetApiLevel (string source)
		{
			if (source == null)
				return 0;

			var m = api_level.Match (source);

			if (!m.Success)
				return 0;

			if (int.TryParse (m.Groups [1].Value, out var api))
				return api;

			return 0;
		}

		static XElement GetPreviousClass (XNode n, string nameValue)
		{
			XElement e = null;

			while (n != null &&
			       ((e = n as XElement) == null ||
				e.Name != "class" ||
				!e.XGetAttribute ("name").StartsWith (nameValue, StringComparison.Ordinal) ||
				// this complicated check (instead of simple name string equivalence match) is required for nested class inside a generic class e.g. android.content.Loader.ForceLoadContentObserver.
				(e.XGetAttribute ("name") != nameValue && e.XGetAttribute ("name").IndexOf ('<') < 0))) {
				n = n.PreviousNode;
			}

			return e;
		}

		static bool IsObfuscatedName (int threshold, string name)
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
			if (!(last.Length == 3 && threshold > 26 * 26 || last.Length == 2 && threshold > 26 || last.Length == 1))
				return false;
			if (last.Any (c => (c < 'a' || 'z' < c) && (c < '0' || '9' < c)))
				return false;
			return true;
		}

		static bool IsPrefixableName (string name)
		{
			// IBlahBlah is not prefixed with 'I'
			return name.Length <= 2 || name [0] != 'I' || !char.IsUpper (name [1]);
		}
	}
}
