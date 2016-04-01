using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;

using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	public class ApiFixup
	{
		XmlDocument api_doc;
		string apiSource = "";

		public string ApiSource { get { return apiSource; } }
		
		public ApiFixup (XmlDocument apiDoc)
		{
			api_doc = apiDoc;
			var api = api_doc.DocumentElement;
			if (api != null)
				apiSource = api.XGetAttribute ("api-source");
		}

		public void Process (IEnumerable<XmlDocument> metaDocs, string apiLevel, int productVersion)
		{
			foreach (var metaDoc in metaDocs)
				Process (metaDoc, apiLevel, productVersion);
		}
		
		bool ShouldSkip (XPathNavigator node, int apiLevel, int productVersion)
		{
			if (apiLevel > 0) {
				string apiSince = node.XGetAttribute ("api-since", "");
				string apiUntil = node.XGetAttribute ("api-until", "");
				if (!string.IsNullOrEmpty (apiSince) && int.Parse (apiSince) > apiLevel)
					return true;
				if (!string.IsNullOrEmpty (apiUntil) && int.Parse (apiUntil) < apiLevel)
					return true;
			}
			if (productVersion > 0) {
				var product_version = node.XGetAttribute ("product-version", "");
				if (!string.IsNullOrEmpty (product_version) && int.Parse (product_version) > productVersion)
					return true;
			}
			return false;
		}

		bool ShouldApply (XPathNavigator node) 
		{
			if (!string.IsNullOrEmpty (apiSource)) {
				var targetsource = node.XGetAttribute ("api-source", "");
				if (string.IsNullOrEmpty (targetsource))
						return true;
				return targetsource == apiSource;
			}
			return true;
		}
		
		void Process (XmlDocument meta_doc, string apiLevelString, int productVersion)
		{
			XPathNavigator api_nav = api_doc.CreateNavigator ();
			XPathNavigator meta_nav = meta_doc.CreateNavigator ();
			int apiLevel = 0;
			int.TryParse (apiLevelString, out apiLevel);

			XPathNodeIterator metadata = meta_nav.Select ("/metadata/*");
			string prev_path = null;
			XPathNavigator attr_last_cache = null;

			while (metadata.MoveNext ()) {
				var metanav = metadata.Current;
				if (ShouldSkip (metanav, apiLevel, productVersion))
					continue;
				if (!ShouldApply (metanav))
					continue;
				string path = metanav.XGetAttribute ("path", "");
				if (path != prev_path)
					attr_last_cache = null;
				prev_path = path;

				switch (metanav.LocalName) {
				case "remove-node":
					try {
						XPathNodeIterator api_iter = api_nav.Select (path);
						List<XmlElement> matches = new List<XmlElement> ();
						while (api_iter.MoveNext ())
							matches.Add (((IHasXmlNode)api_iter.Current).GetNode () as XmlElement);
						foreach (XmlElement api_node in matches)
							api_node.ParentNode.RemoveChild (api_node);
						if (matches.Count == 0)
							// BG8A00
							Report.Warning (0, Report.WarningApiFixup + 0, "<remove-node path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A01
						Report.Error (Report.ErrorApiFixup + 1, e, "Invalid XPath specification: {0}", path);
					}
					break;
				case "add-node":
					try {
						XPathNodeIterator api_iter = api_nav.Select (path);
						bool matched = false;
						while (api_iter.MoveNext ()) {
							XmlElement api_node = ((IHasXmlNode)api_iter.Current).GetNode () as XmlElement;
							foreach (XmlNode child in ((IHasXmlNode)metanav).GetNode().ChildNodes)
								api_node.AppendChild (api_doc.ImportNode (child, true));
							matched = true;
						}
						if (!matched)
							// BG8A01
							Report.Warning (0, Report.WarningApiFixup + 1, "<add-node path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A02
						Report.Error (Report.ErrorApiFixup + 2, e, "Invalid XPath specification: {0}", path);
					}
					break;
				case "change-node":
					try {
						XPathNodeIterator api_iter = api_nav.Select (path);
						bool matched = false;
						while (api_iter.MoveNext ()) {
							XmlElement node = ( (IHasXmlNode) api_iter.Current).GetNode () as XmlElement;
							XmlElement parent = node.ParentNode as XmlElement;
							XmlElement new_node = api_doc.CreateElement (metanav.Value);
							
							foreach (XmlNode child in node.ChildNodes)
								new_node.AppendChild (child.Clone ());
							foreach (XmlAttribute attribute in node.Attributes)
								new_node.Attributes.Append ( (XmlAttribute) attribute.Clone ());
							
							parent.ReplaceChild (new_node, node);
							matched = true;
						}
						
						if (!matched)
							// BG8A03
							Report.Warning (0, Report.WarningApiFixup + 3, "<change-node-type path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A03
						Report.Error (Report.ErrorApiFixup + 3, e, "Invalid XPath specification: {0}", path);
					}
					break;
				case "attr":
					try {
						string attr_name = metanav.XGetAttribute ("name", "");
						if (string.IsNullOrEmpty (attr_name))
							// BG4A07
							Report.Error (Report.ErrorApiFixup + 7, "Target attribute name is not specified for path: {0}", path);
						var nodes = attr_last_cache != null ?
					            (IEnumerable<XPathNavigator>) new XPathNavigator [] {attr_last_cache} :
								api_nav.Select (path).OfType<XPathNavigator> ();
						int attr_matched = 0;
						foreach (var n in nodes) {
							XmlElement node = ((IHasXmlNode) n).GetNode () as XmlElement;
							node.SetAttribute (attr_name, metanav.Value);
							//attr_last_cache = n;
							attr_matched++;
						}
						if (attr_matched == 0)
							// BG8A04
							Report.Warning (0, Report.WarningApiFixup + 4, "<attr path=\"{0}\"/> matched no nodes.", path);
						if (attr_matched != 1)
							attr_last_cache = null;
					} catch (XPathException e) {
						// BG4A04
						Report.Error (Report.ErrorApiFixup + 4, e, "Invalid XPath specification: {0}", path);
					}
					break;
				case "move-node":
					try {
						XPathExpression expr = api_nav.Compile (path);
						string parent = metanav.Value;
						XPathNodeIterator parent_iter = api_nav.Select (parent);
						bool matched = false;
						while (parent_iter.MoveNext ()) {
							XmlNode parent_node = ((IHasXmlNode)parent_iter.Current).GetNode ();
							XPathNodeIterator path_iter = parent_iter.Current.Clone ().Select (expr);
							while (path_iter.MoveNext ()) {
								XmlNode node = ((IHasXmlNode)path_iter.Current).GetNode ();
								parent_node.AppendChild (node.Clone ());
								node.ParentNode.RemoveChild (node);
							}
							matched = true;
						}
						if (!matched)
							// BG8A05
							Report.Warning (0, Report.WarningApiFixup + 5, "<move-node path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A05
						Report.Error (Report.ErrorApiFixup + 5, e, "Invalid XPath specification: {0}", path);
					}
					break;
				case "remove-attr":
					try {
						string name = metanav.XGetAttribute ("name", "");
						XPathNodeIterator api_iter = api_nav.Select (path);
						bool matched = false;
						
						while (api_iter.MoveNext ()) {
							XmlElement node = ( (IHasXmlNode) api_iter.Current).GetNode () as XmlElement;
							
							node.RemoveAttribute (name);
							matched = true;
						}
						
						if (!matched)
							// BG8A06
							Report.Warning (0, Report.WarningApiFixup + 6, "<remove-attr path=\"{0}\"/> matched no nodes.", path);
					} catch (XPathException e) {
						// BG4A06
						Report.Error (Report.ErrorApiFixup + 6, e, "Invalid XPath specification: {0}", path);
					}
					break;
				}
			}
		}
	}
}

