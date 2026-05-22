#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GenerateProguardConfiguration : AndroidTask
	{
		public override string TaskPrefix => "GPC";

		public ITaskItem[] LinkedAssemblies { get; set; } = [];

		[Required]
		public string OutputFile { get; set; } = "";

		public string NativeAotDgmlFile { get; set; } = "";

		public string AcwMapFile { get; set; } = "";

		public override bool RunTask ()
		{
			var dir = Path.GetDirectoryName (OutputFile);
			if (!dir.IsNullOrEmpty () && !Directory.Exists (dir)) {
				Directory.CreateDirectory (dir);
			}
			using var writer = File.CreateText (OutputFile);

			if (!NativeAotDgmlFile.IsNullOrEmpty ()) {
				ProcessNativeAotDgml (writer);
				return !Log.HasLoggedErrors;
			}

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

		void ProcessNativeAotDgml (TextWriter writer)
		{
			if (!File.Exists (NativeAotDgmlFile)) {
				Log.LogError ("NativeAOT DGML file '{0}' was not found.", NativeAotDgmlFile);
				return;
			}
			if (AcwMapFile.IsNullOrEmpty () || !File.Exists (AcwMapFile)) {
				Log.LogError ("ACW map file '{0}' was not found.", AcwMapFile);
				return;
			}

			var retainedTypeKeys = LoadRetainedTypeKeysFromDgml ();
			var javaTypes = LoadJavaTypesFromAcwMap (retainedTypeKeys);

			writer.WriteLine ("# ACWs retained by NativeAOT ILC");
			foreach (var javaTypeName in javaTypes) {
				writer.WriteLine ($"-keep class {javaTypeName} {{ *; }}");
			}

			Log.LogMessage (MessageImportance.Low, $"Generated {javaTypes.Count} NativeAOT trimmable typemap ProGuard rules from '{NativeAotDgmlFile}'.");
		}

		SortedSet<string> LoadJavaTypesFromAcwMap (HashSet<string> retainedTypeKeys)
		{
			var javaTypes = new SortedSet<string> (StringComparer.Ordinal);
			foreach (var line in File.ReadLines (AcwMapFile)) {
				var separator = line.IndexOf (';');
				if (separator <= 0 || separator == line.Length - 1) {
					continue;
				}
				var managedTypeName = line.Substring (0, separator);
				var javaTypeName = line.Substring (separator + 1);
				if (retainedTypeKeys.Contains (managedTypeName)) {
					javaTypes.Add (javaTypeName);
				}
			}
			return javaTypes;
		}

		HashSet<string> LoadRetainedTypeKeysFromDgml ()
		{
			var typeKeys = new HashSet<string> (StringComparer.Ordinal);
			using var reader = XmlReader.Create (NativeAotDgmlFile, new XmlReaderSettings {
				DtdProcessing = DtdProcessing.Prohibit,
			});

			while (reader.Read ()) {
				if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Node") {
					continue;
				}

				var label = reader.GetAttribute ("Label");
				if (label.IsNullOrEmpty () || !label.StartsWith ("Type metadata: [", StringComparison.Ordinal)) {
					continue;
				}

				var assemblyStart = "Type metadata: [".Length;
				var assemblyEnd = label.IndexOf (']', assemblyStart);
				if (assemblyEnd < 0 || assemblyEnd == label.Length - 1) {
					continue;
				}

				var assemblyName = label.Substring (assemblyStart, assemblyEnd - assemblyStart);
				var managedTypeName = label.Substring (assemblyEnd + 1);
				typeKeys.Add (managedTypeName);
				typeKeys.Add ($"{managedTypeName}, {assemblyName}");
			}

			return typeKeys;
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
