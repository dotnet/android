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

	public class GenerateJavaStubs : AndroidTask
	{
		public override string TaskPrefix => "GJS";

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

		[Required]
		public string TypemapOutputDirectory { get; set; }

		[Required]
		public bool GenerateNativeAssembly { get; set; }

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

		public string BundledWearApplicationName { get; set; }

		public string PackageNamingPolicy { get; set; }
		
		public string ApplicationJavaClass { get; set; }

		[Output]
		public string [] GeneratedBinaryTypeMaps { get; set; }

		internal const string AndroidSkipJavaStubGeneration = "AndroidSkipJavaStubGeneration";

		public override bool RunTask ()
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
			}

			return !Log.HasLoggedErrors;
		}

		void Run (DirectoryAssemblyResolver res)
		{
			PackageNamingPolicy pnp;
			JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out pnp) ? pnp : PackageNamingPolicyEnum.LowercaseCrc64;

			foreach (var dir in FrameworkDirectories) {
				if (Directory.Exists (dir.ItemSpec))
					res.SearchDirectories.Add (dir.ItemSpec);
			}

			// Put every assembly we'll need in the resolver
			bool hasExportReference = false;
			bool haveMonoAndroid = false;
			var allTypemapAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			var userAssemblies = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
			foreach (var assembly in ResolvedAssemblies) {
				bool value;
				if (bool.TryParse (assembly.GetMetadata (AndroidSkipJavaStubGeneration), out value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {assembly.ItemSpec}");
					continue;
				}

				bool addAssembly = false;
				string fileName = Path.GetFileName (assembly.ItemSpec);
				if (!hasExportReference && String.Compare ("Mono.Android.Export.dll", fileName, StringComparison.OrdinalIgnoreCase) == 0) {
					hasExportReference = true;
					addAssembly = true;
				} else if (!haveMonoAndroid && String.Compare ("Mono.Android.dll", fileName, StringComparison.OrdinalIgnoreCase) == 0) {
					haveMonoAndroid = true;
					addAssembly = true;
				} else if (MonoAndroidHelper.FrameworkAssembliesToTreatAsUserAssemblies.Contains (fileName)) {
					if (!bool.TryParse (assembly.GetMetadata (AndroidSkipJavaStubGeneration), out value) || !value) {
						string name = Path.GetFileNameWithoutExtension (fileName);
						if (!userAssemblies.ContainsKey (name))
							userAssemblies.Add (name, assembly.ItemSpec);
						addAssembly = true;
					}
				}

				if (addAssembly) {
					allTypemapAssemblies.Add (assembly.ItemSpec);
				}

				res.Load (assembly.ItemSpec);
			}

			// However we only want to look for JLO types in user code for Java stub code generation
			foreach (var asm in ResolvedUserAssemblies) {
				if (bool.TryParse (asm.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
					continue;
				}
				allTypemapAssemblies.Add (asm.ItemSpec);
				userAssemblies.Add (Path.GetFileNameWithoutExtension (asm.ItemSpec), asm.ItemSpec);
			}

			// Step 1 - Find all the JLO types
			var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
				ErrorOnCustomJavaObject     = ErrorOnCustomJavaObject,
			};

			List<TypeDefinition> allJavaTypes = scanner.GetJavaTypes (allTypemapAssemblies, res);

			// Step 2 - Generate type maps
			//   Type mappings need to use all the assemblies, always.
			WriteTypeMappings (res, allJavaTypes);

			var javaTypes = new List<TypeDefinition> ();
			foreach (TypeDefinition td in allJavaTypes) {
				if (!userAssemblies.ContainsKey (td.Module.Assembly.Name.Name) || JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (td)) {
					continue;
				}

				javaTypes.Add (td);
			}

			// Step 3 - Generate Java stub code
			var success = Generator.CreateJavaSources (
				Log,
				javaTypes,
				Path.Combine (OutputDirectory, "src"),
				ApplicationJavaClass,
				AndroidSdkPlatform,
				UseSharedRuntime,
				int.Parse (AndroidSdkPlatform) <= 10,
				hasExportReference);
			if (!success)
				return;

			// We need to save a map of .NET type -> ACW type for resource file fixups
			var managed = new Dictionary<string, TypeDefinition> (javaTypes.Count, StringComparer.Ordinal);
			var java    = new Dictionary<string, TypeDefinition> (javaTypes.Count, StringComparer.Ordinal);

			var managedConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);
			var javaConflicts    = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);

			// Allocate a MemoryStream with a reasonable guess at its capacity
			using (var stream = new MemoryStream (javaTypes.Count * 32))
			using (var acw_map = new StreamWriter (stream)) {
				foreach (TypeDefinition type in javaTypes) {
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
				Log.LogCodedWarning ("XA4214", Properties.Resources.XA4214, kvp.Key, string.Join (", ", kvp.Value));
				Log.LogCodedWarning ("XA4214", Properties.Resources.XA4214_Result, kvp.Key, kvp.Value [0]);
			}

			foreach (var kvp in javaConflicts) {
				Log.LogCodedError ("XA4215", Properties.Resources.XA4215, kvp.Key);
				foreach (var typeName in kvp.Value)
					Log.LogCodedError ("XA4215", Properties.Resources.XA4215_Details, kvp.Key, typeName);
			}

			// Step 3 - Merge [Activity] and friends into AndroidManifest.xml
			var manifest = new ManifestDocument (ManifestTemplate);

			manifest.PackageName = PackageName;
			manifest.ApplicationName = ApplicationName ?? PackageName;
			manifest.Placeholders = ManifestPlaceholders;
			manifest.Assemblies.AddRange (userAssemblies.Values);
			manifest.Resolver = res;
			manifest.SdkDir = AndroidSdkDir;
			manifest.SdkVersion = AndroidSdkPlatform;
			manifest.Debug = Debug;
			manifest.MultiDex = MultiDex;
			manifest.NeedsInternet = NeedsInternet;
			manifest.InstantRunEnabled = InstantRunEnabled;

			var additionalProviders = manifest.Merge (Log, allJavaTypes, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments);

			using (var stream = new MemoryStream ()) {
				manifest.Save (Log, stream);

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
			foreach (var type in javaTypes) {
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

		void WriteTypeMappings (DirectoryAssemblyResolver resolver, List<TypeDefinition> types)
		{
			var tmg = new TypeMapGenerator ((string message) => Log.LogDebugMessage (message), SupportedAbis);
			if (!tmg.Generate (types, TypemapOutputDirectory, GenerateNativeAssembly))
				throw new XamarinAndroidException (4308, Properties.Resources.XA4308);
			GeneratedBinaryTypeMaps = tmg.GeneratedBinaryTypeMaps.ToArray ();
		}
	}
}
