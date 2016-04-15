using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.TypeNameMappings;

namespace Java.Interop.Tools.JavaCallableWrappers
{
	public static class JavaTypeScanner
	{
		// Returns all types for which we need to generate Java delegate types.
		public static List<TypeDefinition> GetJavaTypes (IEnumerable<string> assemblies, IAssemblyResolver resolver, Action<string, object[]> log)
		{
			var javaTypes = new List<TypeDefinition> ();

			foreach (var assembly in assemblies) {
				var assm = resolver.GetAssembly (assembly);

				foreach (ModuleDefinition md in assm.Modules) {
					foreach (TypeDefinition td in md.Types) {
						AddJavaTypes (javaTypes, td, log);
					}
				}
			}

			return javaTypes;
		}

		static void AddJavaTypes (List<TypeDefinition> javaTypes, TypeDefinition type, Action<string, object[]> log)
		{
			if (type.IsSubclassOf ("Java.Lang.Object") || type.IsSubclassOf ("Java.Lang.Throwable")) {

				// For subclasses of e.g. Android.App.Activity.
				javaTypes.Add (type);
			} else if (type.IsClass && !type.IsSubclassOf ("System.Exception") && type.ImplementsInterface ("Android.Runtime.IJavaObject")) {
				log (
						"Type '{0}' implements Android.Runtime.IJavaObject but does not inherit from Java.Lang.Object. It is not supported.",
						new [] { type.FullName });
				return;
			}

			if (!type.HasNestedTypes)
				return;

			foreach (TypeDefinition nested in type.NestedTypes)
				AddJavaTypes (javaTypes, nested, log);
		}

		public static bool ShouldSkipJavaCallableWrapperGeneration (TypeDefinition type)
		{
			if (JniType.IsNonStaticInnerClass (type))
				return true;

			foreach (var r in type.GetCustomAttributes (typeof (global::Android.Runtime.RegisterAttribute))) {

				if (JavaCallableWrapperGenerator.ToRegisterAttribute (r).DoNotGenerateAcw) {
					return true;
				}
			}

			return false;
		}

	}
}
