// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;


using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	using PackageNamingPolicyEnum   = PackageNamingPolicy;

	public class GenerateJavaStubs : Task
	{
		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		[Required]
		public ITaskItem [] FrameworkDirectories { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		public string ManifestTemplate { get; set; }
		public string[] MergedManifestDocuments { get; set; }

		public bool Debug { get; set; }
		public bool MultiDex { get; set; }
		public string ApplicationName { get; set; }
		public string PackageName { get; set; }
		public string [] ManifestPlaceholders { get; set; }

		public string AndroidSdkDir { get; set; }

		public string AndroidSdkPlatform { get; set; }
		public string OutputDirectory { get; set; }
		public string MergedAndroidManifestOutput { get; set; }

		public bool EmbedAssemblies { get; set; }
		public bool NeedsInternet   { get; set; }
		public bool InstantRunEnabled { get; set; }

		public bool UseSharedRuntime { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }

		public string BundledWearApplicationName { get; set; }

		public string PackageNamingPolicy { get; set; }
		
		public string ApplicationJavaClass { get; set; }

		internal const string AndroidSkipJavaStubGeneration = "AndroidSkipJavaStubGeneration";

		public override bool Execute ()
		{
			try {
				// We're going to do 3 steps here instead of separate tasks so
				// we can share the list of JLO TypeDefinitions between them
				using (var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true)) {
					Run (res);
				}
			} catch (XamarinAndroidException e) {
				Log.LogCodedError (string.Format ("XA{0:0000}", e.Code), e.MessageWithoutCode);
				if (MonoAndroidHelper.LogInternalExceptions)
					Log.LogMessage (e.ToString ());
			}

			if (Log.HasLoggedErrors) {
				// Ensure that on a rebuild, we don't *skip* the `_GenerateJavaStubs` target,
				// by ensuring that the target outputs have been deleted.
				Files.DeleteFile (MergedAndroidManifestOutput, Log);
				Files.DeleteFile (AcwMapFile, Log);
				Files.DeleteFile (Path.Combine (OutputDirectory, "typemap.jm"), Log);
				Files.DeleteFile (Path.Combine (OutputDirectory, "typemap.mj"), Log);
			}

			return !Log.HasLoggedErrors;
		}

		void Run (DirectoryAssemblyResolver res)
		{
			PackageNamingPolicy pnp;
			JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out pnp) ? pnp : PackageNamingPolicyEnum.LowercaseHash;

			foreach (var dir in FrameworkDirectories) {
				if (Directory.Exists (dir.ItemSpec))
					res.SearchDirectories.Add (dir.ItemSpec);
			}

			// Put every assembly we'll need in the resolver
			foreach (var assembly in ResolvedAssemblies) {
				if (bool.TryParse (assembly.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {assembly.ItemSpec}");
					continue;
				}
				res.Load (assembly.ItemSpec);
			}

			// However we only want to look for JLO types in user code
			List<string> assemblies = new List<string> ();
			foreach (var asm in ResolvedUserAssemblies) {
				if (bool.TryParse (asm.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
					continue;
				}
				if (!assemblies.All (x => Path.GetFileName (x) != Path.GetFileName (asm.ItemSpec)))
					continue;
				Log.LogDebugMessage ($"Adding {asm.ItemSpec} to assemblies.");
				assemblies.Add (asm.ItemSpec);
			}
			foreach (var asm in MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies)) {
				if (bool.TryParse (asm.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
					continue;
				}
				if (!assemblies.All (x => Path.GetFileName (x) != Path.GetFileName (asm.ItemSpec)))
					continue;
				Log.LogDebugMessage ($"Adding {asm.ItemSpec} to assemblies.");
				assemblies.Add (asm.ItemSpec);
			}

			// Step 1 - Find all the JLO types
			var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
				ErrorOnCustomJavaObject     = ErrorOnCustomJavaObject,
			};
			var all_java_types = scanner.GetJavaTypes (assemblies, res);

			WriteTypeMappings (all_java_types);

			var java_types = all_java_types
				.Where (t => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t))
				.ToArray ();

			// Step 2 - Generate Java stub code
			var success = Generator.CreateJavaSources (
				Log,
				java_types,
				Path.Combine (OutputDirectory, "src"),
				ApplicationJavaClass,
				AndroidSdkPlatform,
				UseSharedRuntime,
				int.Parse (AndroidSdkPlatform) <= 10,
				ResolvedAssemblies.Any (assembly => Path.GetFileName (assembly.ItemSpec) == "Mono.Android.Export.dll"));
			if (!success)
				return;

			// We need to save a map of .NET type -> ACW type for resource file fixups
			var managed = new Dictionary<string, TypeDefinition> (java_types.Length, StringComparer.Ordinal);
			var java    = new Dictionary<string, TypeDefinition> (java_types.Length, StringComparer.Ordinal);

			var managedConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);
			var javaConflicts    = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);

			// Allocate a MemoryStream with a reasonable guess at its capacity
			using (var stream = new MemoryStream (java_types.Length * 32))
			using (var acw_map = new StreamWriter (stream)) {
				foreach (var type in java_types) {
					string managedKey = type.FullName.Replace ('/', '.');
					string javaKey = JavaNativeTypeManager.ToJniName (type).Replace ('/', '.');

					acw_map.Write (type.GetPartialAssemblyQualifiedName ());
					acw_map.Write (';');
					acw_map.Write (javaKey);
					acw_map.WriteLine ();

					TypeDefinition conflict;
					bool hasConflict = false;
					if (managed.TryGetValue (managedKey, out conflict)) {
						if (!managedConflicts.TryGetValue (managedKey, out var list))
							managedConflicts.Add (managedKey, list = new List<string> { conflict.GetPartialAssemblyName () });
						list.Add (type.GetPartialAssemblyName ());
						hasConflict = true;
					}
					if (java.TryGetValue (javaKey, out conflict)) {
						if (!javaConflicts.TryGetValue (javaKey, out var list))
							javaConflicts.Add (javaKey, list = new List<string> { conflict.GetAssemblyQualifiedName () });
						list.Add (type.GetAssemblyQualifiedName ());
						success = false;
						hasConflict = true;
					}
					if (!hasConflict) {
						managed.Add (managedKey, type);
						java.Add (javaKey, type);

						acw_map.Write (managedKey);
						acw_map.Write (';');
						acw_map.Write (javaKey);
						acw_map.WriteLine ();

						acw_map.Write (JavaNativeTypeManager.ToCompatJniName (type).Replace ('/', '.'));
						acw_map.Write (';');
						acw_map.Write (javaKey);
						acw_map.WriteLine ();
					}
				}

				acw_map.Flush ();
				MonoAndroidHelper.CopyIfStreamChanged (stream, AcwMapFile);
			}

			foreach (var kvp in managedConflicts) {
				Log.LogCodedWarning (
					"XA4214",
					"The managed type `{0}` exists in multiple assemblies: {1}. " +
					"Please refactor the managed type names in these assemblies so that they are not identical.",
					kvp.Key,
					string.Join (", ", kvp.Value));
				Log.LogCodedWarning ("XA4214", "References to the type `{0}` will refer to `{0}, {1}`.", kvp.Key, kvp.Value [0]);
			}

			foreach (var kvp in javaConflicts) {
				Log.LogCodedError (
					"XA4215",
					"The Java type `{0}` is generated by more than one managed type. " +
					"Please change the [Register] attribute so that the same Java type is not emitted.",
					kvp.Key);
				foreach (var typeName in kvp.Value)
					Log.LogCodedError ("XA4215", "  `{0}` generated by: {1}", kvp.Key, typeName);
			}

			// Step 3 - Merge [Activity] and friends into AndroidManifest.xml
			var manifest = new ManifestDocument (ManifestTemplate, this.Log);

			manifest.PackageName = PackageName;
			manifest.ApplicationName = ApplicationName ?? PackageName;
			manifest.Placeholders = ManifestPlaceholders;
			manifest.Assemblies.AddRange (assemblies);
			manifest.Resolver = res;
			manifest.SdkDir = AndroidSdkDir;
			manifest.SdkVersion = AndroidSdkPlatform;
			manifest.Debug = Debug;
			manifest.MultiDex = MultiDex;
			manifest.NeedsInternet = NeedsInternet;
			manifest.InstantRunEnabled = InstantRunEnabled;

			var additionalProviders = manifest.Merge (all_java_types, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments);

			using (var stream = new MemoryStream ()) {
				manifest.Save (stream);

				// Only write the new manifest if it actually changed
				MonoAndroidHelper.CopyIfStreamChanged (stream, MergedAndroidManifestOutput);
			}

			// Create additional runtime provider java sources.
			string providerTemplateFile = UseSharedRuntime ? "MonoRuntimeProvider.Shared.java" : "MonoRuntimeProvider.Bundled.java";
			string providerTemplate = GetResource (providerTemplateFile);
			
			foreach (var provider in additionalProviders) {
				var contents = providerTemplate.Replace ("MonoRuntimeProvider", provider);
				var real_provider = Path.Combine (OutputDirectory, "src", "mono", provider + ".java");
				MonoAndroidHelper.CopyIfStringChanged (contents, real_provider);
			}

			// Create additional application java sources.
			StringWriter regCallsWriter = new StringWriter ();
			regCallsWriter.WriteLine ("\t\t// Application and Instrumentation ACWs must be registered first.");
			foreach (var type in java_types) {
				if (JavaNativeTypeManager.IsApplication (type) || JavaNativeTypeManager.IsInstrumentation (type)) {
					string javaKey = JavaNativeTypeManager.ToJniName (type).Replace ('/', '.');				
					regCallsWriter.WriteLine ("\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {1}.__md_methods);",
						type.GetAssemblyQualifiedName (), javaKey);
				}
			}
			regCallsWriter.Close ();

			var real_app_dir = Path.Combine (OutputDirectory, "src", "mono", "android", "app");
			string applicationTemplateFile = "ApplicationRegistration.java";
			SaveResource (applicationTemplateFile, applicationTemplateFile, real_app_dir,
				template => template.Replace ("// REGISTER_APPLICATION_AND_INSTRUMENTATION_CLASSES_HERE", regCallsWriter.ToString ()));
		}

		string GetResource (string resource)
		{
			using (var stream = GetType ().Assembly.GetManifestResourceStream (resource))
			using (var reader = new StreamReader (stream))
				return reader.ReadToEnd ();
		}

		void SaveResource (string resource, string filename, string destDir, Func<string, string> applyTemplate)
		{
			string template = GetResource (resource);
			template = applyTemplate (template);
			MonoAndroidHelper.CopyIfStringChanged (template, Path.Combine (destDir, filename));
		}

		void WriteTypeMappings (List<TypeDefinition> types)
		{
			void logger (TraceLevel level, string value) => Log.LogDebugMessage (value);
			TypeNameMapGenerator createTypeMapGenerator () => UseSharedRuntime ?
				new TypeNameMapGenerator (types, logger) :
				new TypeNameMapGenerator (ResolvedAssemblies.Select (p => p.ItemSpec), logger);
			using (var gen = createTypeMapGenerator ()) {
				using (var ms = new MemoryStream ()) {
					UpdateWhenChanged (Path.Combine (OutputDirectory, "typemap.jm"), "jm", ms, gen.WriteJavaToManaged);
					UpdateWhenChanged (Path.Combine (OutputDirectory, "typemap.mj"), "mj", ms, gen.WriteManagedToJava);
				}
			}
		}

		void UpdateWhenChanged (string path, string type, MemoryStream ms, Action<Stream> generator)
		{
			if (InstantRunEnabled) {
				ms.SetLength (0);
				generator (ms);
				MonoAndroidHelper.CopyIfStreamChanged (ms, path);
			}

			string dataFilePath = $"{path}.inc";
			using (var stream = new NativeAssemblyDataStream ()) {
				generator (stream);
				stream.EndOfFile ();
				MonoAndroidHelper.CopyIfStreamChanged (stream, dataFilePath);

				var generatedFiles = new List <ITaskItem> ();
				string mappingFieldName = $"{type}_typemap";
				string dataFileName = Path.GetFileName (dataFilePath);
				NativeAssemblerTargetProvider asmTargetProvider;
				var utf8Encoding = new UTF8Encoding (false);
				foreach (string abi in SupportedAbis) {
					ms.SetLength (0);
					switch (abi.Trim ()) {
						case "armeabi-v7a":
							asmTargetProvider = new ARMNativeAssemblerTargetProvider (is64Bit: false);
							break;

						case "arm64-v8a":
							asmTargetProvider = new ARMNativeAssemblerTargetProvider (is64Bit: true);
							break;

						case "x86":
							asmTargetProvider = new X86NativeAssemblerTargetProvider (is64Bit: false);
							break;

						case "x86_64":
							asmTargetProvider = new X86NativeAssemblerTargetProvider (is64Bit: true);
							break;

						default:
							throw new InvalidOperationException ($"Unknown ABI {abi}");
					}

					var asmgen = new TypeMappingNativeAssemblyGenerator (asmTargetProvider, stream, dataFileName, stream.MapByteCount, mappingFieldName);
					string asmFileName = $"{path}.{abi.Trim ()}.s";
					using (var sw = new StreamWriter (ms, utf8Encoding, bufferSize: 8192, leaveOpen: true)) {
						asmgen.Write (sw, dataFileName);
						MonoAndroidHelper.CopyIfStreamChanged (ms, asmFileName);
					}
				}
			}
		}
	}
}
