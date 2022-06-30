using System;
using System.Linq;
using System.Xml.XPath;
using System.Xml.Linq;

using Xamarin.Android.Tools;
using System.Collections.Generic;

namespace Java.Interop.Tools.Generator
{
	public class FixupXmlDocument
	{
		public XDocument FixupDocument { get; }
		
		public FixupXmlDocument (XDocument fixupDocument)
		{
			FixupDocument = fixupDocument;
		}

		public static FixupXmlDocument? Load (string filename)
		{
			if (UtilityExtensions.LoadXmlDocument (filename) is XDocument doc)
				return new FixupXmlDocument (doc);

			return null;
		}

		public void Apply (ApiXmlDocument apiDocument, string apiLevelString, int productVersion)
		{
			// Defaulting to 0 here is fine
			int.TryParse (apiLevelString, out var apiLevel);

			var metadataChildren = FixupDocument.XPathSelectElements ("/metadata/*");

			string? prev_path = null;
			XElement? attr_last_cache = null;

			foreach (var metaitem in metadataChildren) {
				if (ShouldSkip (metaitem, apiLevel, productVersion))
					continue;
				if (!ShouldApply (metaitem, apiDocument))
					continue;

				var path = metaitem.XGetAttribute ("path");

				if (path != prev_path)
					attr_last_cache = null;

				prev_path = path;

				switch (metaitem.Name.LocalName) {
				case "remove-node":
					try {
						var nodes = apiDocument.ApiDocument.XPathSelectElements (path).ToArray ();

						if (nodes.Any ())
							foreach (var node in nodes)
								node.Remove ();
						else
							// BG8A00
							Report.LogCodedWarning (0, Report.WarningRemoveNodeMatchedNoNodes, null, metaitem, $"<remove-node path=\"{path}\" />");
					} catch (XPathException) {
						// BG4301
						Report.LogCodedError (Report.ErrorRemoveNodeInvalidXPath, metaitem, path);
					}
					break;
				case "add-node":
					try {
						var nodes = apiDocument.ApiDocument.XPathSelectElements (path);

						if (!nodes.Any ())
							// BG8A01
							Report.LogCodedWarning (0, Report.WarningAddNodeMatchedNoNodes, null, metaitem, $"<add-node path=\"{path}\" />");
						else {
							foreach (var node in nodes)
								node.Add (metaitem.Nodes ());
						}
					} catch (XPathException) {
						// BG4302
						Report.LogCodedError (Report.ErrorAddNodeInvalidXPath, metaitem, path);
					}
					break;
				case "change-node":
					try {
						var nodes = apiDocument.ApiDocument.XPathSelectElements (path);
						var matched = false;

						foreach (var node in nodes) {
							var newChild = new XElement (metaitem.Value);
							newChild.Add (node.Attributes ());
							newChild.Add (node.Nodes ());
							node.ReplaceWith (newChild);
							matched = true;
						}
						
						if (!matched)
							// BG8A03
							Report.LogCodedWarning (0, Report.WarningChangeNodeTypeMatchedNoNodes, null, metaitem, $"<change-node-type path=\"{path}\" />");
					} catch (XPathException) {
						// BG4303
						Report.LogCodedError (Report.ErrorChangeNodeInvalidXPath, metaitem, path);
					}
					break;
				case "attr":
					try {
						var  attr_name = metaitem.XGetAttribute ("name");

						if (string.IsNullOrEmpty (attr_name))
							// BG4307
							Report.LogCodedError (Report.ErrorMissingAttrName, metaitem, path);
						var nodes = attr_last_cache != null ? new XElement [] { attr_last_cache } : apiDocument.ApiDocument.XPathSelectElements (path);
						var attr_matched = 0;

						foreach (var n in nodes) {
							n.SetAttributeValue (attr_name, metaitem.Value);
							attr_matched++;
						}
						if (attr_matched == 0)
							// BG8A04
							Report.LogCodedWarning (0, Report.WarningAttrMatchedNoNodes, null, metaitem, $"<attr path=\"{path}\" />");
						if (attr_matched != 1)
							attr_last_cache = null;
					} catch (XPathException) {
						// BG4304
						Report.LogCodedError (Report.ErrorAttrInvalidXPath, metaitem, path);
					}
					break;
				case "move-node":
					try {
						var parent = metaitem.Value;
						var parents = apiDocument.ApiDocument.XPathSelectElements (parent);
						var matched = false;

						foreach (var parent_node in parents) {
							var nodes = parent_node.XPathSelectElements (path).ToArray ();
							foreach (var node in nodes)
								node.Remove ();
							parent_node.Add (nodes);
							matched = true;
						}
						if (!matched)
							// BG8A05
							Report.LogCodedWarning (0, Report.WarningMoveNodeMatchedNoNodes, null, metaitem, $"<move-node path=\"{path}\" />");
					} catch (XPathException) {
						// BG4305
						Report.LogCodedError (Report.ErrorMoveNodeInvalidXPath, metaitem, path);
					}
					break;
				case "remove-attr":
					try {
						var name = metaitem.XGetAttribute ("name");
						var nodes = apiDocument.ApiDocument.XPathSelectElements (path);
						var matched = false;

						foreach (var node in nodes) {
							node.Attributes (name).Remove ();
							matched = true;
						}
						
						if (!matched)
							// BG8A06
							Report.LogCodedWarning (0, Report.WarningRemoveAttrMatchedNoNodes, null, metaitem, $"<remove-attr path=\"{path}\" />");
					} catch (XPathException) {
						// BG4306
						Report.LogCodedError (Report.ErrorRemoveAttrInvalidXPath, metaitem, path);
					}
					break;
				}
			}
		}

		public IList<NamespaceTransform> GetNamespaceTransforms ()
		{
			var list = new List<NamespaceTransform> ();

			foreach (var xe in FixupDocument.XPathSelectElements ("/metadata/ns-replace")) {
				if (NamespaceTransform.TryParse (xe, out var transform))
					list.Add (transform);
			}

			return list;
		}

		bool ShouldSkip (XElement node, int apiLevel, int productVersion)
		{
			if (apiLevel > 0) {
				var since = node.XGetAttributeAsInt ("api-since");
				var until = node.XGetAttributeAsInt ("api-until");

				if (since is int since_int && since_int > apiLevel)
					return true;
				else if (until is int until_int && until_int < apiLevel)
					return true;
			}

			if (productVersion > 0) {
				var product_version = node.XGetAttributeAsInt ("product-version");

				if (product_version is int version && version > productVersion)
					return true;

			}
			return false;
		}

		bool ShouldApply (XElement node, ApiXmlDocument apiDocument)
		{
			if (apiDocument.ApiSource.HasValue ()) {
				var targetsource = node.XGetAttribute ("api-source");

				if (!targetsource.HasValue ())
					return true;

				return targetsource == apiDocument.ApiSource;
			}

			return true;
		}
	}
}
