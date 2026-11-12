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
		public JavaLangObjectConstructor (ClassGen klass, CodeGenerationOptions opt, string callPartialMethod)
		{
			Name = klass.Name;
			IsUnsafe = SourceWriterExtensions.IsThrowable (klass);
			if (IsUnsafe) {
				Comments.Add ("/// <safety>");
				Comments.Add ("/// javaReference must be zero or a live compatible JNI object. Use an attached thread");
				Comments.Add ("/// (locals: creating thread), with no concurrent deletion through aliases during construction.");
				Comments.Add ("/// transfer must match ownership; transferred references cannot be used again,");
				Comments.Add ("/// even if construction throws.");
				Comments.Add ("/// </safety>");
			}

			if (klass.IsFinal)
				IsInternal = true;
			else
				IsProtected = true;

			Parameters.Add (new MethodParameterWriter ("javaReference", TypeReferenceWriter.IntPtr));
			Parameters.Add (new MethodParameterWriter ("transfer", new TypeReferenceWriter ("JniHandleOwnership")));

			BaseCall = "base (javaReference, transfer)";
			if (!string.IsNullOrWhiteSpace (callPartialMethod)) {
				Body.Add ($"{callPartialMethod} ();");
			}
		}
	}
}
