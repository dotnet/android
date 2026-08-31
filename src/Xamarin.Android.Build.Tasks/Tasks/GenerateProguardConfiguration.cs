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
		readonly HashSet<string> reachableR8Entries = new HashSet<string> (StringComparer.Ordinal);

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

			foreach (var assembly in LinkedAssemblies) {
				ProcessAssembly (assembly.ItemSpec, writer);
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
			File.WriteAllText (path, R8Mapping.CreateManifestContent (reachableR8Entries));
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

			string rewrittenJniName = javaTypeName.Replace ('.', '/');
			bool hasR8ClassMapping = false;
			string originalJniName = rewrittenJniName;
			if (r8Mapping?.TryGetOriginalClass (rewrittenJniName, out string original) == true) {
				hasR8ClassMapping = true;
				originalJniName = original;
			}
			string ruleTypeName = originalJniName.Replace ('/', '.');
			string allowObfuscation = r8Mapping == null ? "" : ",allowobfuscation";
			if (hasR8ClassMapping) {
				reachableR8Entries.Add (R8Mapping.BuildClassEntry (originalJniName));
			}
			writer.WriteLine ($"-keep{allowObfuscation} class {ruleTypeName}");
			writer.WriteLine ($"-keepclassmembers{allowObfuscation} class {ruleTypeName} {{");

			foreach (var methodHandle in type.GetMethods ()) {
				ProcessMethod (reader, methodHandle, originalJniName, writer);
			}
			foreach (var fieldHandle in type.GetFields ()) {
				ProcessFieldLikeMember (reader, reader.GetFieldDefinition (fieldHandle).GetCustomAttributes (), originalJniName, writer);
			}
			foreach (var propertyHandle in type.GetProperties ()) {
				ProcessFieldLikeMember (reader, reader.GetPropertyDefinition (propertyHandle).GetCustomAttributes (), originalJniName, writer);
			}
			foreach (var eventHandle in type.GetEvents ()) {
				ProcessFieldLikeMember (reader, reader.GetEventDefinition (eventHandle).GetCustomAttributes (), originalJniName, writer);
			}

			writer.WriteLine ("}");
			writer.WriteLine ();
		}

		void ProcessMethod (MetadataReader reader, MethodDefinitionHandle methodHandle, string originalJniClassName, TextWriter writer)
		{
			var method = reader.GetMethodDefinition (methodHandle);

			foreach (var attrHandle in method.GetCustomAttributes ()) {
				var attr = reader.GetCustomAttribute (attrHandle);
				var attrName = reader.GetCustomAttributeFullName (attr, Log);
				if (attrName == "Android.Runtime.RegisterAttribute") {
					var args = attr.GetCustomAttributeArguments ();
					if (args.FixedArguments.Length >= 2 &&
					    args.FixedArguments[0].Value is string jname &&
					    args.FixedArguments[1].Value is string jniDescriptor) {
						if (jname == ".ctor" || jname == "<init>") {
							writer.WriteLine ("   <init>(...);");
							if (TryGetOriginalParameterTypes (jniDescriptor, out var originalParameterTypes) &&
									r8Mapping?.TryGetRenamedMethod (originalJniClassName, "<init>", originalParameterTypes, out _) == true) {
								reachableR8Entries.Add (R8Mapping.BuildMethodEntry (
									originalJniClassName,
									R8Mapping.BuildMethodKey ("<init>", originalParameterTypes)));
							}
						} else {
							bool wroteOriginalName = false;
							if (r8Mapping != null &&
									TryGetOriginalMethodName (originalJniClassName, jname, jniDescriptor, out string originalName, out var originalParameterTypes)) {
								writer.WriteLine ($"   *** {originalName}(...);");
								reachableR8Entries.Add (R8Mapping.BuildMethodEntry (
									originalJniClassName,
									R8Mapping.BuildMethodKey (originalName, originalParameterTypes)));
								wroteOriginalName = true;
							}
							if (!wroteOriginalName) {
								writer.WriteLine ($"   *** {jname}(...);");
							}
						}

					}
					break;
				}
			}
		}

		void ProcessFieldLikeMember (MetadataReader reader, CustomAttributeHandleCollection attributes, string originalJniClassName, TextWriter writer)
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

				string fieldName = rewrittenFieldName;
				if (r8Mapping?.TryGetOriginalFieldName (originalJniClassName, rewrittenFieldName, out string originalFieldName) == true) {
					fieldName = originalFieldName;
					reachableR8Entries.Add (R8Mapping.BuildFieldEntry (originalJniClassName, originalFieldName));
				}
				writer.WriteLine ($"   *** {fieldName};");
				break;
			}
		}

		string? GetOriginalClassName (string rewrittenJniName)
			=> r8Mapping?.TryGetOriginalClass (rewrittenJniName, out string originalJniName) == true ? originalJniName : null;

		bool TryGetOriginalMethodName (
			string originalJniClassName,
			string rewrittenMethodName,
			string rewrittenDescriptor,
			out string originalMethodName,
			out List<string> originalParameterTypes)
		{
			if (r8Mapping != null && TryGetOriginalParameterTypes (rewrittenDescriptor, out originalParameterTypes)) {
				return r8Mapping.TryGetOriginalMethodName (
					originalJniClassName,
					rewrittenMethodName,
					originalParameterTypes,
					out originalMethodName);
			}
			originalMethodName = "";
			originalParameterTypes = [];
			return false;
		}

		bool TryGetOriginalParameterTypes (string rewrittenDescriptor, out List<string> originalParameterTypes)
		{
			JniDescriptorText.TryRewriteDescriptor (rewrittenDescriptor, GetOriginalClassName, out string originalDescriptor);
			if (JniDescriptorText.TryParseMethodDescriptor (originalDescriptor, out var originalParameterTokens, out _)) {
				originalParameterTypes = originalParameterTokens.ConvertAll (JniDescriptorText.JniTypeTokenToJavaSource);
				return true;
			}
			originalParameterTypes = [];
			return false;
		}
	}
}
