using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text;

namespace Monodroid {
	static class AndroidResource {
		
		public static bool UpdateXmlResource (string res, Stream source, Stream dest, Dictionary<string, string> acwMap, IEnumerable<string> additionalDirectories = null, Action<TraceLevel, string> logMessage = null, bool leaveOpen = false, Action<string, string> registerCustomView = null)
		{
			string path = string.Empty;
			using (var writer = new LinePreservedXmlWriter (new StreamWriter (dest, System.Text.Encoding.UTF8, 4000, leaveOpen: leaveOpen))) {
				var reader = XmlReader.Create (source);
				while (reader.Read ()) {
					if (reader.NodeType == XmlNodeType.Element) {
						path += "/" + reader.LocalName;
					}
					WriteShallowNode (reader, writer, path, acwMap, res, additionalDirectories);
					registerCustomView?.Invoke (reader.Prefix != String.Empty ? reader.Name : reader.LocalName, null);
					if (reader.NodeType == XmlNodeType.Element) {
						if (reader.HasAttributes) {
							bool inTransition = (reader.Name == "transition");
							string parentLocalName = reader.LocalName;
							string parentNamespace = reader.NamespaceURI;
							for (int attInd = 0; attInd < reader.AttributeCount; attInd++) {
								reader.MoveToAttribute (attInd);

								string value = reader.Value;
								if (value != null)
									registerCustomView?.Invoke (value, null);
								Match m = r.Match (value);
								if (m.Success)
									value = TryLowercaseValue (value, res, additionalDirectories);
								if (String.Compare (reader.Prefix, "xmlns", StringComparison.Ordinal) == 0) {
									writer.WriteAttributeString (reader.Name, value);
									continue;
								}
								TryFixResAuto (reader, acwMap, ref value);
								if (reader.NamespaceURI != android && !(String.Compare (reader.LocalName, "layout", StringComparison.Ordinal) == 0 &&
										reader.NamespaceURI == string.Empty && String.Compare (parentLocalName, "include", StringComparison.Ordinal) == 0 &&
										parentNamespace == string.Empty)) {
									writer.WriteAttributeString (reader.Name, value);
									continue;
								}
								writer.WriteAttributeString (reader.Name, value);
							}
							reader.MoveToElement ();
						}
						if (reader.IsEmptyElement) {
							writer.WriteEndElement ();
							path = path.Substring (0, path.LastIndexOf ('/'));
						}
					}
					if (reader.NodeType == XmlNodeType.EndElement) {
						path = path.Substring (0, path.LastIndexOf ('/'));
					}
				}
			}
			return true;
		}

		public static XElement UpdateXmlResource (XElement e)
		{
			using (var ms = new MemoryStream ()) {
				using (var source = new MemoryStream ()) {
					e.Save (source);
					source.Position = 0;
					UpdateXmlResource (null, source, ms, new Dictionary<string, string> (), leaveOpen: true);
					ms.Position = 0;
					return XElement.Load (ms, LoadOptions.SetLineInfo);
				}
			}
		}
		public static XDocument UpdateXmlResourceAndLoad (string filename)
		{
			using (var ms = new MemoryStream ()) {
				using (var source = File.OpenRead (filename)) {
					UpdateXmlResource (null, source, ms, new Dictionary<string, string> (), leaveOpen: true);
					ms.Position = 0;
					return XDocument.Load (ms, LoadOptions.SetLineInfo);
				}
			}
		}

		public static bool UpdateXmlResource (string res, string filename, Dictionary<string, string> acwMap, IEnumerable<string> additionalDirectories = null, Action<TraceLevel, string> logMessage = null, Action<string, string> registerCustomView = null)
		{
			string tmpfile = filename + ".bk";
			try {
				using (var dest = File.Open (tmpfile, FileMode.Create))
				using (var source = File.OpenRead (filename)) {
					UpdateXmlResource (res, source, dest, acwMap, additionalDirectories, logMessage, registerCustomView: (e, f) => {
						registerCustomView?.Invoke (e, filename);
					});
				}
				return Xamarin.Android.Tasks.MonoAndroidHelper.CopyIfChanged (tmpfile, filename);
			} catch (Exception e) {
				logMessage?.Invoke (TraceLevel.Warning, $"AndroidResgen: Warning while updating Resource XML '{filename}': {e}");
			}
			finally {
				if (File.Exists (tmpfile)) {
					File.Delete (tmpfile);
				}
			}
			return false;
		}

		static readonly XNamespace android = "http://schemas.android.com/apk/res/android";
		static readonly XNamespace res_auto = "http://schemas.android.com/apk/res-auto";
		static readonly Regex r = new Regex (@"^@\+?(?<package>[^:]+:)?(anim|color|drawable|layout|menu)/(?<file>.*)$", RegexOptions.Compiled);
		static readonly string [] fixResourcesAliasPaths = {
			"/resources/item",
			"/resources/integer-array/item",
			"/resources/array/item",
			"/resources/style/item",
		};

		internal static IEnumerable<T> Prepend<T> (this IEnumerable<T> l, T another) where T : XNode
		{
			yield return another;
			foreach (var e in l)
				yield return e;
		}
		
		static bool ResourceNeedsToBeLowerCased (string value, string resourceBasePath, IEnumerable<string> additionalDirectories)
		{
			// Might be a bit of an overkill, but the data comes (indirectly) from the user since it's the
			// path to the msbuild's intermediate output directory and that location can be changed by the
			// user. It's better to be safe than sorry.
			resourceBasePath = (resourceBasePath ?? String.Empty).Trim ();
			if (String.IsNullOrEmpty (resourceBasePath))
				return true;

			// Avoid resource names that are all whitespace
			value = (value ?? String.Empty).Trim ();
			if (String.IsNullOrEmpty (value))
				return false; // let's save some time
			if (value.Length < 4 || value [0] != '@') // 4 is the minimum length since we need a string
								  // that is at least of the following
								  // form: @x/y. Checking it here saves some time
								  // below.
				return true;

			string filePath = null;
			int slash = value.IndexOf ('/');
			int colon = value.IndexOf (':');
			if (colon == -1)
				colon = 0;

			// Determine the the potential definition file's path based on the resource type.
			string dirPrefix = value.Substring (colon + 1, slash - colon - 1).ToLowerInvariant ();
			string fileNamePattern = value.Substring (slash + 1).ToLowerInvariant () + ".*";
			if (Directory.EnumerateDirectories (resourceBasePath, dirPrefix + "*").Any (dir => Directory.EnumerateFiles (dir, fileNamePattern).Any ()))
				return true;

			// check additional directories if we have them incase the resource is in a library project
			if (additionalDirectories != null)
				foreach (var additionalDirectory in additionalDirectories)
					if (Directory.EnumerateDirectories (additionalDirectory, dirPrefix + "*").Any (dir => Directory.EnumerateFiles (dir, fileNamePattern).Any ()))
						return true;

			// No need to change the reference case.
			return false;
		}

		internal static IEnumerable<XAttribute> GetAttributes (XElement e)
		{
			foreach (XAttribute a in e.Attributes ())
				yield return a;
			foreach (XElement c in e.Elements ())
				foreach (XAttribute a in GetAttributes (c))
					yield return a;
		}

		internal static IEnumerable<XElement> GetElements (XElement e)
		{
			foreach (var a in e.Elements ()) {
				yield return a;

				foreach (var b in GetElements (a))
					yield return b;
			}
		}

		static string TryFixResourceAlias (XmlReader elem, string path, string resourceBasePath, IEnumerable<string> additionalDirectories)
		{
			// Looks for any resources aliases:
			//   <item type="layout" name="">@layout/Page1</item>
			//   <item type="layout" name="">@drawable/Page1</item>
			// and corrects the alias to be lower case.
			if (!fixResourcesAliasPaths.Contains (path))
				return elem.Value;
			if (!string.IsNullOrEmpty (elem.Value)) {
				string value = elem.Value.Trim ();
				Match m = r.Match (value);
				if (m.Success) {
					return TryLowercaseValue (elem.Value, resourceBasePath, additionalDirectories);
				}
			}
			return elem.Value;
		}

		private static bool TryFixResAuto (XmlReader attr, Dictionary<string, string> acwMap, ref string value)
		{
			if (attr.NamespaceURI != res_auto)
				return false;
			var name = attr.LocalName;
			if (name.Equals ("rectLayout") || name.Equals ("roundLayout") || name.Equals ("actionLayout")) {
				value = value.ToLowerInvariant ();
				return true;
			}
			return false;
		}

		private static string TryLowercaseValue (string value, string resourceBasePath, IEnumerable<string> additionalDirectories)
		{
			int s = value.LastIndexOf ('/');
			if (s >= 0) {
				if (ResourceNeedsToBeLowerCased (value, resourceBasePath, additionalDirectories)) {
					return value.Substring (0, s) + "/" + value.Substring (s + 1).ToLowerInvariant ();
				}
			}
			return value;
		}

		static void WriteShallowNode (XmlReader reader, XmlWriter writer, string path, Dictionary<string, string> acwMap, string resourceBasePath, IEnumerable<string> additionalDirectories)
		{
			if (reader == null) {
				throw new ArgumentNullException ("reader");
			}
			if (writer == null) {
				throw new ArgumentNullException ("writer");
			}

			switch (reader.NodeType) {
			case XmlNodeType.Element:
				if (reader.Prefix != String.Empty)
					writer.WriteStartElement (reader.Name);
				else
					writer.WriteStartElement (reader.LocalName);
				break;
			case XmlNodeType.Text:
				writer.WriteString (TryFixResourceAlias (reader, path, resourceBasePath, additionalDirectories));
				break;
			case XmlNodeType.Whitespace:
			case XmlNodeType.SignificantWhitespace:
				writer.WriteWhitespace (reader.Value);
				break;
			case XmlNodeType.CDATA:
				writer.WriteCData (reader.Value);
				break;
			case XmlNodeType.EntityReference:
				writer.WriteEntityRef (reader.Name);
				break;
			case XmlNodeType.XmlDeclaration:
			case XmlNodeType.ProcessingInstruction:
				writer.WriteProcessingInstruction (reader.Name, reader.Value);
				break;
			case XmlNodeType.DocumentType:
				writer.WriteDocType (reader.Name, reader.GetAttribute ("PUBLIC"), reader.GetAttribute ("SYSTEM"), reader.Value);
				break;
			case XmlNodeType.Comment:
				writer.WriteComment (reader.Value);
				break;
			case XmlNodeType.EndElement:
				writer.WriteFullEndElement ();
				break;
			}
		}
	}
}