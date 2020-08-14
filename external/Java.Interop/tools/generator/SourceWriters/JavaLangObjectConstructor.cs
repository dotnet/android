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
	public class JavaLangObjectConstructor : ConstructorWriter
	{
		public JavaLangObjectConstructor (ClassGen klass)
		{
			Name = klass.Name;

			if (klass.IsFinal)
				IsInternal = true;
			else
				IsProtected = true;

			Parameters.Add (new MethodParameterWriter ("javaReference", TypeReferenceWriter.IntPtr));
			Parameters.Add (new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")));

			BaseCall = "base (javaReference, transfer)";
		}
	}
}
