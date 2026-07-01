using System;
using System.Xml;
using System.Xml.Linq;

namespace Java.Interop.Tools.JavaCallableWrappers.Extensions;

static class XmlExtensions
{
	public static T GetAttributeOrDefault<T> (this XElement xml, string name, T defaultValue)
	{
		var value = xml.Attribute (name)?.Value;

		if (string.IsNullOrWhiteSpace (value))
			return defaultValue;

		return (T) Convert.ChangeType (value, typeof (T));
	}

	public static string GetRequiredAttribute (this XElement xml, string name)
	{
		var value = xml.Attribute (name)?.Value;

		if (string.IsNullOrWhiteSpace (value))
			throw new InvalidOperationException ($"Missing required attribute '{name}' within element `{xml.ToString()}`.");

		return value!;  // NRT - Guarded by IsNullOrWhiteSpace check above
	}

	public static void WriteAttributeStringIfNotNull (this XmlWriter xml, string name, string? value)
	{
		if (value is not null)
			xml.WriteAttributeString (name, value);
	}

	public static void WriteAttributeStringIfNotFalse (this XmlWriter xml, string name, bool value)
	{
		// If value is false, don't write the attribute, we'll default to false on import
		if (value)
			xml.WriteAttributeString (name, value.ToString ());
	}
}
