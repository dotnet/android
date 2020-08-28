using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class EditorBrowsableAttr : AttributeWriter
	{
		public override void WriteAttribute (CodeWriter writer)
		{
			writer.WriteLine ("[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]");
		}
	}
}
