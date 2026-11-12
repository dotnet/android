using System;
using System.Collections.Generic;

namespace Xamarin.SourceWriter
{
	public class EnumMemberWriter : ISourceWriter
	{
		public string Name { get; set; }
		public List<string> Comments { get; } = new List<string> ();
		public List<AttributeWriter> Attributes { get; } = new List<AttributeWriter> ();
		public string Value { get; set; }
		public int Priority { get; set; }

		public virtual void Write (CodeWriter writer)
		{
			WriteComments (writer);
			WriteAttributes (writer);
			WriteSignature (writer);
		}

		public virtual void WriteComments (CodeWriter writer)
		{
			foreach (var c in Comments)
				writer.WriteLine (c);
		}

		public virtual void WriteAttributes (CodeWriter writer)
		{
			foreach (var att in Attributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteSignature (CodeWriter writer)
		{
			if (Value.HasValue ()) {
				writer.Write (Name + " = ");
				writer.Write (Value);
				writer.WriteLine (",");
			} else {
				writer.WriteLine ($"{Name},");
			}
		}
	}
}
