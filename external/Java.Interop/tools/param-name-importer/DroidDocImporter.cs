using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Xamarin.Android.Tools.ApiXmlAdjuster;

namespace Xamarin.Android.ApiTools.DroidDocImporter
{
	public class DroidDocScrapingImporter
	{
		static bool ClassContains (XElement e, string cls)
		{
			return e.Attribute ("class")?.Value?.Split (' ')?.Contains (cls) == true;
		}

		static readonly string [] excludes = new string [] {
			"classes.html",
			"hierarchy.html",
			"index.html",
			"package-summary.html",
			"packages-wearable-support.html",
			"packages-wearable-support.html",
		};
		static readonly string [] non_frameworks = new string [] {
			"android.support.",
			"com.google.android.gms.",
			"renderscript."
		};

		/*

		The DroidDoc format from API Level 16 to 23, the format is:

		- All pages have ToC links and body (unlike standard JavaDoc which is based on HTML frames).
		- The actual doc section is a div element whose id is "doc-col".
		- The "doc-col" div element has a section div element whose id is "jd-header" and another one with id "jd-content".
		- "jd-header" div element contains the type signature (modifiers, name, and inheritance).
		  - Here we care only about type name and kind (whether it is a class or interface).
		    - Generic arguments are insignificant.
		- In the following terms I explain the "correct" (or "expected") document structure, but in fact
		  Google completely broke it and it is impossible to retrieve the document tree like this.
		  We workaround this issue by changing the strategy "iterate children of 'jd-content'"
		  with "iterate descendants of 'jd-content'"... It occurs only in API Level 15 or later.
		  - "jd-content" div element contains a collection of sections. Each section consists of:
		    - an "h2" element whose value text indicates the section name ("Public Constructors", "Protected Methods" etc.)
		      - There was an issue in javax/xml/validation/SchemaFactory.html in API Level 15 that the method details contain
		        "h2" and confuses the parser. To workaround this, we accept only limited kind of values.
		    - the content, which follows the h2 element.
		  - The section content is a collection of members. Each member consists of:
		    - an anchor ("A") element with "name" attribute, and
		    - a div element which contains an h4 child element whose class contains "jd-details-title".
		  - The h4 element contains the member signature. We parse it and retrieve the method name and list of parameters.
		    - Parameters are tokenized by ", ".
		    - Note that the splitter contains a white space which disambiguates any use of generic arguments (we don't want to split "Foo<K,V> bar" as "Foo<K" and "V> bar")

		API Level 10 to 15 has slightly different format:

		- There is no "doc-col" element. But "jd-header" and "jd-content" are still alive.

		*/
		public void Import (ImporterOptions options)
		{
			options.DiagnosticWriter.WriteLine (options.DocumentDirectory);

			string referenceDocsTopDir = Path.Combine (options.DocumentDirectory, "reference");
			var htmlFiles = Directory.GetDirectories (referenceDocsTopDir).SelectMany (d => Directory.GetFiles (d, "*.html", SearchOption.AllDirectories));

			var api = new JavaApi ();

			foreach (var htmlFile in htmlFiles) {

				// skip irrelevant files.
				if (excludes.Any (x => htmlFile.EndsWith (x, StringComparison.OrdinalIgnoreCase)))
					continue;
				var packageName = Path.GetDirectoryName (htmlFile).Substring (referenceDocsTopDir.Length + 1).Replace ('/', '.');
				if (options.FrameworkOnly && non_frameworks.Any (n => packageName.StartsWith (n, StringComparison.Ordinal)))
					continue;

				options.DiagnosticWriter.WriteLine ("-- " + htmlFile);

				var doc = new HtmlLoader ().GetJavaDocFile (htmlFile);

				var header = doc.Descendants ().FirstOrDefault (e => e.Attribute ("id")?.Value == "jd-header");
				var content = doc.Descendants ().FirstOrDefault (e => e.Attribute ("id")?.Value == "jd-content");

				if (header == null || content == null)
					continue;

				var apiSignatureTokens = header.Value.Replace ('\r', ' ').Replace ('\n', ' ').Replace ('\t', ' ').Trim ();
				if (apiSignatureTokens.Contains ("extends "))
					apiSignatureTokens = apiSignatureTokens.Substring (0, apiSignatureTokens.IndexOf ("extends ", StringComparison.Ordinal)).Trim ();
				if (apiSignatureTokens.Contains ("implements "))
					apiSignatureTokens = apiSignatureTokens.Substring (0, apiSignatureTokens.IndexOf ("implements ", StringComparison.Ordinal)).Trim ();
				bool isClass = apiSignatureTokens.Contains ("class");
				options.DiagnosticWriter.WriteLine (apiSignatureTokens);

				var javaPackage = api.AllPackages.FirstOrDefault (p => p.Name == packageName);
				if (javaPackage == null) {
					javaPackage = new JavaPackage (api) { Name = packageName };
					api.Packages.Add (packageName, javaPackage);
				}

				var javaType = isClass ? (JavaType)new JavaClass (javaPackage) : new JavaInterface (javaPackage);
				javaType.Name = apiSignatureTokens.Substring (apiSignatureTokens.LastIndexOf (' ') + 1);
				javaPackage.AddType (javaType);

				string sectionType = null;
				var sep = new string [] { ", " };
				var ssep = new char [] { ' ' };
				foreach (var child in content.Descendants ()) {
					if (child.Name == "h2") {
						var value = child.Value;
						switch (value) {
						case "Public Constructors":
						case "Protected Constructors":
						case "Public Methods":
						case "Protected Methods":
							sectionType = value;
							break;
						}
						continue;
					}

					if (sectionType == null)
						continue;

					if (child.Name != "a" || child.Attribute ("name") == null)
						continue;

					var h4 = child.XPathSelectElement ("following-sibling::div/h4[contains(@class, 'jd-details-title')]");
					if (h4 == null)
						continue;

					string sigTypeOnly = child.Attribute ("name").Value;
					string sigTypeAndName = h4.Value.Replace ('\n', ' ').Replace ('\r', ' ').Trim ();
					if (!sigTypeAndName.Contains ('('))
						continue;
					JavaMethodBase javaMethod = null;
					string name = sigTypeAndName.Substring (0, sigTypeAndName.IndexOf ('(')).Split (ssep, StringSplitOptions.RemoveEmptyEntries).Last ();
					switch (sectionType) {
					case "Public Constructors":
					case "Protected Constructors":
						javaMethod = new JavaConstructor (javaType) { Name = name };
						break;
					case "Public Methods":
					case "Protected Methods":
						string mname = sigTypeAndName.Substring (0, sigTypeAndName.IndexOf ('('));
						javaMethod = new JavaMethod (javaType) { Name = name };
						break;
					}
					javaType.Members.Add (javaMethod);

					var paramTypes = SplitTypes (sigTypeOnly.Substring (sigTypeOnly.IndexOf ('(') + 1).TrimEnd (')'), 0).ToArray ();
					var parameters = sigTypeAndName.Substring (sigTypeAndName.IndexOf ('(') + 1).TrimEnd (')')
							    .Split (sep, StringSplitOptions.RemoveEmptyEntries)
							    .Select (s => s.Trim ())
							    .ToArray ();
					foreach (var p in paramTypes.Zip (parameters, (to, tn) => new { Type = to, TypeAndName = tn })
					         .Select (pp => new { Type = pp.Type, Name = pp.TypeAndName.Split (' ') [1] }))
						javaMethod.Parameters.Add (new JavaParameter (javaMethod) { Name = p.Name, Type = p.Type });
				}
				javaType.Members = javaType.Members.OfType<JavaMethodBase> ()
					.OrderBy (m => m.Name + "(" + string.Join (",", m.Parameters.Select (p => p.Type)) + ")")
					.ToArray ();
			}

			if (options.OutputTextFile != null)
				api.WriteParameterNamesText (options.OutputTextFile);
			if (options.OutputXmlFile != null)
				api.WriteParameterNamesXml (options.OutputXmlFile);
		}

		IEnumerable<string> SplitTypes (string types, int start)
		{
			if (start == types.Length)
				yield break;
			var p2 = types.IndexOf (',', start);
			if (p2 < 0) {
				yield return types.Substring (start);
				yield break;
			}

			var p1 = types.IndexOf ('<', start);
			if (p1 < 0 || p2 < p1) {
				yield return types.Substring (start, p2 - start);
				foreach (var s in SplitTypes (types, p2 + ", ".Length))
					yield return s;
				yield break;
			}

			int open = 1;
			for (int i = p1 + 1; i < types.Length; i++) {
				switch (types [i]) {
				case '<':
					open++;
					break;
				case '>':
					open--;
					if (open == 0) {
						i++; // will be positioned either at ',' or at the end.
						yield return types.Substring (start, i - start);
						if (i + 2 < types.Length && types [i] == ',' && types [i + 1] == ' ') // skip ", "
							i += 2;
						if (i < types.Length)
							foreach (var s in SplitTypes (types, i))
								yield return s;
						yield break;
					}
					break;
				}
			}
			throw new ArgumentException ("Unexpected list of parameters: " + types);
		}
	}

}
