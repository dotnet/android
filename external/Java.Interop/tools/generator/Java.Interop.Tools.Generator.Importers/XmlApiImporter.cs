using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Java.Interop.Tools.Generator;
using Java.Interop.Tools.JavaCallableWrappers;
using MonoDroid.Utils;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	class XmlApiImporter
	{
		static readonly Regex api_level = new Regex (@"api-(\d+).xml");

		public static List<GenBase> Parse (XDocument doc, CodeGenerationOptions options)
		{
			if (doc is null)
				return null;

			var root = doc.Root;

			if ((root == null) || !root.HasElements) {
				Report.LogCodedWarning (0, Report.WarningNoPackageElements);
				return null;
			}

			var gens = new List<GenBase> ();

			foreach (var elem in root.Elements ()) {
				switch (elem.Name.LocalName) {
					case "package":
						gens.AddRange (ParsePackage (elem, options));
						break;
					case "enum":
						var name = elem.XGetAttribute ("name");
						var sym = new EnumSymbol (name);
						options.SymbolTable.AddType (name, sym);
						continue;
					default:
						Report.LogCodedWarning (0, Report.WarningUnexpectedRootChildNode, elem.Name.ToString ());
						break;
				}
			}

			return gens;
		}

		public static List<GenBase> ParsePackage (XElement ns, CodeGenerationOptions options)
		{
			var result = new List<GenBase> ();
			var nested = new Dictionary<string, GenBase> ();
			var by_name = new Dictionary<string, GenBase> ();

			foreach (var elem in ns.Elements ()) {

				var name = elem.XGetAttribute ("name");
				GenBase gen = null;

				switch (elem.Name.LocalName) {
					case "class":
						if (ShouldBind (elem))
							gen = CreateClass (ns, elem, options);
						break;
					case "interface":
						if (ShouldBind (elem))
							gen = CreateInterface (ns, elem, options);
						break;
					default:
						Report.LogCodedWarning (0, Report.WarningUnexpectedPackageChildNode, elem.Name.ToString ());
						break;
				}

				if (gen is null)
					continue;

				var idx = name.IndexOf ('<');

				if (idx > 0)
					name = name.Substring (0, idx);

				by_name [name] = gen;

				if (name.IndexOf ('.') > 0)
					nested [name] = gen;
				else
					result.Add (gen);
			}

			foreach (var name in nested.Keys) {
				var top_ancestor = name.Substring (0, name.IndexOf ('.'));

				if (by_name.ContainsKey (top_ancestor))
					by_name [top_ancestor].AddNestedType (nested [name]);
				else {
					Report.LogCodedWarning (0, Report.WarningNestedTypeAncestorNotFound, top_ancestor, nested [name].FullName);
					nested [name].Invalidate ();
				}
			}

			return result;
		}

		public static ClassGen CreateClass (XElement pkg, XElement elem, CodeGenerationOptions options)
		{
			var klass = new ClassGen (CreateGenBaseSupport (pkg, elem, options, false)) {
				BaseType = elem.XGetAttribute ("extends"),
				FromXml = true,
				IsAbstract = elem.XGetAttribute ("abstract") == "true",
				IsFinal = elem.XGetAttribute ("final") == "true",
				PeerConstructorPartialMethod = elem.XGetAttribute ("peerConstructorPartialMethod"),
				// Only use an explicitly set XML attribute
				Unnest = elem.XGetAttribute ("unnest") == "true" ? true :
					 elem.XGetAttribute ("unnest") == "false" ? false :
					 !options.SupportNestedInterfaceTypes
			};

			FillApiSince (klass, pkg, elem);
			SetLineInfo (klass, elem, options);

			foreach (var child in elem.Elements ()) {
				switch (child.Name.LocalName) {
					case "implements":
						var iname = child.XGetAttribute ("name-generic-aware");
						iname = iname.Length > 0 ? iname : child.XGetAttribute ("name");
						klass.AddImplementedInterface (iname);
						break;
					case "method":
						if (child.XGetAttribute ("visibility") != "kotlin-internal")
							klass.AddMethod (CreateMethod (klass, child, options));
						break;
					case "constructor":
						if (child.XGetAttribute ("visibility") != "kotlin-internal")
							klass.Ctors.Add (CreateCtor (klass, child, options));
						break;
					case "field":
						if (child.XGetAttribute ("visibility") != "kotlin-internal")
							klass.AddField (CreateField (klass, child, options));
						break;
					case "typeParameters":
						break; // handled at GenBaseSupport
					default:
						Report.LogCodedWarning (0, Report.WarningUnexpectedChild, klass, child.Name.ToString ());
						break;
				}
			}

			return klass;
		}

		public static Ctor CreateCtor (GenBase declaringType, XElement elem, CodeGenerationOptions options = null)
		{
			var ctor = new Ctor (declaringType) {
				AnnotatedVisibility = elem.XGetAttribute ("annotated-visibility"),
				ApiAvailableSince = declaringType.ApiAvailableSince,
				CustomAttributes = elem.XGetAttribute ("customAttributes"),
				Deprecated = elem.Deprecated (),
				DeprecatedSince = elem.XGetAttributeAsIntOrNull ("deprecated-since"),
				GenericArguments = elem.GenericArguments (),
				Name = elem.XGetAttribute ("name"),
				Visibility = elem.Visibility ()
			};

			SetLineInfo (ctor, elem, options);

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
					Report.LogCodedWarning (0, Report.WarningMissingClassForConstructor, ctor, ctor.Name, expectedEnclosingName);
				} else
					ctor.Parameters.AddFirst (CreateParameterFromClassElement (enclosingType, options));
			}

			foreach (var child in elem.Elements ()) {
				if (child.Name == "parameter")
					ctor.Parameters.Add (CreateParameter (child, options));
			}

			ctor.Name = EnsureValidIdentifer (ctor.Name);

			// If declaring type was deprecated earlier than member, use the type's deprecated-since
			if (declaringType.DeprecatedSince.HasValue && declaringType.DeprecatedSince.Value < ctor.DeprecatedSince.GetValueOrDefault (0))
				ctor.DeprecatedSince = declaringType.DeprecatedSince;

			FillApiSince (ctor, elem);

			return ctor;
		}

		public static Field CreateField (GenBase declaringType, XElement elem, CodeGenerationOptions options = null)
		{
			var field = new Field {
				AnnotatedVisibility = elem.XGetAttribute ("annotated-visibility"),
				ApiAvailableSince = declaringType.ApiAvailableSince,
				DeprecatedComment = elem.XGetAttribute ("deprecated"),
				DeprecatedSince = elem.XGetAttributeAsIntOrNull ("deprecated-since"),
				IsAcw = true,
				IsDeprecated = elem.XGetAttribute ("deprecated") != "not deprecated",
				IsDeprecatedError = elem.XGetAttribute ("deprecated-error") == "true",
				IsFinal = elem.XGetAttribute ("final") == "true",
				IsStatic = elem.XGetAttribute ("static") == "true",
				JavaName = elem.XGetAttribute ("name"),
				JniSignature = elem.XGetAttribute ("jni-signature"),
				NotNull = elem.XGetAttribute ("not-null") == "true",
				SetterParameter = CreateParameter (elem, options),
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
			else {
				field.Name = TypeNameUtilities.StudlyCase (char.IsLower (field.JavaName [0]) || field.JavaName.ToLowerInvariant ().ToUpperInvariant () != field.JavaName ? field.JavaName : field.JavaName.ToLowerInvariant ());
				field.Name = EnsureValidIdentifer (field.Name);
			}

			// If declaring type was deprecated earlier than member, use the type's deprecated-since
			if (declaringType.DeprecatedSince.HasValue && declaringType.DeprecatedSince.Value < field.DeprecatedSince.GetValueOrDefault (0))
				field.DeprecatedSince = declaringType.DeprecatedSince;

			FillApiSince (field, elem);
			SetLineInfo (field, elem, options);

			return field;
		}

		public static GenBaseSupport CreateGenBaseSupport (XElement pkg, XElement elem, CodeGenerationOptions opt, bool isInterface)
		{
			var support = new GenBaseSupport {
				AnnotatedVisibility = elem.XGetAttribute ("annotated-visibility"),
				DeprecatedSince = elem.XGetAttributeAsIntOrNull ("deprecated-since"),
				IsAcw = true,
				IsDeprecated = elem.XGetAttribute ("deprecated") != "not deprecated",
				IsGeneratable = true,
				JavaSimpleName = elem.XGetAttribute ("name"),
				PackageName = pkg.XGetAttribute ("name"),
				Visibility = elem.XGetAttribute ("visibility")
			};

			if (elem.Attribute ("skipInvokerMethods")?.Value is string skip)
				foreach (var m in skip.Split (new char [] { ',', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
					support.SkippedInvokerMethods.Add (m);

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
				support.Namespace = opt.GetTransformedNamespace (StringRocks.PackageToPascalCase (support.PackageName));

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
				support.FullName = string.IsNullOrWhiteSpace(support.Namespace) ? support.Name : $"{support.Namespace}.{support.Name}";
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
				support.Name = EnsureValidIdentifer (support.TypeNamePrefix + raw_name);
				var supportNamespace = string.IsNullOrWhiteSpace (support.Namespace) ? string.Empty : $"{support.Namespace}.";
				var supportSimpleName = idx > 0 ? StringRocks.TypeToPascalCase (support.JavaSimpleName.Substring (0, idx + 1)) : string.Empty;
				support.FullName = string.Format ("{0}{1}{2}", supportNamespace, supportSimpleName, support.Name);
			}

			support.IsObfuscated = IsObfuscatedName (pkg.Elements ().Count (), support.JavaSimpleName) && elem.XGetAttribute ("obfuscated") != "false";

			return support;
		}

		public static InterfaceGen CreateInterface (XElement pkg, XElement elem, CodeGenerationOptions options)
		{
			var iface = new InterfaceGen (CreateGenBaseSupport (pkg, elem, options, true)) {
				ArgsType = elem.XGetAttribute ("argsType"),
				HasManagedName = elem.Attribute ("managedName") != null,
				NoAlternatives = elem.XGetAttribute ("no-alternatives") == "true",
				// Only use an explicitly set XML attribute
				Unnest = elem.XGetAttribute ("unnest") == "true" ? true :
					 elem.XGetAttribute ("unnest") == "false" ? false :
					 !options.SupportNestedInterfaceTypes
			};

			FillApiSince (iface, pkg, elem);
			SetLineInfo (iface, elem, options);

			foreach (var child in elem.Elements ()) {
				switch (child.Name.LocalName) {
					case "implements":
						string iname = child.XGetAttribute ("name-generic-aware");
						iname = iname.Length > 0 ? iname : child.XGetAttribute ("name");
						iface.AddImplementedInterface (iname);
						break;
					case "method":
						if (child.XGetAttribute ("visibility") != "kotlin-internal")
							iface.AddMethod (CreateMethod (iface, child, options));
						break;
					case "field":
						if (child.XGetAttribute ("visibility") != "kotlin-internal")
							iface.AddField (CreateField (iface, child, options));
						break;
					case "typeParameters":
						break; // handled at GenBaseSupport
					default:
						Report.LogCodedWarning (0, Report.WarningUnexpectedInterfaceChild, iface, child.ToString ());
						break;
				}
			}

			return iface;
		}

		public static Method CreateMethod (GenBase declaringType, XElement elem, CodeGenerationOptions options = null)
		{
			var method = new Method (declaringType) {
				AnnotatedVisibility = elem.XGetAttribute ("annotated-visibility"),
				ApiAvailableSince = declaringType.ApiAvailableSince,
				ArgsType = elem.Attribute ("argsType")?.Value,
				CustomAttributes = elem.XGetAttribute ("customAttributes"),
				Deprecated = elem.Deprecated (),
				DeprecatedSince = elem.XGetAttributeAsIntOrNull ("deprecated-since"),
				ExplicitInterface = elem.XGetAttribute ("explicitInterface"),
				EventName = elem.Attribute ("eventName")?.Value,
				GenerateAsyncWrapper = elem.Attribute ("generateAsyncWrapper") != null,
				GenerateDispatchingSetter = elem.Attribute ("generateDispatchingSetter") != null,
				GenericArguments = elem.GenericArguments (),
				IsAbstract = elem.XGetAttribute ("abstract") == "true",
				IsAcw = true,
				IsCompatVirtualMethod = elem.XGetAttribute ("compatVirtualMethod") == "true",
				IsFinal = elem.XGetAttribute ("final") == "true",
				IsReturnEnumified = elem.Attribute ("enumReturn") != null,
				IsStatic = elem.XGetAttribute ("static") == "true",
				JavaName = elem.XGetAttribute ("name"),
				ManagedOverride = elem.XGetAttribute ("managedOverride"),
				ManagedReturn = elem.XGetAttribute ("managedReturn"),
				PropertyNameOverride = elem.XGetAttribute ("propertyName"),
				Return = elem.XGetAttribute ("return"),
				ReturnNotNull = elem.XGetAttribute ("return-not-null") == "true",
				SourceApiLevel = GetApiLevel (elem.XGetAttribute ("merge.SourceFile")),
				Visibility = elem.Visibility ()
			};

			// CompatVirtualMethods aren't abstract
			if (method.IsCompatVirtualMethod)
				method.IsAbstract = false;

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
					method.Parameters.Add (CreateParameter (child, options));
			}

			method.Name = EnsureValidIdentifer (method.Name);

			method.FillReturnType ();

			// If declaring type was deprecated earlier than member, use the type's deprecated-since
			if (declaringType.DeprecatedSince.HasValue && declaringType.DeprecatedSince.Value < method.DeprecatedSince.GetValueOrDefault (0))
				method.DeprecatedSince = declaringType.DeprecatedSince;

			FillApiSince (method, elem);
			SetLineInfo (method, elem, options);

			return method;
		}

		public static Parameter CreateParameter (XElement elem, CodeGenerationOptions options = null)
		{
			string managedName = elem.XGetAttribute ("managedName");
			string name = !string.IsNullOrEmpty (managedName) ? managedName : TypeNameUtilities.MangleName (EnsureValidIdentifer (elem.XGetAttribute ("name")));
			string java_type = elem.XGetAttribute ("type");
			string enum_type = elem.Attribute ("enumType") != null ? elem.XGetAttribute ("enumType") : null;
			string managed_type = elem.Attribute ("managedType") != null ? elem.XGetAttribute ("managedType") : null;
			var not_null = elem.XGetAttribute ("not-null") == "true";
			// FIXME: "enum_type ?? java_type" should be extraneous. Somewhere in generator uses it improperly.
			var result = new Parameter (name, enum_type ?? java_type, enum_type ?? managed_type, enum_type != null, java_type, not_null);
			if (elem.Attribute ("sender") != null)
				result.IsSender = true;
			SetLineInfo (result, elem, options);
			return result;
		}

		public static Parameter CreateParameterFromClassElement (XElement elem, CodeGenerationOptions options)
		{
			string name = "__self";
			string java_type = elem.XGetAttribute ("name");
			string java_package = elem.Parent.XGetAttribute ("name");
			var p = new Parameter (name, java_package + "." + java_type, null, false);

			SetLineInfo (p, elem, options);
			return p;
		}

		static string EnsureValidIdentifer (string name)
		{
			if (string.IsNullOrWhiteSpace (name))
				return name;

			name = IdentifierValidator.CreateValidIdentifier (name);

			if (char.IsNumber (name [0]))
				name = $"_{name}";

			return name;
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

		// The array here allows members to inherit defaults from their parent, but
		// override them if they were added later.
		// For example:
		// - <package api-since="21">
		//   - <class api-since="24">
		//     - <method api-since="28">
		// Elements need to be passed in the above order. (package, class, member)
		static void FillApiSince (ApiVersionsSupport.IApiAvailability model, params XElement[] elems)
		{
			foreach (var elem in elems)
				if (int.TryParse (elem.XGetAttribute ("api-since"), out var result))
					model.ApiAvailableSince = result;
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

		static void SetLineInfo (ISourceLineInfo model, XNode node, CodeGenerationOptions options)
		{
			model.SourceFile = options?.ApiXmlFile;

			if (node is IXmlLineInfo info && info.HasLineInfo ()) {
				model.LineNumber = info.LineNumber;
				model.LinePosition = info.LinePosition;
			}
		}

		static bool ShouldBind (XElement elem)
		{
			// Don't bind things the user has said are "obfuscated"
			if (elem.XGetAttribute ("obfuscated") == "true")
				return false;

			var java_name = elem.XGetAttribute ("name");

			// Ignore types that do not have a name (nested classes would end in a period like "Document.")
			if (!java_name.HasValue () || java_name.EndsWith (".", StringComparison.Ordinal))
				return false;

			return true;
		}
	}
}
