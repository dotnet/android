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
			Comments.Add ("/// <safety>javaReference must be zero or a live compatible JNI object. Use an attached thread (locals: creating thread), with no concurrent deletion through aliases during construction. transfer must match ownership; transferred references cannot be used again, even if construction throws.</safety>");
			if (!IsUnsafe)
				Comments.Add ("/// <remarks>This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference obligations still apply.</remarks>");

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
