#nullable enable

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GenerateProguardConfiguration : AndroidTask
	{
		public override string TaskPrefix => "GPC";

		[Required]
		public ITaskItem[] LinkedAssemblies { get; set; } = [];

		[Required]
		public string OutputFile { get; set; } = "";

		public override bool RunTask ()
		{
			var dir = Path.GetDirectoryName (OutputFile);
			if (!Directory.Exists (dir))
				Directory.CreateDirectory (dir);
			using var writer = File.CreateText (OutputFile);

			foreach (var assembly in LinkedAssemblies) {
				ProcessAssembly (assembly.ItemSpec, writer);
			}

			return !Log.HasLoggedErrors;
		}

		void ProcessAssembly (string assemblyPath, TextWriter writer)
		{
			try {
				using var stream = File.OpenRead (assemblyPath);
				using var pe = new PEReader (stream);

				if (!pe.HasMetadata)
					return;

				var reader = pe.GetMetadataReader ();

				// Those assemblies that do not reference Mono.Android.dll (such as System.*
				// assemblies and Mono.Android.dll itself) can be skipped.
				// (Mono.Android.dll is special; android.jar is not part of classes.dex).
				//
				// FIXME: Those non-embedded jar bindings could visit here too, and they don't have to
				// be part of proguard configuration. But they don't break (they will be NOTEd though).
				if (!ReferencesMonoAndroid (reader))
					return;

				var assemblyName = reader.GetString (reader.GetAssemblyDefinition ().Name);
				writer.WriteLine ($"# ACW for {assemblyName}");

				foreach (var typeHandle in reader.TypeDefinitions) {
					var type = reader.GetTypeDefinition (typeHandle);
					ProcessType (reader, type, writer);
				}
			} catch (BadImageFormatException ex) {
				// Skip non-managed assemblies
				Log.LogDebugMessage ($"Could not read assembly '{assemblyPath}': {ex.Message}");
			}
		}

		static bool ReferencesMonoAndroid (MetadataReader reader)
		{
			foreach (var refHandle in reader.AssemblyReferences) {
				var reference = reader.GetAssemblyReference (refHandle);
				if (reader.GetString (reference.Name) == "Mono.Android")
					return true;
			}
			return false;
		}

		void ProcessType (MetadataReader reader, TypeDefinition type, TextWriter writer)
		{
			// RegisterAttribute can be applied to interfaces, but proguard rules are only needed for classes.
			// Structs don't need to be checked because RegisterAttribute cannot be applied to them.
			if ((type.Attributes & System.Reflection.TypeAttributes.Interface) != 0)
				return;

			string? javaTypeName = null;
			foreach (var attrHandle in type.GetCustomAttributes ()) {
				var attr = reader.GetCustomAttribute (attrHandle);
				var attrName = reader.GetCustomAttributeFullName (attr, Log);
				if (attrName == "Android.Runtime.RegisterAttribute") {
					var args = attr.GetCustomAttributeArguments ();
					if (args.FixedArguments.Length > 0 && args.FixedArguments[0].Value is string jtype) {
						javaTypeName = jtype.Replace ('/', '.');
					}
					break;
				}
			}

			if (javaTypeName == null)
				return;

			writer.WriteLine ($"-keep class {javaTypeName}");
			writer.WriteLine ($"-keepclassmembers class {javaTypeName} {{");

			foreach (var methodHandle in type.GetMethods ()) {
				ProcessMethod (reader, methodHandle, writer);
			}

			writer.WriteLine ("}");
			writer.WriteLine ();
		}

		void ProcessMethod (MetadataReader reader, MethodDefinitionHandle methodHandle, TextWriter writer)
		{
			var method = reader.GetMethodDefinition (methodHandle);

			foreach (var attrHandle in method.GetCustomAttributes ()) {
				var attr = reader.GetCustomAttribute (attrHandle);
				var attrName = reader.GetCustomAttributeFullName (attr, Log);
				if (attrName == "Android.Runtime.RegisterAttribute") {
					var args = attr.GetCustomAttributeArguments ();
					if (args.FixedArguments.Length >= 2 &&
					    args.FixedArguments[0].Value is string jname &&
					    args.FixedArguments[1].Value is string) {
						if (jname == ".ctor") {
							writer.WriteLine ("   <init>(...);");
						} else {
							writer.WriteLine ($"   *** {jname}(...);");
						}
					}
					break;
				}
			}
		}
	}
}
