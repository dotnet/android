using Xamarin.SourceWriter;

namespace generator.SourceWriters;

public class DebuggerBrowsableAttr : AttributeWriter
{
	public override void WriteAttribute (CodeWriter writer)
	{
		writer.WriteLine ("[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]");
	}
}
