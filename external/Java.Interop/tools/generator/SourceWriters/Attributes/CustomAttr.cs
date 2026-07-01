using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class CustomAttr : AttributeWriter
	{
		public string Value { get; set; }

		public CustomAttr (string value)
		{
			Value = value;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			writer.WriteLine (Value);
		}
	}
}
