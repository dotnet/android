using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class WeakImplementorField : FieldWriter
	{
		public WeakImplementorField (string name, CodeGenerationOptions opt)
		{
			Name = "weak_implementor_" + name;
			Type = new TypeReferenceWriter (opt.GetOutputName ("WeakReference")) { Nullable = opt.SupportNullableReferenceTypes };
		}		
	}
}
