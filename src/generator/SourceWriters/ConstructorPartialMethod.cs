using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.Android.Binder;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class ConstructorPartialMethod : MethodWriter
	{
		public ConstructorPartialMethod (string partialMethodName)
		{
			Name = partialMethodName;
			IsPartial = true;
			IsDeclaration = true;
			ReturnType = new TypeReferenceWriter ("void");
		}
	}
}
