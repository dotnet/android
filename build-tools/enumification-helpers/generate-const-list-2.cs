using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;


public class Driver
{
	class ConstantCandidate {
		public string Package, ParentType, Level, FieldType, Name, Value;
		public bool IsTypeInterface, IsFinal;
	}
	
	static string GetApiLevel (string file)
	{
		Console.Error.WriteLine ($"[{file}]");
		return file.Substring (0, file.Length - ".xml.in".Length).Substring ("api-".Length);
	}
	
	public static void Main (string [] args)
	{
		var docs = args.SelectMany (a => Directory.GetFiles (a, "api-*.xml.in"))
			.Select (file => XDocument.Load (file, LoadOptions.SetBaseUri));
		Dictionary<string,string> levels = docs.Select (doc => new { Doc = doc.BaseUri, File = Path.GetFileName (doc.BaseUri) })
			.Select (p => new { Doc = p.Doc, Level = GetApiLevel (p.File) })
			.ToDictionary (p => p.Doc, p => p.Level);
		var results = docs.Select (doc => doc.Root.Elements ("package"))
			.SelectMany (p => p.Elements ())
			.SelectMany (t => t.Elements ("field"))
			.Where (f => f.Attribute ("type")?.Value == "int")
			//.Where (f => f.Attribute ("final")?.Value == "true" && f.Attribute ("value") != null)
			.ToArray ();
		var consts = results.Select (f => new ConstantCandidate {
			Package = f.Parent.Parent.Attribute ("name").Value,
			ParentType = f.Parent.Attribute ("name").Value,
			IsFinal = f.Attribute ("final")?.Value == "true",
			IsTypeInterface = f.Parent.Name.LocalName == "interface",
			Name = f.Attribute ("name").Value,
			FieldType = f.Attribute ("type").Value,
			Value = f.Attribute ("value")?.Value,
			Level = levels [f.Document.BaseUri] 
			})
			.OrderBy (c => c.Package)
			.ThenBy (c => c.ParentType)
			.ThenBy (c => c.Name)
			.ThenBy (c => c.Level)
			.ToArray ();

		for (int i = 1; i < consts.Length; i++) {
			int x = 1;
			while (consts [i - x].Name == consts [i].Name && consts [i - x].ParentType == consts [i].ParentType && consts [i - x].Package == consts [i].Package && consts [i - x].Value != consts [i].Value) {
				Console.Error.WriteLine ("Overwrite field value: {0}.{1}.{2}: {3} (at {4}) -> {5} (at {6})", consts [i - x].Package, consts [i - x].ParentType, consts [i - x].Name, consts [i - x].Value, consts [i - x].Level, consts [i].Value, consts [i].Level);
				consts [i - x].Value = consts [i].Value;
				if (!consts [i - x].IsFinal)
					Console.Error.WriteLine ("Field {0}.{1}.{2} was not constant at API Level {3}", consts [i - x].Package, consts [i - x].ParentType, consts [i - x].Name, consts [i - x].Level);
				consts [i - x].IsFinal = consts [i].IsFinal;
				x++;
			}
		}
		
		consts = consts.Where (f => f.IsFinal).ToArray ();

		var fields = new List<string> ();
		string package = null, type = null;
		var writer = XmlWriter.Create (Console.Out, new XmlWriterSettings { Indent = true });
		writer.WriteStartElement ("enums");
		foreach (var c in consts) {
			if (c.Package != package) {
				if (package != null) {
					writer.WriteEndElement (); // type
					type = null;
					writer.WriteEndElement (); // package
				}
				package = c.Package;
				writer.WriteStartElement ("package");
				writer.WriteAttributeString ("name", package);
			}
			if (c.ParentType != type) {
				if (type != null)
					writer.WriteEndElement ();
				type = c.ParentType;
				writer.WriteStartElement (c.IsTypeInterface ? "interface" : "class");
				writer.WriteAttributeString ("name", c.ParentType);
				fields.Clear ();
			}
			if (fields.Contains (c.Name))
				continue;
			fields.Add (c.Name);
			writer.WriteStartElement ("const");
			writer.WriteAttributeString ("type", c.FieldType);
			writer.WriteAttributeString ("name", c.Name);
			writer.WriteAttributeString ("api-level", c.Level);
			writer.WriteString (c.Value);
			writer.WriteEndElement ();
		}
		writer.Close ();
	}
}
