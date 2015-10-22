using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Java.Interop;

namespace Java.InteropTests
{
	public class TestJVM : JreVM {

		static JreVMBuilder CreateBuilder (string[] jars)
		{
			var builder = new JreVMBuilder ();
			if (jars != null) {
				foreach (var jar in jars)
					builder.ClassPath.Add (jar);
			}
			builder.AddOption ("-Xcheck:jni");
			builder.JniObjectReferenceManager   = LoggingJniObjectReferenceManagerDecorator.GetObjectReferenceManager (new Java.Interop.JniObjectReferenceManager ());

			return builder;
		}

		Dictionary<string, Type> typeMappings;

		public TestJVM (string[] jars = null, Dictionary<string, Type> typeMappings = null)
			: base (CreateBuilder (jars))
		{
			this.typeMappings = typeMappings;
		}

		public override Type GetTypeForJniSimplifiedTypeReference (string jniTypeReference)
		{
			Type target = base.GetTypeForJniSimplifiedTypeReference (jniTypeReference);
			if (target != null)
				return target;
			if (typeMappings != null && typeMappings.TryGetValue (jniTypeReference, out target))
				return target;
			return null;
		}
	}
}

