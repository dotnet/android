using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public interface ISourceWriter
	{
		void Write (CodeWriter writer);

		// This is for testing compatibility, allowing us to write members
		// in the same order as previous generator.
		int Priority { get; set; }
	}
}
