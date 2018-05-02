using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	public class Parser
	{
		readonly CodeGenerationOptions opt;

		public Parser (CodeGenerationOptions opt)
		{
			this.opt = opt;
		}

		public string ApiSource { get; private set; }

		public XDocument Load (string filename)
		{
			XDocument doc = null;

			try {
				doc = XDocument.Load (filename, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
			} catch (XmlException e) {
				Report.Verbose (0, "Exception: {0}", e);
				Report.Warning (0, Report.WarningParser + 0, e, "Invalid XML file '{0}': {1}", filename, e.Message);
			}

			return doc;
		}

		public List<GenBase> Parse (string filename, IEnumerable<string> fixups, string apiLevel, int productVersion)
		{
			var doc = Load (filename);
			try {
				return Parse (doc, fixups, apiLevel, productVersion);
			} finally {
				try {
					doc.Save (filename + ".fixed");
				} catch { } // skip any error here.
			}
		}

		public List<GenBase> Parse (XDocument doc, IEnumerable<string> fixups, string apiLevel, int productVersion)
		{
			if (doc == null)
				return null;
			try {
				var apiFixup = new ApiFixup (doc);
				apiFixup.Process (from fixup in fixups select Load (fixup), apiLevel, productVersion);
				ApiSource = apiFixup.ApiSource;
			} catch (XmlException ex) {
				// BG4200
				Report.Error (Report.ErrorParser + 0, ex, "Error during processing metadata fixup: {0}", ex.Message);
				return null;
			}

			var root = doc.Root;

			if ((root == null) || !root.HasElements) {
				Report.Warning (0, Report.WarningParser + 1, "No packages found.");
				return null;
			}

			List<GenBase> gens = new List<GenBase> ();

			foreach (var elem in root.Elements ()) {
				switch (elem.Name.LocalName) {
				case "package":
					gens.AddRange (ParsePackage (elem));
					break;
				case "enum":
					ISymbol sym = new EnumSymbol (elem.XGetAttribute ("name"));
					opt.SymbolTable.AddType (elem.XGetAttribute ("name"), sym);
					continue;
				default:
					Report.Warning (0, Report.WarningParser + 2, "Unexpected child node: {0}.", elem.Name);
					break;
				}
			}

			return gens;
		}

		List<GenBase> ParsePackage (XElement ns)
		{
			return ParsePackage (ns, null);
		}

		List<GenBase> ParsePackage (XElement ns, Predicate<XElement> p)
		{
			List<GenBase> result = new List<GenBase> ();
			Dictionary<string, GenBase> nested = new Dictionary<string, GenBase> ();
			Dictionary<string, GenBase> by_name = new Dictionary<string, GenBase> ();

			foreach (var elem in ns.Elements ()) {

				string name = elem.XGetAttribute ("name");
				GenBase gen = null;
				switch (elem.Name.LocalName) {
				case "class":
					if (elem.XGetAttribute ("obfuscated") == "true")
						continue;
					gen = new XmlClassGen (ns, elem);
					break;
				case "interface":
					if (elem.XGetAttribute ("obfuscated") == "true")
						continue;
					gen = new XmlInterfaceGen (ns, elem);
					break;
				default:
					Report.Warning (0, Report.WarningParser + 3, "Unexpected node in package element: {0}.", elem.Name);
					break;
				}

				if (gen == null)
					continue;
				int idx = name.IndexOf ('<');
				if (idx > 0)
					name = name.Substring (0, idx);
				by_name [name] = gen;
				if (name.IndexOf ('.') > 0)
					nested [name] = gen;
				else
					result.Add (gen);
			}

			foreach (string name in nested.Keys) {
				string top_ancestor = name.Substring (0, name.IndexOf ('.'));
				if (by_name.ContainsKey (top_ancestor))
					by_name [top_ancestor].AddNestedType (nested [name]);
				else {
					Report.Warning (0, Report.WarningParser + 4, "top ancestor {0} not found for nested type {1}.", top_ancestor, nested [name].FullName);
					nested [name].Invalidate ();
				}
			}
			return result;
		}
	}
}
