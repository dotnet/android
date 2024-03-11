using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven.Extensions;

class PropertyStack
{
	// Why go to this trouble?
	// A property can be specified in both a child POM and its parent POM.
	// Even if the property is being consumed in the parent POM, the property in
	// the child POM takes precedence.
	readonly List<List<KeyValuePair<string, string>>> stack = new ();

	public void Push (ModelProperties? properties)
	{
		// We add a new list to the stack, even if it's empty, so that the Pop works later
		var list = new List<KeyValuePair<string, string>> ();

		if (properties?.Any is Collection<XElement> props)
			foreach (var prop in props)
				list.Add (new KeyValuePair<string, string> (prop.Name.LocalName, prop.Value));

		stack.Add (list);
	}

	public void Pop ()
	{
		stack.RemoveAt (stack.Count - 1);
	}

	public string Apply (string value)
	{
		if (stack.Count == 0 || !value.Contains ("${"))
			return value;

		foreach (var property_set in stack) {
			foreach (var prop in property_set)
				value = value.Replace ($"${{{prop.Key}}}", prop.Value);
		}

		return value;
	}
}
