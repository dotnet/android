using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public abstract class AttributeWriter
	{
		public virtual void WriteAttribute (CodeWriter writer) { }
	}
}
