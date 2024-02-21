using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;

public class CallableWrapperTypeAnnotation
{
	public string Name { get; set; }
	public List<KeyValuePair<string, string>> Properties { get; } = new ();

	public CallableWrapperTypeAnnotation (string name)
	{
		Name = name;
	}

	public void Generate (TextWriter sw, string indent, CallableWrapperWriterOptions options)
	{
		sw.Write (indent);
		sw.Write ('@');
		sw.Write (Name);

		var properties = string.Join (", ", Properties.Select (p => $"{p.Key} = {p.Value}"));

		if (!string.IsNullOrEmpty (properties)) {
			sw.Write (" (");
			sw.Write (properties);
			sw.Write (")");
		}

		sw.WriteLine ();
	}
}
