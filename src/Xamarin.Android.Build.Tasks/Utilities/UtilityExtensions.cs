#nullable enable
using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Xamarin.Android.Tasks;

static class UtilityExtensions
{
	public static T GetAttributeOrDefault<T> (this XElement xml, string name, T defaultValue)
	{
		var value = xml.Attribute (name)?.Value;

		if (value.IsNullOrWhiteSpace ())
			return defaultValue;

		return (T) Convert.ChangeType (value, typeof (T));
	}

	public static string GetRequiredAttribute (this XElement xml, string name)
	{
		var value = xml.Attribute (name)?.Value;

		if (value.IsNullOrWhiteSpace ())
			throw new InvalidOperationException ($"Missing required attribute '{name}'");

		return value!;  // NRT - Guarded by IsNullOrWhiteSpace check above
	}

	public static void WriteAttributeStringIfNotDefault (this XmlWriter xml, string name, string? value)
	{
		if (value.HasValue ())
			xml.WriteAttributeString (name, value);
	}

	public static void WriteAttributeStringIfNotDefault (this XmlWriter xml, string name, bool value)
	{
		// If value is false, don't write the attribute, we'll default to false on import
		if (value)
			xml.WriteAttributeString (name, value.ToString ());
	}
}
