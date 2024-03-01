using System.Text;
using System.Xml.Linq;

namespace Xamarin.Android.Tools.ManifestAttributeCodeGenerator;

class MetadataSource
{
	public Dictionary<string, MetadataType> Types { get; } = new ();
	public Dictionary<string, MetadataElement> Elements { get; } = new ();

	static readonly MetadataElement default_element = new MetadataElement ("*");


	public MetadataSource (string filename)
	{
		var xml = XElement.Load (filename);

		foreach (var element in xml.Elements ("element")) {
			var path = element.Attribute ("path")?.Value ?? throw new InvalidDataException ("Missing 'path' attribute.");

			Elements.Add (path, new MetadataElement (path) {
				Visible = element.GetAsBoolOrNull ("visible"),
				Type = element.Attribute ("type")?.Value,
				Name = element.Attribute ("name")?.Value,
				Obsolete = element.Attribute ("obsolete")?.Value,
				ReadOnly = element.GetAsBoolOrNull ("readonly") ?? false,
			});
		}

		foreach (var element in xml.Elements ("type")) {
			var el = new MetadataType (element);
			Types.Add (el.Name, el);
		}
	}

	public MetadataElement GetMetadata (string path)
	{
		if (Elements.TryGetValue (path, out var element)) {
			element.Consumed = true;
			return element;
		}

		return default_element;
	}

	public void EnsureMetadataElementsConsumed ()
	{
		var unconsumed = Elements.Values.Where (e => !e.Consumed).ToList ();

		if (unconsumed.Count == 0)
			return;

		var sb = new StringBuilder ();
		sb.AppendLine ("The following metadata elements were not consumed:");

		foreach (var e in unconsumed)
			sb.AppendLine ($"- {e.Path}");

		throw new InvalidOperationException (sb.ToString ());
	}

	public void EnsureMetadataTypesConsumed ()
	{
		var unconsumed = Types.Values.Where (t => !t.Consumed && !t.Ignore).ToList ();

		if (unconsumed.Count == 0)
			return;

		var sb = new StringBuilder ();
		sb.AppendLine ("The following metadata types were not consumed:");

		foreach (var t in unconsumed)
			sb.AppendLine ($"- {t.Name}");

		throw new InvalidOperationException (sb.ToString ());
	}

	public void EnsureAllTypesAccountedFor (IEnumerable<ElementDefinition> elements)
	{
		var missing = new List<string> ();

		foreach (var e in elements) {
			if (!Types.ContainsKey (e.ActualElementName))
				missing.Add (e.ActualElementName);
		}

		if (missing.Count == 0)
			return;

		var sb = new StringBuilder ();
		sb.AppendLine ("The following types were not accounted for:");

		foreach (var m in missing.Order ())
			sb.AppendLine ($"- {m}");

		throw new InvalidOperationException (sb.ToString ());
	}
}

class MetadataElement
{
	public string Path { get; set; }
	public bool? Visible { get; set; }
	public string? Type { get; set; }
	public string? Name { get; set; }
	public string? Obsolete { get; set; }
	public bool ReadOnly { get; set; }
	public bool Consumed { get; set; }

	public MetadataElement (string path)
	{
		Path = path;
	}
}

public class MetadataType
{
	public string Name { get; set; }
	public string ManagedName { get; set; } = string.Empty;
	public string Namespace { get; set; } = string.Empty;
	public bool Ignore { get; set; }
	public string OutputFile { get; set; } = string.Empty;
	public string Usage { get; set; } = string.Empty;
	public bool AllowMultiple { get; set; }
	public bool IsJniNameProvider { get; set; }
	public bool HasDefaultConstructor { get; set; }
	public bool IsSealed { get; set; }
	public bool Consumed { get; set; }


	public MetadataType (XElement element)
	{
		Name = element.GetRequiredAttributeString ("name");
		Ignore = element.GetAttributeBoolOrDefault ("ignore", false);

		if (Ignore)
			return;

		Namespace = element.GetRequiredAttributeString ("namespace");
		OutputFile = element.GetRequiredAttributeString ("outputFile");
		Usage = element.GetRequiredAttributeString ("usage");
		AllowMultiple = element.GetAttributeBoolOrDefault ("allowMultiple", false);
		IsJniNameProvider = element.GetAttributeBoolOrDefault ("jniNameProvider", false);
		HasDefaultConstructor = element.GetAttributeBoolOrDefault("defaultConstructor", true);
		IsSealed = element.GetAttributeBoolOrDefault ("sealed", true);
		ManagedName = element.Attribute ("managedName")?.Value ?? Name.Unhyphenate ().Capitalize () + "Attribute";		
	}
}
