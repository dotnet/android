using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class DebuggerBrowsableAttr : AttributeWriter
	{
		public override void WriteAttribute (CodeWriter writer)
		{
			writer.WriteLine ("[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]");
		}
	}
}
