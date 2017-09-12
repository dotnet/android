// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MonoDroid.Utils;
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

		public string ManifestTemplate { get; set; }
		public string[] MergedManifestDocuments { get; set; }

		public bool Debug { get; set; }
		public string ApplicationName { get; set; }
		public string PackageName { get; set; }
		public string [] ManifestPlaceholders { get; set; }

		public string AndroidSdkDir { get; set; }

		public string AndroidSdkPlatform { get; set; }
		public string OutputDirectory { get; set; }
		public string MergedAndroidManifestOutput { get; set; }

		public bool EmbedAssemblies { get; set; }
		public bool NeedsInternet   { get; set; }

		public bool UseSharedRuntime { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }

		public string BundledWearApplicationName { get; set; }

		public string PackageNamingPolicy { get; set; }
		
		public string ApplicationJavaClass { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GenerateJavaStubs Task");
			Log.LogDebugMessage ("  ManifestTemplate: {0}", ManifestTemplate);
			Log.LogDebugMessage ("  Debug: {0}", Debug);
			Log.LogDebugMessage ("  ApplicationName: {0}", ApplicationName);
			Log.LogDebugMessage ("  PackageName: {0}", PackageName);
			Log.LogDebugMessage ("  AndroidSdkDir: {0}", AndroidSdkDir);
			Log.LogDebugMessage ("  AndroidSdkPlatform: {0}", AndroidSdkPlatform);
			Log.LogDebugMessage ($"  {nameof (ErrorOnCustomJavaObject)}: {ErrorOnCustomJavaObject}");
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
			Log.LogDebugMessage ("  MergedAndroidManifestOutput: {0}", MergedAndroidManifestOutput);
			Log.LogDebugMessage ("  UseSharedRuntime: {0}", UseSharedRuntime);
			Log.LogDebugTaskItems ("  ResolvedAssemblies:", ResolvedAssemblies);
			Log.LogDebugTaskItems ("  ResolvedUserAssemblies:", ResolvedUserAssemblies);
			Log.LogDebugMessage ("  BundledWearApplicationName: {0}", BundledWearApplicationName);
			Log.LogDebugTaskItems ("  MergedManifestDocuments:", MergedManifestDocuments);
			Log.LogDebugMessage ("  PackageNamingPolicy: {0}", PackageNamingPolicy);
			Log.LogDebugMessage ("  ApplicationJavaClass: {0}", ApplicationJavaClass);
			Log.LogDebugTaskItems ("  ManifestPlaceholders: ", ManifestPlaceholders);

			try {
				// We're going to do 3 steps here instead of separate tasks so
				// we can share the list of JLO TypeDefinitions between them
				using (var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true)) {
					Run (res);
				}
			}
			catch (XamarinAndroidException e) {
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
			var temp = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (temp);

			var selectedWhitelistAssemblies = new List<string> ();
			
			// Put every assembly we'll need in the resolver
			foreach (var assembly in ResolvedAssemblies) {
				res.Load (Path.GetFullPath (assembly.ItemSpec));
				if (MonoAndroidHelper.FrameworkAttributeLookupTargets.Any (a => Path.GetFileName (assembly.ItemSpec) == a))
					selectedWhitelistAssemblies.Add (Path.GetFullPath (assembly.ItemSpec));
			}

			// However we only want to look for JLO types in user code
			var assemblies = ResolvedUserAssemblies.Select (p => p.ItemSpec).ToList ();
			var fxAdditions = MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies)
				.Where (a => assemblies.All (x => Path.GetFileName (x) != Path.GetFileName (a)));
			assemblies = assemblies.Concat (fxAdditions).ToList ();

			// Step 1 - Find all the JLO types
			var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
				ErrorOnCustomJavaObject     = ErrorOnCustomJavaObject,
			};
			var all_java_types = scanner.GetJavaTypes (assemblies, res);

			WriteTypeMappings (all_java_types);

			var java_types = all_java_types.Where (t => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t));

			// Step 2 - Generate Java stub code
			var keep_going = Generator.CreateJavaSources (
				Log,
				java_types,
				temp,
				ApplicationJavaClass,
				UseSharedRuntime,
				int.Parse (AndroidSdkPlatform) <= 10,
				ResolvedAssemblies.Any (assembly => Path.GetFileName (assembly.ItemSpec) == "Mono.Android.Export.dll"));

			var temp_map_file = Path.Combine (temp, "acw-map.temp");

			// We need to save a map of .NET type -> ACW type for resource file fixups
			var managed = new Dictionary<string, TypeDefinition> ();
			var java    = new Dictionary<string, TypeDefinition> ();
			var acw_map = new StreamWriter (temp_map_file);

			foreach (var type in java_types) {
				string managedKey = type.FullName.Replace ('/', '.');
				string javaKey    = JavaNativeTypeManager.ToJniName (type).Replace ('/', '.');

				acw_map.WriteLine ("{0};{1}", type.GetPartialAssemblyQualifiedName (), javaKey);
				acw_map.WriteLine ("{0};{1}", type.GetAssemblyQualifiedName (), javaKey);

				TypeDefinition conflict;
				if (managed.TryGetValue (managedKey, out conflict)) {
					Log.LogWarning (
							"Duplicate managed type found! Mappings between managed types and Java types must be unique. " +
							"First Type: '{0}'; Second Type: '{1}'.",
							conflict.GetAssemblyQualifiedName (),
							type.GetAssemblyQualifiedName ());
					Log.LogWarning (
							"References to the type '{0}' will refer to '{1}'.",
							managedKey, conflict.GetAssemblyQualifiedName ());
					continue;
				}
				if (java.TryGetValue (javaKey, out conflict)) {
					Log.LogError (
							"Duplicate Java type found! Mappings between managed types and Java types must be unique. " +
							"First Type: '{0}'; Second Type: '{1}'",
							conflict.GetAssemblyQualifiedName (),
							type.GetAssemblyQualifiedName ());
					keep_going = false;
					continue;
				}
				managed.Add (managedKey, type);
				java.Add (javaKey, type);
				acw_map.WriteLine ("{0};{1}", managedKey, javaKey);
				acw_map.WriteLine ("{0};{1}", JavaNativeTypeManager.ToCompatJniName (type).Replace ('/', '.'), javaKey);
			}

			acw_map.Close ();

			//The previous steps found an error, so we must abort and not generate any further output
			//We must do so subsequent unchanged builds fail too.
			if (!keep_going) {
				File.Delete (temp_map_file);
				return;
			}

			MonoAndroidHelper.CopyIfChanged (temp_map_file, AcwMapFile);

			try { File.Delete (temp_map_file); } catch (Exception) { }

			// Only overwrite files if the contents actually changed
			foreach (var file in Directory.GetFiles (temp, "*", SearchOption.AllDirectories)) {
				var dest = Path.GetFullPath (Path.Combine (OutputDirectory, "src", file.Substring (temp.Length + 1)));

				MonoAndroidHelper.CopyIfChanged (file, dest);
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
			manifest.NeedsInternet = NeedsInternet;

			var additionalProviders = manifest.Merge (all_java_types, selectedWhitelistAssemblies, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments);

			var temp_manifest = Path.Combine (temp, "AndroidManifest.xml");
			var real_manifest = Path.GetFullPath (MergedAndroidManifestOutput);

			manifest.Save (temp_manifest);

			// Only write the new manifest if it actually changed
			MonoAndroidHelper.CopyIfChanged (temp_manifest, real_manifest);

			// Create additional runtime provider java sources.
			string providerTemplateFile = UseSharedRuntime ? "MonoRuntimeProvider.Shared.java" : "MonoRuntimeProvider.Bundled.java";
			string providerTemplate = new StreamReader (typeof (JavaCallableWrapperGenerator).Assembly.GetManifestResourceStream (providerTemplateFile)).ReadToEnd ();
			
			foreach (var provider in additionalProviders) {
				var temp_provider = Path.Combine (temp, provider + ".java");
				File.WriteAllText (temp_provider, providerTemplate.Replace ("MonoRuntimeProvider", provider));
				var real_provider_dir = Path.GetFullPath (Path.Combine (OutputDirectory, "src", "mono"));
				Directory.CreateDirectory (real_provider_dir);
				var real_provider = Path.Combine (real_provider_dir, provider + ".java");
				MonoAndroidHelper.CopyIfChanged (temp_provider, real_provider);
			}

			// Create additional application java sources.
			
			Action<string,string,string,Func<string,string>> save = (resource, filename, destDir, applyTemplate) => {
				string temp_file = Path.Combine (temp, filename);
				string template = applyTemplate (new StreamReader (typeof (GenerateJavaStubs).Assembly.GetManifestResourceStream (resource)).ReadToEnd ());
				File.WriteAllText (temp_file, template);
				Directory.CreateDirectory (destDir);
				var real_file = Path.Combine (destDir, filename);
				MonoAndroidHelper.CopyIfChanged (temp_file, real_file);
			};
			
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

			var real_app_dir = Path.GetFullPath (Path.Combine (OutputDirectory, "src", "mono", "android", "app"));
			string applicationTemplateFile = "ApplicationRegistration.java";
			save (applicationTemplateFile, applicationTemplateFile, real_app_dir,
				template => template.Replace ("// REGISTER_APPLICATION_AND_INSTRUMENTATION_CLASSES_HERE", regCallsWriter.ToString ()));
			
			// Create NotifyTimeZoneChanges java sources.
			string notifyTimeZoneChangesFile = "NotifyTimeZoneChanges.java";
			save (notifyTimeZoneChangesFile, notifyTimeZoneChangesFile, real_app_dir, template => template);
			
			// Delete our temp directory
			try { Directory.Delete (temp, true); } catch (Exception) { }
		}

		void WriteTypeMappings (List<TypeDefinition> types)
		{
			using (var gen = UseSharedRuntime
				? new TypeNameMapGenerator (types, Log.LogDebugMessage)
			        : new TypeNameMapGenerator (ResolvedAssemblies.Select (p => p.ItemSpec), Log.LogDebugMessage)) {
				UpdateWhenChanged (Path.Combine (OutputDirectory, "typemap.jm"), gen.WriteJavaToManaged);
				UpdateWhenChanged (Path.Combine (OutputDirectory, "typemap.mj"), gen.WriteManagedToJava);
			}
		}

		void UpdateWhenChanged (string path, Action<Stream> generator)
		{
			var np  = path + ".new";
			using (var o = File.OpenWrite (np))
				generator (o);
			Files.CopyIfChanged (np, path);
			File.Delete (np);
		}
	}
}
