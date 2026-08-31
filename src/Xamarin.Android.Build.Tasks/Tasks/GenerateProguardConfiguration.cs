#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Tasks
{
	public class GenerateProguardConfiguration : AndroidTask
	{
		public override string TaskPrefix => "GPC";

		[Required]
		public ITaskItem [] LinkedAssemblies { get; set; } = [];

		[Required]
		public string OutputFile { get; set; } = "";

		public string? R8MappingFile { get; set; }

		public string? R8ReachabilityManifestFile { get; set; }

		R8Mapping? r8Mapping;

		public override bool RunTask ()
		{
			if (!R8MappingFile.IsNullOrEmpty ()) {
				r8Mapping = R8Mapping.Load (R8MappingFile);
			}
			var dir = Path.GetDirectoryName (OutputFile);
			if (!dir.IsNullOrEmpty () && !Directory.Exists (dir)) {
				Directory.CreateDirectory (dir);
			}
			using var writer = new StringWriter ();

			R8Mapping? mapping = r8Mapping;
			if (mapping != null) {
				foreach (var assembly in LinkedAssemblies) {
					ScanRewrittenAssembly (assembly.ItemSpec, mapping);
				}
				WriteMappedRules (writer, mapping);
			} else {
				foreach (var assembly in LinkedAssemblies) {
					ProcessAssembly (assembly.ItemSpec, writer);
				}
			}
			File.WriteAllText (OutputFile, writer.ToString ());
			if (!R8ReachabilityManifestFile.IsNullOrEmpty ()) {
				WriteReachabilityManifest (R8ReachabilityManifestFile);
			}

			return !Log.HasLoggedErrors;
		}

		void WriteReachabilityManifest (string path)
		{
			string? directory = Path.GetDirectoryName (path);
			if (!directory.IsNullOrEmpty ()) {
				Directory.CreateDirectory (directory);
			}
			File.WriteAllText (path, R8Mapping.CreateManifestContent (r8Mapping?.AccessedEntries ?? []));
		}

		void ScanRewrittenAssembly (string assemblyPath, R8Mapping mapping)
		{
			try {
				using var stream = File.OpenRead (assemblyPath);
				using var pe = new PEReader (stream);
				if (!pe.HasMetadata) {
					return;
				}
				MetadataReader reader = pe.GetMetadataReader ();
				JniAssemblyRewriter.ScanRewrittenAssembly (pe, reader, mapping, Log);
			} catch (BadImageFormatException ex) {
				Log.LogDebugMessage ($"Could not read assembly '{assemblyPath}': {ex.Message}");
			} catch (JniRewriteException ex) {
				Log.LogCodedError ("XA4307", Properties.Resources.XA4307,
					$"Could not scan the linked assembly '{assemblyPath}' for rewritten JNI references: {ex.Message}");
			}
		}

		void WriteMappedRules (TextWriter writer, R8Mapping mapping)
		{
			var rules = new SortedDictionary<string, SortedSet<string>> (StringComparer.Ordinal);
			foreach (string entry in mapping.AccessedEntries) {
				string [] parts = entry.Split ('\t');
				if (parts.Length < 2) {
					continue;
				}
				if (!rules.TryGetValue (parts [1], out var members)) {
					rules [parts [1]] = members = new SortedSet<string> (StringComparer.Ordinal);
				}
				if (parts.Length != 3) {
					continue;
				}

				if (parts [0] == "F") {
					members.Add ($"   *** {parts [2]};");
				} else if (parts [0] == "M") {
					int parameterStart = parts [2].IndexOf ('(');
					string methodName = parameterStart < 0 ? parts [2] : parts [2].Substring (0, parameterStart);
					if (methodName == "<init>") {
						members.Add ("   <init>(...);");
					} else if (methodName == "<clinit>") {
						members.Add ("   <methods>;");
					} else {
						members.Add ($"   *** {methodName}(...);");
					}
				}
			}

			foreach (var rule in rules) {
				string javaClassName = rule.Key.Replace ('/', '.');
				writer.WriteLine ($"-keep,allowobfuscation class {javaClassName}");
				writer.WriteLine ($"-keepclassmembers,allowobfuscation class {javaClassName} {{");
				foreach (string member in rule.Value) {
					writer.WriteLine (member);
				}
				writer.WriteLine ("}");
				writer.WriteLine ();
			}
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
			foreach (var fieldHandle in type.GetFields ()) {
				ProcessFieldLikeMember (reader, reader.GetFieldDefinition (fieldHandle).GetCustomAttributes (), writer);
			}
			foreach (var propertyHandle in type.GetProperties ()) {
				ProcessFieldLikeMember (reader, reader.GetPropertyDefinition (propertyHandle).GetCustomAttributes (), writer);
			}
			foreach (var eventHandle in type.GetEvents ()) {
				ProcessFieldLikeMember (reader, reader.GetEventDefinition (eventHandle).GetCustomAttributes (), writer);
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
						if (jname == ".ctor" || jname == "<init>") {
							writer.WriteLine ("   <init>(...);");
						} else {
							writer.WriteLine ($"   *** {jname}(...);");
						}

					}
					break;
				}
			}
		}

		void ProcessFieldLikeMember (MetadataReader reader, CustomAttributeHandleCollection attributes, TextWriter writer)
		{
			foreach (var attrHandle in attributes) {
				var attr = reader.GetCustomAttribute (attrHandle);
				if (reader.GetCustomAttributeFullName (attr, Log) != "Android.Runtime.RegisterAttribute") {
					continue;
				}
				var args = attr.GetCustomAttributeArguments ();
				if (args.FixedArguments.Length == 0 || args.FixedArguments [0].Value is not string rewrittenFieldName) {
					break;
				}

				writer.WriteLine ($"   *** {rewrittenFieldName};");
				break;
			}
		}
	}
}
