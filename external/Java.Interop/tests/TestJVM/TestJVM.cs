using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Java.Interop;

namespace Java.InteropTests
{
	public class TestJVM : JreRuntime {

		static JreRuntimeOptions CreateBuilder (string[] jars)
		{
			var builder = new JreRuntimeOptions ();
			if (jars != null) {
				foreach (var jar in jars)
					builder.ClassPath.Add (jar);
			}
			builder.AddOption ("-Xcheck:jni");
			builder.ObjectReferenceManager      = LoggingJniObjectReferenceManagerDecorator.GetObjectReferenceManager (new Java.Interop.JniObjectReferenceManager ());
			builder.TypeManager                 = new JreTypeManager ();

			return builder;
		}

		Dictionary<string, Type> typeMappings;

		public TestJVM (string[] jars = null, Dictionary<string, Type> typeMappings = null)
			: base (CreateBuilder (jars))
		{
			this.typeMappings = typeMappings;
		}

		class JreTypeManager : JniTypeManager {

			protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
			{
				foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference))
					yield return t;
				var mappings = ((TestJVM) Runtime).typeMappings;
				Type target;
				if (mappings != null && mappings.TryGetValue (jniSimpleReference, out target))
					yield return target;
			}
		}
	}
}

