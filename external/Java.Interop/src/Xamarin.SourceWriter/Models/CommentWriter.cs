using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public class CommentWriter : ISourceWriter
	{
		public string Value { get; set; }
		public int Priority { get; set; }

		public CommentWriter (string value)
		{
			Value = value;
		}

		public virtual void Write (CodeWriter writer)
		{
			writer.WriteLine (Value);
		}
	}
}
