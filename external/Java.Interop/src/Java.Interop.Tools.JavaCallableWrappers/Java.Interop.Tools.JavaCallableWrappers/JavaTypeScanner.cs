using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.TypeNameMappings;

namespace Java.Interop.Tools.JavaCallableWrappers
{
	public class JavaTypeScanner
	{
		public  Action<TraceLevel, string>      Logger                      { get; private set; }
		public  bool                            ErrorOnCustomJavaObject     { get; set; }

		readonly IMetadataResolver cache;

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public JavaTypeScanner (Action<TraceLevel, string> logger)
			: this (logger, resolver: null)
		{ }

		public JavaTypeScanner (Action<TraceLevel, string> logger, TypeDefinitionCache? cache)
			: this (logger, (IMetadataResolver?) cache)
		{
		}

		public JavaTypeScanner (Action<TraceLevel, string> logger, IMetadataResolver? resolver)
		{
			if (logger == null)
				throw new ArgumentNullException (nameof (logger));
			Logger      = logger;
			this.cache = cache ?? new TypeDefinitionCache ();
		}

		public List<TypeDefinition> GetJavaTypes (IEnumerable<string> assemblies, IAssemblyResolver resolver)
		{
			var javaTypes = new List<TypeDefinition> ();

			foreach (var assembly in assemblies) {
				var assm = resolver.GetAssembly (assembly);

				foreach (ModuleDefinition md in assm.Modules) {
					foreach (TypeDefinition td in md.Types) {
						AddJavaTypes (javaTypes, td);
					}
				}
			}

			return javaTypes;
		}

		void AddJavaTypes (List<TypeDefinition> javaTypes, TypeDefinition type)
		{
			if (type.IsSubclassOf ("Java.Lang.Object", cache) ||
					type.IsSubclassOf ("Java.Lang.Throwable", cache) ||
					(type.IsInterface && type.ImplementsInterface ("Java.Interop.IJavaPeerable", cache))) {
				// For subclasses of e.g. Android.App.Activity.
				javaTypes.Add (type);
			} else if (type.IsClass && !type.IsSubclassOf ("System.Exception", cache) && type.ImplementsInterface ("Android.Runtime.IJavaObject", cache)) {
				var level   = ErrorOnCustomJavaObject ? TraceLevel.Error : TraceLevel.Warning;
				var prefix  = ErrorOnCustomJavaObject ? "error" : "warning";
				Logger (
						level,
						$"{prefix} XA4212: Type `{type.FullName}` implements `Android.Runtime.IJavaObject` but does not inherit `Java.Lang.Object` or `Java.Lang.Throwable`. This is not supported.");
				return;
			}

			if (!type.HasNestedTypes)
				return;

			foreach (TypeDefinition nested in type.NestedTypes)
				AddJavaTypes (javaTypes, nested);
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool ShouldSkipJavaCallableWrapperGeneration (TypeDefinition type) =>
			ShouldSkipJavaCallableWrapperGeneration (type, resolver: null);

		public static bool ShouldSkipJavaCallableWrapperGeneration (TypeDefinition type, TypeDefinitionCache? cache) =>
			ShouldSkipJavaCallableWrapperGeneration (type, (IMetadataResolver?) cache);

		public static bool ShouldSkipJavaCallableWrapperGeneration (TypeDefinition type, IMetadataResolver? resolver)
		{
			if (JavaNativeTypeManager.IsNonStaticInnerClass (type, resolver))
				return true;

			foreach (var c in JavaCallableWrapperGenerator.GetTypeRegistrationAttributes (type)) {
				if (c.DoNotGenerateAcw) {
					return true;
				}
			}

			return false;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static List<TypeDefinition> GetJavaTypes (IEnumerable<string> assemblies, IAssemblyResolver resolver, Action<string, object []> log) =>
			GetJavaTypes (assemblies, resolver, log, metadataResolver: null);

		// Returns all types for which we need to generate Java delegate types.
		public static List<TypeDefinition> GetJavaTypes (IEnumerable<string> assemblies, IAssemblyResolver resolver, Action<string, object []> log, TypeDefinitionCache? cache) =>
			GetJavaTypes (assemblies, resolver, log, (IMetadataResolver?) cache);

		public static List<TypeDefinition> GetJavaTypes (IEnumerable<string> assemblies, IAssemblyResolver resolver, Action<string, object []> log, IMetadataResolver? metadataResolver)
		{
			Action<TraceLevel, string> l = (level, value) => log ("{0}", new string [] { value });
			return new JavaTypeScanner (l, metadataResolver).GetJavaTypes (assemblies, resolver);
		}
	}
}
