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
		public JavaLangObjectConstructor (ClassGen klass, CodeGenerationOptions opt)
		{
			Name = klass.Name;

			if (klass.IsFinal)
				IsInternal = true;
			else
				IsProtected = true;

			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				Parameters.Add (new MethodParameterWriter ("reference", new TypeReferenceWriter ("ref JniObjectReference")));
				Parameters.Add (new MethodParameterWriter ("options", new TypeReferenceWriter ("JniObjectReferenceOptions")));

				BaseCall = "base (ref reference, options)";
			} else {
				Parameters.Add (new MethodParameterWriter ("javaReference", TypeReferenceWriter.IntPtr));
				Parameters.Add (new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")));

				BaseCall = "base (javaReference, transfer)";
			}
		}
	}
}
