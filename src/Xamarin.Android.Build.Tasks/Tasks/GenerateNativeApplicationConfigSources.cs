// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

using Java.Interop.Tools.TypeNameMappings;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	using PackageNamingPolicyEnum   = PackageNamingPolicy;

	/// <summary>
	/// Creates the binary CoreCLR application configuration.
	/// </summary>
	public class GenerateNativeApplicationConfigSources : AndroidTask
	{
		public override string TaskPrefix => "GCA";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; } = [];

		public ITaskItem[]? AdditionalResolvedAssemblies { get; set; }

		public ITaskItem[]? NativeLibraries { get; set; }
		public ITaskItem[]? NativeLibrariesNoJniPreload { get; set; }
		public ITaskItem[]? NativeLibrariesAlwaysJniPreload { get; set; }

		public ITaskItem[]? SatelliteAssemblies { get; set; }

		public bool UseAssemblyStore { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; } = [];

		[Required]
		public string AndroidPackageName { get; set; } = "";

		[Required]
		public string AndroidRuntime { get; set; } = "";

		public string? CoreClrBootstrapOutputFile { get; set; }

		public string ProjectRuntimeConfigFilePath { get; set; } = String.Empty;
		public string? ProjectRuntimeConfigDevFilePath { get; set; }

		public string? PackageNamingPolicy { get; set; }
		public ITaskItem[]? Environments { get; set; }
		public string? CustomBundleConfigFile { get; set; }

		public override bool RunTask ()
		{
			if (MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime) != Xamarin.Android.Tasks.AndroidRuntime.CoreCLR) {
				throw new InvalidOperationException (string.Format (CultureInfo.CurrentCulture, Properties.Resources.XAGCA7009_UnsupportedRuntime, AndroidRuntime));
			}
			string? bootstrapPath = CoreClrBootstrapOutputFile;
			if (bootstrapPath.IsNullOrWhiteSpace ()) {
				throw new InvalidOperationException ("CoreCLR bootstrap output path is required.");
			}

			if (!Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicy pnp)) {
				pnp = PackageNamingPolicyEnum.LowercaseCrc64;
			}

			// Include generated environment files from the later stages of the build.
			var envBuilder = new EnvironmentBuilder ();
			envBuilder.Read (Environments);

			int assemblyNameWidth = 0;
			Encoding assemblyNameEncoding = Encoding.UTF8;

			Action<ITaskItem> updateNameWidth = (ITaskItem assembly) => {
				if (UseAssemblyStore) {
					return;
				}

				string assemblyName = Path.GetFileName (assembly.ItemSpec);
				int nameBytes = assemblyNameEncoding.GetBytes (assemblyName).Length;
				if (nameBytes > assemblyNameWidth) {
					assemblyNameWidth = nameBytes;
				}
			};

			int assemblyCount = 0;
			HashSet<string>? archAssemblyNames = null;
			Action<ITaskItem> updateAssemblyCount = (ITaskItem assembly) => {
				string? culture = MonoAndroidHelper.GetAssemblyCulture (assembly);
				string fileName = Path.GetFileName (assembly.ItemSpec);
				string assemblyName;

				if (String.IsNullOrEmpty (culture)) {
					assemblyName = fileName;
				} else {
					assemblyName = $"{culture}/{fileName}";
				}

				archAssemblyNames ??= new HashSet<string> (StringComparer.OrdinalIgnoreCase);

				if (!archAssemblyNames.Contains (assemblyName)) {
					assemblyCount++;
					archAssemblyNames.Add (assemblyName);
				}
			};

			static bool ShouldSkipAssembly (ITaskItem assembly)
			{
				return assembly.GetMetadataOrDefault ("AndroidSkipAddToPackage", false);
			}

			if (SatelliteAssemblies != null) {
				foreach (ITaskItem assembly in SatelliteAssemblies) {
					if (ShouldSkipAssembly (assembly)) {
						continue;
					}

					updateNameWidth (assembly);
					updateAssemblyCount (assembly);
				}
			}

			foreach (var assembly in ResolvedAssemblies) {
				if (ShouldSkipAssembly (assembly)) {
					continue;
				}

				updateNameWidth (assembly);
				updateAssemblyCount (assembly);
			}

			if (AdditionalResolvedAssemblies != null) {
				foreach (ITaskItem assembly in AdditionalResolvedAssemblies) {
					updateNameWidth (assembly);
					updateAssemblyCount (assembly);
				}
			}

			if (!UseAssemblyStore) {
				int abiNameLength = 0;
				foreach (string abi in SupportedAbis) {
					if (abi.Length <= abiNameLength) {
						continue;
					}
					abiNameLength = abi.Length;
				}
				assemblyNameWidth += abiNameLength + 2; // room for '/' and the terminating NUL
			}

			var uniqueNativeLibraries = new List<ITaskItem> ();
			var seenNativeLibraryNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			if (NativeLibraries != null) {
				foreach (ITaskItem item in NativeLibraries) {
					// We don't care about different ABIs here, just the file name
					string name = Path.GetFileName (item.ItemSpec);
					if (seenNativeLibraryNames.Contains (name)) {
						continue;
					}

					seenNativeLibraryNames.Add (name);
					uniqueNativeLibraries.Add (item);
				}
			}

			Dictionary<string, string>? runtimeProperties = RuntimePropertiesParser.ParseConfig (ProjectRuntimeConfigFilePath, ProjectRuntimeConfigDevFilePath);
			var bootstrapBuilder = new CoreClrBootstrapBuilder (envBuilder.EnvironmentVariables, envBuilder.SystemProperties, runtimeProperties, Log) {
				AndroidPackageName = AndroidPackageName,
				PackageNamingPolicy = pnp,
				NumberOfAssembliesInApk = assemblyCount,
				BundledAssemblyNameWidth = assemblyNameWidth,
				NativeLibraries = uniqueNativeLibraries,
				NativeLibrariesNoJniPreload = NativeLibrariesNoJniPreload,
				NativeLibrariesAlwaysJniPreload = NativeLibrariesAlwaysJniPreload,
				IgnoreSplitConfigs = ShouldIgnoreSplitConfigs (),
				HaveAssemblyStore = UseAssemblyStore,
			};

			var stream = MemoryStreamPool.Shared.Rent ();
			try {
				bootstrapBuilder.Write (stream);
				Files.CopyIfStreamChanged (stream, bootstrapPath);
			} finally {
				MemoryStreamPool.Shared.Return (stream);
			}

			return !Log.HasLoggedErrors;
		}

		bool ShouldIgnoreSplitConfigs ()
		{
			if (CustomBundleConfigFile.IsNullOrEmpty ()) {
				return false;
			}

			return BundleConfigSplitConfigsChecker.ShouldIgnoreSplitConfigs (Log, CustomBundleConfigFile);
		}
	}
}
