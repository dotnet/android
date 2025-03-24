using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;
using System.IO.Compression;

namespace Xamarin.Android.Tasks
{
	class FileResourceParser : ResourceParser
	{
		public string JavaPlatformDirectory { get; set; }

		public string ResourceFlagFile { get; set; }

		Dictionary<R, R []> arrayMapping = new Dictionary<R, R []> ();
		Dictionary<string, List<string>> foofoo = new Dictionary<string, List<string>> ();
		List<string> custom_types = new List<string> ();
		XDocument publicXml;

		string[] publicXmlFiles = new string[] {
			"public.xml",
			"public-final.xml",
			"public-staging.xml",
		};

		protected XDocument LoadPublicXml () {
			if (string.IsNullOrEmpty (JavaPlatformDirectory))
				return null;
			string publicXmlPath = Path.Combine (JavaPlatformDirectory, "data", "res", "values");
			foreach (var file in publicXmlFiles) {
				if (File.Exists (Path.Combine (publicXmlPath, file))) {
					return XDocument.Load (Path.Combine (publicXmlPath, file));
				}
			}
			return null;
		}

		public IList<R> Parse (string resourceDirectory, IEnumerable<string> additionalResourceDirectories, IEnumerable<string> aarLibraries, Dictionary<string, string> resourceMap)
		{
			Log.LogDebugMessage ($"Parsing Directory {resourceDirectory}");
			publicXml = LoadPublicXml ();
			var result = new List<R> ();
			Dictionary<string, ICollection<R>> resources = new Dictionary<string, ICollection<R>> ();
			foreach (var knownType in RtxtParser.knownTypes) {
				if (knownType == "styleable") {
					resources.Add (knownType, new List<R> ());
					continue;
				}
				resources.Add (knownType, new SortedSet<R> (new RComparer ()));
			}
			foreach (var dir in Directory.EnumerateDirectories (resourceDirectory, "*", SearchOption.TopDirectoryOnly)) {
				foreach (var file in Directory.EnumerateFiles (dir, "*.*", SearchOption.AllDirectories)) {
					ProcessResourceFile (file, resources);
				}
			}
			foreach (var dir in additionalResourceDirectories ?? Array.Empty<string>()) {
				Log.LogDebugMessage ($"Processing Directory {dir}");
				if (Directory.Exists (dir)) {
					foreach (var file in Directory.EnumerateFiles (dir, "*.*", SearchOption.AllDirectories)) {
						ProcessResourceFile (file, resources);
					}
				} else {
					Log.LogDebugMessage ($"Skipping non-existent directory: {dir}");
				}
			}
			foreach (var aar in aarLibraries ??  Array.Empty<string>()) {
				Log.LogDebugMessage ($"Processing Aar file {aar}");
				if (!File.Exists (aar)) {
					Log.LogDebugMessage ($"Skipping non-existent aar: {aar}");
					continue;
				}
				using (var file = File.OpenRead (aar)) {
					using var zip = new ZipArchive (file);
					foreach (var entry in zip.Entries) {
						if (entry.IsDirectory ())
							continue;
						if (!entry.FullName.StartsWith ("res"))
							continue;
						var ext = Path.GetExtension (entry.FullName);
						var path = Directory.GetParent (entry.FullName).Name;
						if (ext == ".xml" || ext == ".axml") {
							if (string.Compare (path, "raw", StringComparison.OrdinalIgnoreCase) != 0) {
								var ms = MemoryStreamPool.Shared.Rent ();
								try {
									using (var entryStream = entry.Open ()) {
										entryStream.CopyTo (ms);
									}
									ms.Position = 0;
									using XmlReader reader = XmlReader.Create (ms);
									ProcessXmlFile (reader, resources);
								} finally {
									MemoryStreamPool.Shared.Return (ms);
								}
							}
						}
						ProcessResourceFile (entry.FullName, resources, processXml: false);
					}
				}
			}

			// now generate the Id's we need in a specific order
			List<string> declarationIds = new List<string> ();
			declarationIds.Add ("attr");
			declarationIds.Add ("drawable");
			declarationIds.Add ("mipmap");
			declarationIds.Add ("font");
			declarationIds.Add ("layout");
			declarationIds.Add ("anim");
			declarationIds.Add ("animator");
			declarationIds.Add ("transition");
			declarationIds.Add ("xml");
			declarationIds.Add ("raw");
			declarationIds.Add ("dimen");
			declarationIds.Add ("string");
			declarationIds.Add ("array");
			declarationIds.Add ("plurals");
			declarationIds.Add ("bool");
			declarationIds.Add ("color");
			declarationIds.Add ("integer");
			declarationIds.Add ("menu");
			declarationIds.Add ("id");
			// custom types
			foreach (var customClass in custom_types) {
				declarationIds.Add (customClass);
			}

			declarationIds.Add ("interpolator");
			declarationIds.Add ("style");
			declarationIds.Add ("styleable");

			declarationIds.Sort ((a, b) => {
				return string.Compare (a, b, StringComparison.OrdinalIgnoreCase);
			});

			string itemPackageId = "0x7f";
			int typeid = 1;

			foreach (var t in declarationIds) {
				int itemid = 0;
				if (!resources.ContainsKey(t)) {
					continue;
				}
				if (resources[t].Count == 0) {
					continue;
				}
				foreach (R r in resources[t].OrderBy(x => x.ToSortedString(), StringComparer.Ordinal)) {

					int id = Convert.ToInt32 (itemPackageId + typeid.ToString ("X2") + itemid.ToString ("X4"), fromBase: 16);
					if ((r.Type != RType.Array) && r.Id == -1) {
						itemid++;
						r.UpdateId (id);
					} else {
						if (foofoo.ContainsKey (r.Identifier)) {
							var items = foofoo[r.Identifier];
							if (r.Ids != null) {
								// do something special cos its an array we need to replace *some* its.
								int[] newIds = new int[r.Ids.Length];
								for (int i = 0; i < r.Ids.Length; i++) {
									// we need to lookup the ID's for these from the ones generated.
									newIds[i] = r.Ids[i];
									if (r.Ids[i] == -1)
										newIds[i] = GetId (result, items[i]);
								}
								r.UpdateIds (newIds);
							}
						}
					}
					result.Add (r);
				}
				typeid++;
			}

			result.Sort (new RComparer ());

			return result;
		}

		class RComparer : IComparer<R> {
			public int Compare(R a, R b) {
				return string.Compare (a.ToSortedString (), b.ToSortedString (), StringComparison.Ordinal);
			}
		}

		HashSet<string> resourceNamesToUseDirectly = new HashSet<string> () {
			"integer-array",
			"string-array",
			"declare-styleable",
			"add-resource",
		};

		int GetId (ICollection<R> resources, string identifier)
		{
			foreach (R r in resources) {
				if (r.Identifier == identifier) {
					return r.Id;
				}
			}
			return -1;
		}

		void ProcessResourceFile (string file, Dictionary<string, ICollection<R>> resources, bool processXml = true)
		{
			Log.LogDebugMessage ($"{nameof(ProcessResourceFile)} {file}");
			var fileName = Path.GetFileNameWithoutExtension (file);
			if (string.IsNullOrEmpty (fileName))
				return;
			if (fileName.EndsWith (".9", StringComparison.OrdinalIgnoreCase))
				fileName = Path.GetFileNameWithoutExtension (fileName);
			var path = Directory.GetParent (file).Name;
			if (!processXml) {
				CreateResourceField (path, fileName, resources);
				return;
			}
			var ext = Path.GetExtension (file);
			switch (ext) {
				case ".xml":
				case ".axml":
					if (string.Compare (path, "raw", StringComparison.OrdinalIgnoreCase) == 0)
						goto default;
					try {
						ProcessXmlFile (file, resources);
					} catch (XmlException ex) {
						Log.LogCodedWarning ("XA1000", Properties.Resources.XA1000, file, ex);
					}
					break;
				default:
					break;
			}
			CreateResourceField (path, fileName, resources);
		}

		void CreateResourceField (string root, string id, Dictionary<string, ICollection<R>> resources) {
			var i = root.IndexOf ('-');
			var item = i < 0 ? root : root.Substring (0, i);
			item = resourceNamesToUseDirectly.Contains (root) ? root : item;
			switch (item.ToLowerInvariant ()) {
				case "animation":
					item = "anim";
					break;
				case "array":
				case "string-array":
				case "integer-array":
					item = "array";
					break;
				case "enum":
				case "flag":
					item = "id";
					break;
			}
			var r = new R () {
				ResourceTypeName = item,
				Identifier = id,
				Id = -1,
			};
			if (!resources.ContainsKey (item)) {
				Log.LogDebugMessage ($"Ignoring path:{item}");
				return;
			}
			resources[item].Add (r);
		}

		void ProcessStyleable (XmlReader reader, Dictionary<string, ICollection<R>> resources)
		{
			Log.LogDebugMessage ($"{nameof(ProcessStyleable)}");
			string topName = null;
			int fieldCount = 0;
			List<R> fields = new List<R> ();
			List<string> attribs = new List<string> ();
			if (reader.HasAttributes) {
				while (reader.MoveToNextAttribute ()) {
					if (reader.Name.Replace ("android:", "") == "name")
						topName = reader.Value;
				}
			}
			while (reader.Read ()) {
				if (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.Comment)
					continue;
				string name = null;
				if (string.IsNullOrEmpty (topName)) {
					if (reader.HasAttributes) {
						while (reader.MoveToNextAttribute ()) {
							if (reader.Name.Replace ("android:", "") == "name")
								topName = reader.Value;
						}
					}
				}
				if (!reader.IsStartElement ())
					continue;
				if (reader.HasAttributes) {
					while (reader.MoveToNextAttribute ()) {
						if (reader.Name.Replace ("android:", "") == "name")
							name = reader.Value;
					}
				}
				reader.MoveToElement ();
				if (reader.LocalName == "attr") {
					attribs.Add (name);
				}
			}
			var field = new R () {
				ResourceTypeName = "styleable",
				Identifier = topName,
				Type = RType.Array,
			};
			if (!arrayMapping.ContainsKey (field)) {
				foofoo.Add (field.Identifier, new List<string> ());
				attribs.Sort (StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < attribs.Count; i++) {
					string name = attribs [i];
					if (!name.StartsWith ("android:", StringComparison.OrdinalIgnoreCase)) {
						var r = new R () {
							ResourceTypeName = "attr",
							Identifier = $"{name}",
							Id = -1,
						};
						resources [r.ResourceTypeName].Add (r);
						fields.Add (r);
					} else {
						// this is an android:xxx resource, we should not calculate the id
						// we should get it from "somewhere" maybe the pubic.xml
						name = name.Replace ("android:", string.Empty);
						var element = publicXml?.XPathSelectElement ($"/resources/public[@name='{name}']") ?? null;
						int value = Convert.ToInt32 (element?.Attribute ("id")?.Value ?? "0x0", fromBase: 16);
						var r = new R () {
							ResourceTypeName = "attr",
							Identifier = $"{name}",
							Id = value,
						};
						fields.Add (r);
					}
				}
				arrayMapping.Add (field, fields.ToArray ());

				field.Ids = new int [attribs.Count];
				for (int idx =0; idx < field.Ids.Length; idx++)
					field.Ids[idx] = fields[idx].Id;
				resources [field.ResourceTypeName].Add (field);
				int id = 0;
				foreach (string r in attribs) {
					foofoo[field.Identifier].Add (r.Replace (":", "_"));
					resources [field.ResourceTypeName].Add (new R () {
						ResourceTypeName = field.ResourceTypeName,
						Identifier = $"{field.Identifier}_{r.Replace (":", "_")}",
						Id = id++,
						Type = RType.Integer_Styleable,
					});
				}
			}
		}

		void ProcessXmlFile (string file, Dictionary<string, ICollection<R>> resources)
		{
			using (var reader = XmlReader.Create (file)) {
				ProcessXmlFile (reader, resources);
			}
		}

		void ProcessXmlFile (XmlReader reader, Dictionary<string, ICollection<R>> resources)
		{
			Log.LogDebugMessage ($"{nameof(ProcessXmlFile)}");
			while (reader.Read ()) {
				if (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.Comment)
					continue;
				if (reader.IsStartElement ()) {
					var elementName = reader.Name;
					var elementNS = reader.NamespaceURI;
					if (!string.IsNullOrEmpty (elementNS)) {
						if (elementNS != "http://schemas.android.com/apk/res/android")
							continue;
					}
					if (elementName == "declare-styleable" || elementName == "configVarying" || elementName == "add-resource") {
						try {
							ProcessStyleable (reader.ReadSubtree (), resources);
						} catch (Exception ex) {
							Log.LogErrorFromException (ex);
						}
						continue;
					}
					if (reader.HasAttributes) {
						string name = null;
						string type = null;
						string id = null;
						string custom_id = null;
						while (reader.MoveToNextAttribute ()) {
							if (reader.LocalName == "name")
								name = reader.Value;
							if (reader.LocalName == "type")
								type = reader.Value;
							if (reader.LocalName == "id") {
								string[] values = reader.Value.Split ('/');
								if (values.Length != 2) {
									id = reader.Value.Replace ("@+id/", "").Replace ("@id/", "");
								} else {
									if (values [0] != "@+id" && values [0] != "@id" && !values [0].Contains ("android:")) {
										custom_id = values [0].Replace ("@", "").Replace ("+", "");
									}
									id = values [1];
								}

							}
							if (reader.LocalName == "inflatedId") {
								string inflateId = reader.Value.Replace ("@+id/", "").Replace ("@id/", "");
								var r = new R () {
									ResourceTypeName = "id",
									Identifier = inflateId,
									Id = -1,
								};
								Log.LogDebugMessage ($"Adding 1 {r}");
								resources[r.ResourceTypeName].Add (r);
							}
						}
						if (name?.Contains ("android:") ?? false)
							continue;
						if (id?.Contains ("android:") ?? false)
							continue;
						// Move the reader back to the element node.
						reader.MoveToElement ();
						if (!string.IsNullOrEmpty (name)) {
							CreateResourceField (type ?? elementName, name, resources);
						}
						if (!string.IsNullOrEmpty (custom_id) && !resources.ContainsKey (custom_id)) {
							resources.Add (custom_id, new SortedSet<R> (new RComparer ()));
							custom_types.Add (custom_id);
						}
						if (!string.IsNullOrEmpty (id)) {
							CreateResourceField (custom_id ?? "id", id.Replace ("-", "_").Replace (".", "_"), resources);
						}
					}
				}
			}
		}
	}
}
