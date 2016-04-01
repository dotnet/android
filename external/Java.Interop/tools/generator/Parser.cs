using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Xamarin.Android.Tools;

namespace MonoDroid.Generation {

	public class Parser  {

		public string ApiSource { get; private set; }

		public XmlDocument Load (string filename)
		{
			XmlDocument doc = new XmlDocument ();

			try {
				doc.Load (filename);
			} catch (XmlException e) {
				Report.Verbose (0, "Exception: {0}", e);
				Report.Warning (0, Report.WarningParser + 0, e, "Invalid XML file '{0}': {1}", filename, e.Message);
				doc = null;
			}

			return doc;
		}

		public List<GenBase> Parse (string filename, IEnumerable<string> fixups, string apiLevel, int productVersion)
		{
			return Parse (Load (filename), fixups, apiLevel, productVersion);
		}

		public List<GenBase> Parse (XmlDocument doc, IEnumerable<string> fixups, string apiLevel, int productVersion)
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

			XmlElement root = doc.DocumentElement;

			if ((root == null) || !root.HasChildNodes) {
				Report.Warning (0, Report.WarningParser + 1, "No packages found.");
				return null;
			}

			List<GenBase> gens = new List<GenBase> ();

			foreach (XmlNode child in root.ChildNodes) {
				XmlElement elem = child as XmlElement;
				if (elem == null)
					continue;

				switch (child.Name) {
				case "package":
					gens.AddRange (ParsePackage (elem));
					break;
				case "enum":
					ISymbol sym = new EnumSymbol (elem.XGetAttribute ("name"));
					SymbolTable.AddType (elem.XGetAttribute ("name"), sym);
					continue;
				default:
					Report.Warning (0, Report.WarningParser + 2, "Unexpected child node: {0}.", child.Name);
					break;
				}
			}

			return gens;
		}

		List<GenBase> ParsePackage (XmlElement ns)
		{
			return ParsePackage (ns, null);
		}

		List<GenBase> ParsePackage (XmlElement ns, Predicate<XmlElement> p)
		{
			List<GenBase> result = new List<GenBase> ();
			Dictionary<string, GenBase> nested = new Dictionary<string, GenBase> ();
			Dictionary<string, GenBase> by_name = new Dictionary<string, GenBase> ();

			foreach (XmlNode def in ns.ChildNodes) {

				XmlElement elem = def as XmlElement;
				if (elem == null)
					continue;

				string name = elem.XGetAttribute ("name");
				GenBase gen = null;
				switch (def.Name) {
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
					Report.Warning (0, Report.WarningParser + 3, "Unexpected node in package element: {0}.", def.Name);
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
				SymbolTable.AddType (gen);
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
