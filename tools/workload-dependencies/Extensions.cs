#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

static class Extensions
{
	public static IEnumerable<XElement> GetSupportedElements (XDocument doc, string element)
	{
		if (doc.Root == null) {
			return [];
		}
		return doc.Root.Elements (element)
			.Where (e =>
				string.Equals ("False", e.ReqAttr ("obsolete"), StringComparison.OrdinalIgnoreCase) &&
				string.Equals ("False", e.ReqAttr ("preview"), StringComparison.OrdinalIgnoreCase))
			;
	}

	public static IEnumerable<(XElement Element, string Revision, Version Version)> GetByRevisions (XDocument doc, string element)
	{
		return GetSupportedElements (doc, element)
			.OrderByRevision ();
	}

	public static string? GetLatestRevision (XDocument doc, string element, Version minimumVersion, Version maximumVersion)
	{
		return GetByRevisions (doc, element)
			.Where (item => item.Version >= minimumVersion && item.Version < maximumVersion)
			.LastOrDefault ()
			.Revision;
	}

	public static string ReqAttr (this XElement e, string attribute)
	{
		var v = (string?) e.Attribute (attribute);
		if (v == null) {
			throw new InvalidOperationException ($"Missing required attribute `{attribute}` in: `{e}");
		}
		return v;
	}

	public static IEnumerable<(XElement Element, string Revision, Version Version)> OrderByRevision (this IEnumerable<XElement> elements)
	{
		return from e in elements
			let     revision    = e.ReqAttr ("revision")
			let     version     = new Version (revision.Contains (".") ? revision : revision + ".0")
			orderby version
			select (e, revision, version);
	}
}
