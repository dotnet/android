using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;

using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	public class ApiFixup
	{
		XDocument api_doc;
		string apiSource = "";

		public string ApiSource { get { return apiSource; } }
		
		public ApiFixup (XDocument apiDoc)
		{
			api_doc = apiDoc;
			var api = api_doc.Root;
			if (api != null)
				apiSource = api.XGetAttribute ("api-source");
		}

		public void Process (IEnumerable<XDocument> metaDocs, string apiLevel, int productVersion)
		{
			foreach (var metaDoc in metaDocs)
				Process (metaDoc, apiLevel, productVersion);
		}
		
		bool ShouldSkip (XElement node, int apiLevel, int productVersion)
		{
			if (apiLevel > 0) {
				string apiSince = node.XGetAttribute ("api-since");
				string apiUntil = node.XGetAttribute ("api-until");
				if (!string.IsNullOrEmpty (apiSince) && int.Parse (apiSince) > apiLevel)
					return true;
				if (!string.IsNullOrEmpty (apiUntil) && int.Parse (apiUntil) < apiLevel)
					return true;
			}
			if (productVersion > 0) {
				var product_version = node.XGetAttribute ("product-version");
				if (!string.IsNullOrEmpty (product_version) && int.Parse (product_version) > productVersion)
					return true;
			}
			return false;
		}

		bool ShouldApply (XElement node) 
		{
			if (!string.IsNullOrEmpty (apiSource)) {
				var targetsource = node.XGetAttribute ("api-source");
				if (string.IsNullOrEmpty (targetsource))
						return true;
				return targetsource == apiSource;
			}
			return true;
		}
		
		void Process (XDocument meta_doc, string apiLevelString, int productVersion)
		{
			int apiLevel = 0;
			int.TryParse (apiLevelString, out apiLevel);

			var metadataChildren = meta_doc.XPathSelectElements ("/metadata/*");
			string prev_path = null;
			XElement attr_last_cache = null;

			foreach (var metaitem in metadataChildren) {
				if (ShouldSkip (metaitem, apiLevel, productVersion))
					continue;
				if (!ShouldApply (metaitem))
					continue;
				string path = metaitem.XGetAttribute ("path");
				if (path != prev_path)
					attr_last_cache = null;
				prev_path = path;

				switch (metaitem.Name.LocalName) {
				case "remove-node":
					try {
						var nodes = api_doc.XPathSelectElements (path).ToArray ();
						if (nodes.Any ())
							foreach (var node in nodes)
								node.Remove ();
						else
							// BG8A00
							Report.Warning (0, Report.WarningApiFixup + 0, null, metaitem, "<remove-node path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A01
						Report.Error (Report.ErrorApiFixup + 1, e, metaitem, "Invalid XPath specification: {0}", path);
					}
					break;
				case "add-node":
					try {
						var nodes = api_doc.XPathSelectElements (path);
						if (!nodes.Any ())
							// BG8A01
							Report.Warning (0, Report.WarningApiFixup + 1, null, metaitem, "<add-node path=\"{0}\"/> matched no nodes.", path);
						else {
							foreach (var node in nodes)
								node.Add (metaitem.Nodes ());
						}
					} catch (XPathException e) {
						// BG4A02
						Report.Error (Report.ErrorApiFixup + 2, e, metaitem, "Invalid XPath specification: {0}", path);
					}
					break;
				case "change-node":
					try {
						var nodes = api_doc.XPathSelectElements (path);
						bool matched = false;
						foreach (var node in nodes) {
							var newChild = new XElement (metaitem.Value);
							newChild.Add (node.Attributes ());
							newChild.Add (node.Nodes ());
							node.ReplaceWith (newChild);
							matched = true;
						}
						
						if (!matched)
							// BG8A03
							Report.Warning (0, Report.WarningApiFixup + 3, null, metaitem, "<change-node-type path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A03
						Report.Error (Report.ErrorApiFixup + 3, e, metaitem, "Invalid XPath specification: {0}", path);
					}
					break;
				case "attr":
					try {
						string attr_name = metaitem.XGetAttribute ("name");
						if (string.IsNullOrEmpty (attr_name))
							// BG4A07
							Report.Error (Report.ErrorApiFixup + 7, null, metaitem, "Target attribute name is not specified for path: {0}", path);
						var nodes = attr_last_cache != null ? new XElement [] { attr_last_cache } : api_doc.XPathSelectElements (path);
						int attr_matched = 0;
						foreach (var n in nodes) {
							n.SetAttributeValue (attr_name, metaitem.Value);
							attr_matched++;
						}
						if (attr_matched == 0)
							// BG8A04
							Report.Warning (0, Report.WarningApiFixup + 4, null, metaitem, "<attr path=\"{0}\"/> matched no nodes.", path);
						if (attr_matched != 1)
							attr_last_cache = null;
					} catch (XPathException e) {
						// BG4A04
						Report.Error (Report.ErrorApiFixup + 4, e, metaitem, "Invalid XPath specification: {0}", path);
					}
					break;
				case "move-node":
					try {
						string parent = metaitem.Value;
						var parents = api_doc.XPathSelectElements (parent);
						bool matched = false;
						foreach (var parent_node in parents) {
							var nodes = parent_node.XPathSelectElements (path).ToArray ();
							foreach (var node in nodes)
								node.Remove ();
							parent_node.Add (nodes);
							matched = true;
						}
						if (!matched)
							// BG8A05
							Report.Warning (0, Report.WarningApiFixup + 5, null, metaitem, "<move-node path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A05
						Report.Error (Report.ErrorApiFixup + 5, e, metaitem, "Invalid XPath specification: {0}", path);
					}
					break;
				case "remove-attr":
					try {
						string name = metaitem.XGetAttribute ("name");
						var nodes = api_doc.XPathSelectElements (path);
						bool matched = false;

						foreach (var node in nodes) {
							node.RemoveAttributes ();
							matched = true;
						}
						
						if (!matched)
							// BG8A06
							Report.Warning (0, Report.WarningApiFixup + 6, null, metaitem, "<remove-attr path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A06
						Report.Error (Report.ErrorApiFixup + 6, e, metaitem, "Invalid XPath specification: {0}", path);
					}
					break;
				}
			}
		}
	}
}

