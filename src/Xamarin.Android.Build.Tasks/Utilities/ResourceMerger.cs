using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Xamarin.Android.Tasks
{
	[Serializable]
	internal struct ResourceItem {
		public string Source;
		public string Destination;
		public string Name;
		public string ElementName;
		public string Value;
	}
		
	internal class ResourceMerger {

		Dictionary<string,List<ResourceItem>> sources = new Dictionary<string, List<ResourceItem>>();
		Dictionary<string, ResourceItem> resources = new Dictionary<string, ResourceItem> ();
		Regex localizationRegEx = new Regex ("([vV]alues[-])");
		Regex regEx = new Regex ("([vV]alues)[-A-Za-z0-9_]*");
		const string keyFormat = "{0} (\"{1}\")";
		const SaveOptions saveOptions = SaveOptions.DisableFormatting;

		public ResourceMerger ()
		{
		}

		internal void Load ()
		{
			if (File.Exists (CacheFile)) {
				var document = XDocument.Load (CacheFile);
				foreach (var k in document.Element ("Keys").Elements ("Key")) {
					var name = k.Attribute ("name").Value;
					sources.Add (name, new List<ResourceItem> ());
					foreach ( var i in k.Element ("Values").Elements ("Value")) {
						sources [name].Add (new ResourceItem () {
							ElementName = i.Element ("ElementName").Value,
							Name = i.Element ("Name").Value,
							Destination = i.Element ("Destination").Value,
							Source = i.Element ("Source").Value,
							Value = i.Element ("Value").Value
						});
					}

				}
			}
		}

		internal void Save ()
		{
			var document = new XDocument (
				new XDeclaration ("1.0", "UTF-8", null),
				new XElement ("Keys", sources.Select (
					x => new XElement ("Key", 
						new XAttribute ("name", x.Key),
						new XElement ("Values",
							x.Value.Select (v => 
								new XElement ("Value",
									new XElement ("ElementName", v.ElementName),
									new XElement ("Name", v.Name),
									new XElement ("Source", v.Source),
									new XElement ("Destination", v.Destination),
									new XElement ("Value", new XCData (v.Value))
								)
							)
						)
					))
				)
			);
			document.Save (CacheFile, options: saveOptions);
		}

		internal void RegisterResource(string src, string dest, XElement m)
		{
			var key = CalculateKey (m);
			if (!sources.ContainsKey (key)) {
				sources [key] = new List<ResourceItem> ();
			}
			var item = new ResourceItem () {
				Source = src,
				Destination = dest,
				Value = m.Value,
				Name = m.Attribute("name").Value,
				ElementName = m.Name.ToString (),
			};
			if (!resources.ContainsKey (key))
				resources.Add (key, item);
			if (!sources [key].Any (x => x.Source == src)) {
				// ignore localized resources e.g values-da as we will always get duplicates
				if (!localizationRegEx.Match (src).Success) {
					Log.LogDebugMessage ("Adding {0} from {1} {2} {3}", key, item.Source, item.Destination, item.Value);
					sources [key].Add (item);
				}
			}
		}

		internal void RemoveResourcesForFile(string filename)
		{
			var removed = sources.Keys.Where (x => !resources.ContainsKey (x) && sources[x].Any (d => d.Destination == filename )).ToArray();
			if (removed.Any ()) {
				var doc = XDocument.Load (filename);
				foreach (var r in removed) {
					var r1 = doc.Root.Elements ().FirstOrDefault (x => CalculateKey (x) == r);
					if (r1 != null)
						r1.Remove ();
					// clean up the "cache"
					sources [r].RemoveAll (x => x.Destination == filename);
					if (sources [r].Count == 0)
						sources.Remove (r);
				}
				doc.Save (filename, SaveOptions.DisableFormatting);
			}
		}

		internal string CalculateKey (XElement x)
		{
			return !HasNameAttribute (x) ? x.Name.ToString() : string.Format (keyFormat, x.Name, x.Attribute ("name").Value);
		}

		internal bool ElementsMatch (XElement src, XElement dst)
		{
			return src.Name == dst.Name &&
				HasNameAttribute (src) &&
				HasNameAttribute (dst) &&
				src.Attribute ("name").Value == dst.Attribute ("name").Value;
		}

		internal bool HasNameAttribute (XElement src)
		{
			return src.Attribute ("name") != null;
		}

		internal void MergeValues (string src, string dest)
		{
			Log.LogDebugMessage ("Attempting to merge {0} and {1}" ,src, dest);

			XDocument source = null, destination = null;
			try {
				source = XDocument.Load (src);
			} catch (Exception ex) {
				Log.LogErrorFromException (ex, showStackTrace: true, showDetail: true, file: src);
				return;
			}
			try {
				destination = File.Exists (dest) ? XDocument.Load (dest) : new XDocument ();
			} catch (Exception ex) {
				Log.LogErrorFromException (ex, showStackTrace: true, showDetail: true, file: src);
				return;
			}

			if (source.Root != null) {
				var duplicates = source.Root.Elements ()
					.Where (x => source.Root.Elements ().Any (d  => x != d && ElementsMatch(x, d)));
				foreach (var d in duplicates) {
					Log.LogError (
						subcategory: null,
						errorCode: "XA5216",
						helpKeyword: null, 
						file: src, 
						lineNumber: 0, 
						columnNumber: 0,
						endLineNumber: 0,
						endColumnNumber: 0,
						message: "Resource entry {0} is already defined in {1}",
						messageArgs: new[] {
							d.Attribute ("name").Value,
							src,
						});
				}

				foreach (var m in source.Root.Elements ()) {
					if (sources == null)
						continue;
					if (!HasNameAttribute(m))
						continue;
					RegisterResource (src, dest, m);
				}
			}

			var nameElements = (destination.Root == null ? new XElement[0] : destination.Root.Elements ())
				.Where (x =>  HasNameAttribute(x))
				.Select (x => x);

			var toMerge = (source.Root == null ? new XElement[0] : source.Root.Elements ())
				.Where (x => !nameElements.Any (d => ElementsMatch (d, x) && d.Value == x.Value));

			if (!toMerge.Any ()) {
				Log.LogDebugMessage ("Skipped merging, nothing to merge");
				if (!File.Exists (dest)) {
					Directory.CreateDirectory (Path.GetDirectoryName (dest));
					source.Save (dest, saveOptions);
				}
				return;
			}
			Log.LogDebugMessage ("Merging values document {0} into {1}", src, dest);

			var existing = from x in toMerge
				let key = CalculateKey (x)
					where this [key].Any (d => d.Value != x.Value ||
						(d.Value == x.Value && Path.GetFullPath (d.Source) != Path.GetFullPath (src)))
				select x;

			if (!localizationRegEx.Match (src).Success && existing.Any ()) {
				foreach (var e in existing) {
					var key = CalculateKey (e);
					var others = string.Join (" ", this[key].Select (x => x.Source));
					Log.LogWarning (
						subcategory: null,
						warningCode: "XA5215",
						helpKeyword: null, 
						file: src, 
						lineNumber: 0, 
						columnNumber: 0,
						endLineNumber: 0,
						endColumnNumber: 0,
						message: "Duplicate Resource found for {0}. Duplicates are in {1}",
						messageArgs: new[] {
							e.Attribute ("name").Value,
							others,
						});

					// load the other file. replace the value
					var existingFile = this[key].First ();
					Log.LogDebugMessage ("Loading {0}", existingFile.Destination);
					var doc = existingFile.Destination != dest ? XDocument.Load (existingFile.Destination) : destination;
					var exist = doc.Root == null ? null : doc.Root.Elements ().FirstOrDefault (x => ElementsMatch (x, e));
					if (exist != null && !string.IsNullOrEmpty (e.Value)) {
						Log.LogDebugMessage ("Replacing {0} with {1} in {2}", exist.Value, e.Value, existingFile.Destination);
						exist.Value = e.Value;
					}
					if (existingFile.Destination != dest) {
						Log.LogDebugMessage ("Saving {0}", existingFile.Destination);
						doc.Save (existingFile.Destination, saveOptions);
					}
					// remove this element from this document
					e.Remove ();
				}
			}

			foreach (var e in toMerge) {
				Log.LogDebugMessage ("Need to Merge {0} {1}", e.Name, e.Value);
				var d = destination.Root == null ? null : destination.Root.Elements ().FirstOrDefault (x => ElementsMatch (x, e));
				if (d != null) {
					d.Value = e.Value;
					Log.LogDebugMessage ("Merged {0} {1}", e.Name, e.Value);
				}
			}

			if (destination.Root != null) {
				toMerge = toMerge.Where (x => !existing.Any (d => ElementsMatch(d, x)));
				destination.Root.Add (toMerge);
				destination.Save (dest, saveOptions);
			} else {
				Directory.CreateDirectory (Path.GetDirectoryName (dest));
				source.Save (dest, saveOptions);
			}
		}

		internal TaskLoggingHelper Log {
			get;
			set;
		}

		internal string CacheFile {
			get;
			set;
		}

		internal IEnumerable<string> Keys {
			get {
				return sources.Keys;
			}
		}

		internal List<ResourceItem> this[string key] {
			get {
				if (sources.ContainsKey (key))
					return sources [key];
				return new List<ResourceItem> ();;
			}
		}

		public bool NeedsMerge (string path)
		{
			return regEx.Match (path).Success;
		}
	}
}

