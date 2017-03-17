using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using MonoDroid.Utils;

using Android.Runtime;

using Mono.Cecil;

using Type = Mono.Cecil.TypeDefinition;//IKVM.Reflection.Type;
using MemberInfo = Mono.Cecil.IMemberDefinition;
using MethodInfo = Mono.Cecil.MethodDefinition;
using PropertyInfo = Mono.Cecil.PropertyDefinition;
using ParameterInfo = Mono.Cecil.ParameterDefinition;

namespace Xamarin.Android.Tools.JavaDocToMdoc
{

	abstract class MdocHelper
	{

		Dictionary<string, Func<XElement, bool, object>> HtmlToMdocElementMapping;

		Regex typeInHref;

		public abstract string TypeInHrefPrefix { get; }

		protected MdocHelper ()
		{
			typeInHref = new Regex (TypeInHrefPrefix + @"(?<type>.*)\.html(#(?<member>.*))?$");

			HtmlToMdocElementMapping = new Dictionary<string, Func<XElement, bool, object>> {
			{ "a",      (e, i) => ConvertLink (e, i) },
			{ "code",   (e, i) => ConvertCode (e, i) },
			{ "div",    (e, i) => FromHtml (e.Nodes (), i) },
			{ "em",     (e, i) => new XElement ("i", FromHtml (e.Nodes (), i)) },
			{ "li",     (e, i) => new XElement ("item", new XElement ("term", FromHtml (e.Nodes (), i))) },
			{ "ol",     (e, i) => new XElement ("list", new XAttribute ("type", "number"), FromHtml (e.Nodes ())) },
			{ "p",      (e, i) => new XElement ("para", new XAttribute ("tool", "javadoc-to-mdoc"), FromHtml (e.Nodes (), i)) },
			{ "span",   (e, i) => FromHtml (e.Nodes (), i) },
			{ "strong", (e, i) => new XElement ("i", FromHtml (e.Nodes (), i)) },
			{ "tt",     (e, i) => new XElement ("c", FromHtml (e.Nodes (), i)) },
			{ "ul",     (e, i) => new XElement ("list", new XAttribute ("type", "bullet"), FromHtml (e.Nodes ())) },
			{ "pre",    (e, i) => ConvertPre (e, i) }
			};
		}

		public string CrefFromHref (string href)
		{
			var m = typeInHref.Match (href);
			if (!m.Success)
				return string.Format ("!:BadHref:{0}", href);
			string jniTypeName = m.Groups ["type"].Value.Replace ('.', '$');
			if (jniTypeName.EndsWith ("package-summary"))
				return CreateNamespaceCref (jniTypeName);
			Type type = GetAvailableTypes ()
				.FirstOrDefault (t => {
					var r = (RegisterAttribute [])t.GetCustomAttributes<RegisterAttribute> (false);
					if (r.Length == 0)
						return false;
					return r [0].Name == jniTypeName;
				});
			if (type == null)
				return string.Format ("!:NoType:{0};Href={1}", jniTypeName, href);
			string member = m.Groups ["member"].Value;
			if (string.IsNullOrEmpty (member))
				return "T:" + type.FullName.Replace ('/', '+');
			member = TypeUtilities.StripGenericArgument (member);

			var tregister = ((RegisterAttribute [])type.GetCustomAttributes<RegisterAttribute> (false)) [0];
			MemberInfo mi = type.Fields.Cast<MemberInfo> ().Concat (type.Properties).Concat (type.Methods)
				.FirstOrDefault (x => {
					var ras = (RegisterAttribute [])x.GetCustomAttributes<RegisterAttribute> (false);
					return ras.Any (ra => Application.GetAnchor (tregister, ra) == member);
				});
			if (mi != null)
				return CreateCref (mi);

			string enumRef;
			if (Application.EnumMappings != null && Application.EnumMappings.TryGetValue (jniTypeName + "." + member, out enumRef))
				return enumRef;
			return "!:" + type.FullName + "." + member;
		}

		IEnumerable<Type> GetAvailableTypes ()
		{
			var assemblies = Application.Assemblies/*
				.SelectMany (a => a.MainModule.AssemblyReferences)
				.Select (an =>
						{try
							{return AssemblyDefinition.ReadAssembly (an.Name); }
						catch
							{return (Assembly) null;}})
				.Where (a => a != null)
				.Concat (Application.Assemblies)*/;
			return assemblies.SelectMany (a => a.Modules.SelectMany (m => m.Types).SelectMany (t => t.FlattenTypeHierarchy ()));
		}

		string CreateNamespaceCref (string jniTypeName)
		{
			string package = jniTypeName.Substring (0, jniTypeName.LastIndexOf ('/'));
			package = package.Replace ('/', '.');
			if (Application.PackageRenames.ContainsKey (package))
				package = Application.PackageRenames [package];
			return "N:" + StringRocks.PackageToPascalCase (package);
		}

		PropertyInfo GetPropertyForMember (MemberInfo member)
		{
			var method = member as MethodInfo;
			if (method == null || !method.IsSpecialName)
				return null;
			foreach (var p in method.DeclaringType.Properties) {
				if (p.GetMethod == method || p.SetMethod == method)
					return p;
			}
			return null;
		}

		string CreateCref (MemberInfo member)
		{
			member = GetPropertyForMember (member) ?? member;
			var cref = new StringBuilder ();
			switch (member.GetMemberType ()) {
			case "Constructor": cref.Append ("C"); break;
			case "Event": cref.Append ("E"); break;
			case "Field": cref.Append ("F"); break;
			case "Method": cref.Append ("M"); break;
			case "Property": cref.Append ("P"); break;
			case "Type": cref.Append ("T"); break;
			default:
				throw new InvalidOperationException ("Unable to create cref for member: " + member + ".");
			}
			cref.Append (":");

			AppendMemberRef (cref, member);

			return cref.ToString ();
		}

		StringBuilder AppendMemberRef (StringBuilder cref, TypeReference type)
		{
			cref.Append (type.Namespace).Append ('.').Append (type.Name);
			return cref;
		}

		StringBuilder AppendMemberRef (StringBuilder cref, MemberInfo member)
		{
			switch (member.GetMemberType ()) {
			case "Type": {
					Type type = (Type)member;
					cref.Append (type.Namespace).Append ('.').Append (type.Name);
					return cref;
				}
			default: AppendMemberRef (cref, (MemberInfo)member.DeclaringType); break;
			}

			if (member.GetMemberType () != "Constructor")
				cref.Append (".").Append (member.Name);

			Mono.Collections.Generic.Collection<ParameterInfo> p = null;
			if (member is MethodDefinition)
				p = ((MethodDefinition)member).Parameters;
			else if (member is PropertyDefinition)
				p = ((PropertyInfo)member).GetMethod.Parameters;
			if (p != null && p.Count > 0) {
				cref.Append ("(");
				AppendMemberRef (cref, p [0].ParameterType);
				for (int i = 1; i < p.Count; ++i) {
					cref.Append (", ");
					AppendMemberRef (cref, p [1].ParameterType);
				}
				cref.Append (")");
			}

			return cref;
		}

		public string CrefFromJavaType (string javaType)
		{
			Type type = Application.Assemblies.SelectMany (a => a.Modules.SelectMany (m => m.Types))
				.FirstOrDefault (t => {
					var r = (RegisterAttribute [])t.GetCustomAttributes<RegisterAttribute> (false);
					if (r.Length == 0)
						return false;
					return r [0].Name.Replace ('/', '.').Replace ('$', '.') == javaType;
				});
			if (type == null)
				return string.Format ("!:NoType:{0}", javaType);

			return "T:" + type.FullName;
		}

		object ConvertLink (XElement e, bool insideFormat)
		{
			var href = e.Attribute ("href");
			if (href == null)
				return "";
			var cref = CrefFromHref (href.Value);
			if (!cref.StartsWith ("!:BadHref"))
				return new XElement ("see",
						new XAttribute ("cref", cref));
			int packageStart = href.Value.LastIndexOf ("../");
			if (packageStart < 0)
				return "";
			var url = href.Value.Substring (packageStart + "../".Length);
			url = Application.OnlineDocumentationPrefix + "../" + url;
			var a = new XElement ("a", new XAttribute ("href", url),
					e.Value);
			return insideFormat
				? a
				: new XElement ("format", new XAttribute ("type", "text/html"),
						a);
		}

		object ConvertPre (XElement e, bool insideFormat)
		{
			string content = e.Value;
			XElement result = null;
			SampleDesc desc;
			string snippet;

			if (!Application.ProcessingContext.ImportSamples)
				return null;
			if ((snippet = Application.Samples.GetSampleFromContent (content, out desc)) != null) {
				result = new XElement ("example",
						new XElement ("code",
							new XAttribute ("lang", desc.Language ?? "java"),
							// new XAttribute (XNamespace.Xml + "space", "preserve"),
							snippet));
			} else {
				string language = content.TrimStart ();
				language = (string.IsNullOrEmpty (language) || language [0] != '<')
					? "java" : "xml";
				SampleDesc d = new SampleDesc {
					Language = language,
					FullTypeName = Application.ProcessingContext.CurrentType.FullName,
					DocumentationFilePath = Application.ProcessingContext.CurrentFilePath
				};
				var id = Application.Samples.RegisterSample (content, d);
				result = new XElement ("sample", new XAttribute ("external-id", id));
			}
			return result;
		}

		XElement ConvertCode (XElement e, bool insideFormat)
		{
			if (e.Value == "YES")
				return new XElement ("see", new XAttribute ("langword", "true"));
			if (e.Value == "NO")
				return new XElement ("see", new XAttribute ("langword", "false"));
			return new XElement ("c", FromHtml (e.Nodes (), insideFormat));
		}

		public IEnumerable<object> FromHtml (IEnumerable<XNode> rest)
		{
			return FromHtml (rest, false);
		}

		public IEnumerable<object> FromHtml (IEnumerable<XNode> rest, bool insideFormat)
		{
			if (rest != null)
				foreach (var e in rest)
					yield return FromHtml (e, insideFormat);
		}

		public object FromHtml (XElement e)
		{
			return FromHtml (e, false);
		}

		public object FromHtml (XNode n, bool insideFormat)
		{
			// Try to intelligently convert HTML into mdoc(5).
			object r = null;
			var e = n as XElement;
			if (e != null && HtmlToMdocElementMapping.ContainsKey (e.Name.LocalName))
				r = HtmlToMdocElementMapping [e.Name.LocalName] (e, insideFormat);
			else if (e != null && !insideFormat)
				r = new XElement ("format",
						new XAttribute ("type", "text/html"),
						FromHtml (e, true));
			else if (e != null)
				r = new XElement (e.Name,
						e.Attributes (),
						FromHtml (e.Nodes (), insideFormat));
			else
				r = n;
			return r;
		}

		public object FromHtml (XElement e, IEnumerable<XElement> rest)
		{
			return FromHtml (new [] { e }.Concat (rest).Cast<XNode> (), false);
		}
	}

	class DroidDocMdocHelper : MdocHelper
	{
		public override string TypeInHrefPrefix { get { return "reference\\/"; } }
	}

	class JavaDocMdocHelper : MdocHelper
	{
		public override string TypeInHrefPrefix { get { return @"(\.\.\/)*"; } }
	}
}
