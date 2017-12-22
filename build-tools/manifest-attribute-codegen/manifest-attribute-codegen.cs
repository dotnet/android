using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;


namespace Xamarin.Android.Tools.ManifestAttributeCodeGenerator
{
	public class Driver
	{
		public static void Main (string [] args)
		{
			var sdk = args.FirstOrDefault ()
				?? Environment.GetEnvironmentVariable ("ANDROID_SDK_HOME")
				?? Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (sdk == null)
				throw new InvalidOperationException ("Pass Android SDK location as the command argument, or specify ANDROID_SDK_HOME or ANDROID_SDK_PATH environment variable");

			var manifests = Directory.GetDirectories  (Path.Combine (sdk, "platforms"), "android-*")
				.Select (d => Path.Combine (d, "data", "res", "values", "attrs_manifest.xml"))
				.Where (f => File.Exists (f))
				.ToList ();
			manifests.Sort (StringComparer.OrdinalIgnoreCase);
			var defs = manifests.Select (m => ManifestDefinition.FromFileName (m));
			
			var merged = new ManifestDefinition ();
			foreach (var def in defs) {
				foreach (var el in def.Elements) {
					var element = merged.Elements.FirstOrDefault (_ => _.Name == el.Name);
					if (element == null)
						merged.Elements.Add ((element = new ElementDefinition {
							ApiLevel = el.ApiLevel,
							Name = el.Name,
							Parents = (string []) el.Parents?.Clone (),
						}));
					foreach (var at in el.Attributes) {
						var attribute = element.Attributes.FirstOrDefault (_ => _.Name == at.Name);
						if (attribute == null)
							element.Attributes.Add ((attribute = new AttributeDefinition {
								ApiLevel = at.ApiLevel,
								Name = at.Name,
								Format = at.Format,
							}));
						foreach (var en in at.Enums) {
							var enumeration = at.Enums.FirstOrDefault (_ => _.Name == en.Name);
							if (enumeration == null)
								attribute.Enums.Add (new EnumDefinition {
									ApiLevel = en.ApiLevel,
									Name = en.Name,
									Value = en.Value,
								});
						}
					}
				}
			}
			var sw = new StringWriter ();
			merged.WriteXml (sw);
			Console.WriteLine ("<!-- TODO: `compatible-screens` is not defined -->");
			Console.WriteLine (sw.ToString ()
				.Replace (" api-level='10'", "")
				.Replace (" format=''", "")
				.Replace (" parent=''", ""));
		}
	}
	
	class ManifestDefinition
	{
		public int ApiLevel { get; set; }
		public IList<ElementDefinition> Elements { get; private set; } = new List<ElementDefinition> ();
		
		public static ManifestDefinition FromFileName (string filePath)
		{
			var dirName = new FileInfo (filePath).Directory.Parent.Parent.Parent.Name;
			var api = int.Parse (dirName.Substring (dirName.IndexOf ('-') + 1));
			return new ManifestDefinition () {
				ApiLevel = api,
				Elements = XDocument.Load (filePath).Root.Elements ("declare-styleable")
				.Select (e => ElementDefinition.FromElement (api, e))
				.ToList ()
				};
		}
		
		public void WriteXml (TextWriter w)
		{
			w.WriteLine ("<m>");
			foreach (var e in Elements)
				e.WriteXml (w);
			w.WriteLine ("</m>");
		}
		
		public void WriteCode (TextWriter w)
		{
			foreach (var e in Elements)
				e.WriteCode (w);
		}
	}
	
	class ElementDefinition
	{
		public int ApiLevel { get; set; }
		public string Name { get; set; }
		public string [] Parents { get; set; }
		public IList<AttributeDefinition> Attributes { get; private set; } = new List<AttributeDefinition> ();
		
		public string ActualElementName {
			get { return Name.ToActualName (); }
		}
		
		static readonly char [] sep = new char [] {' '};
		
		public static ElementDefinition FromElement (int api, XElement e)
		{
			return new ElementDefinition () {
				ApiLevel = api,
				Name = e.Attribute ("name").Value,
				Parents = e.Attribute ("parent")?.Value?.Split ().Select (s => s.Trim ()).Where (s => !string.IsNullOrWhiteSpace (s)).ToArray (),
				Attributes = e.Elements ("attr")
					.Select (a => AttributeDefinition.FromElement (api, a))
					.ToList ()
				};
		}
		
		public void WriteXml (TextWriter w)
		{
			w.WriteLine ($"    <e name='{ActualElementName}' api-level='{ApiLevel}'>");
			if (Parents != null && Parents.Any ())
				foreach (var p in Parents)
					w.WriteLine ($"        <parent>{p.ToActualName ()}</parent>");
			foreach (var a in Attributes)
				a.WriteXml (w);
			w.WriteLine ("    </e>");
		}
		
		public void WriteCode (TextWriter w)
		{
		}
	}
	
	class AttributeDefinition
	{
		public int ApiLevel { get; set; }
		public string Name { get; set; }
		public string Format { get; set; }
		public IList<EnumDefinition> Enums { get; set; } = new List<EnumDefinition> ();
		
		public static AttributeDefinition FromElement (int api, XElement e)
		{
			return new AttributeDefinition {
				ApiLevel = api,
				Name = e.Attribute ("name").Value,
				Format = e.Attribute ("format")?.Value,
				Enums = e.Elements ("enum")
					.Select (n => new EnumDefinition {
						ApiLevel = api,
						Name = n.Attribute ("name").Value,
						Value = n.Attribute ("value").Value,
					})
					.ToList ()
			};
		}
		
		public void WriteXml (TextWriter w)
		{
			w.Write ($"        <a name='{Name}' format='{Format}' api-level='{ApiLevel}'");
			if (Enums.Any ()) {
				w.WriteLine (">");
				foreach (var e in Enums)
					w.WriteLine ($"            <enum-definition name='{e.Name}' value='{e.Value}' api-level='{e.ApiLevel}' />");
				w.WriteLine ("        </a>");
			}
			else
				w.WriteLine (" />");
		}
	}
	
	class EnumDefinition
	{
		public int ApiLevel { get; set; }
		public string Name { get; set; }
		public string Value { get; set; }
	}
	
	static class StringExtensions
	{
		static StringExtensions ()
		{
			// micro unit testing, am so clever!
			if (Hyphenate ("AndSoOn") != "and-so-on")
				throw new InvalidOperationException ("Am so buggy 1 " + Hyphenate ("AndSoOn"));
			if (Hyphenate ("aBigProblem") != "a-big-problem")
				throw new InvalidOperationException ("Am so buggy 2");
			if (Hyphenate ("my-two-cents") != "my-two-cents")
				throw new InvalidOperationException ("Am so buggy 3");
		}
		
		public static string Hyphenate (this string s)
		{
			var sb = new StringBuilder (s.Length * 2);
			for (int i = 0; i < s.Length; i++) {
				if (char.IsUpper (s [i])) {
					if (i > 0)
						sb.Append ('-');
					sb.Append (char.ToLowerInvariant (s [i]));
				}
				else
					sb.Append (s [i]);
			}
			return sb.ToString ();
		}

		const string prefix = "AndroidManifest";
		
		public static string ToActualName (this string s)
		{
			s = s.IndexOf ('.') < 0 ? s : s.Substring (s.LastIndexOf ('.') + 1);
		
			var ret = (s.StartsWith (prefix, StringComparison.Ordinal) ? s.Substring (prefix.Length) : s).Hyphenate ();
			return ret.Length == 0 ? "manifest" : ret;
		}
	}
}	
