#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Processes .dll files coming from @(ResolvedFileToPublish). Removes duplicate .NET assemblies by MVID.
	///
	/// Also sets some metadata:
	/// * %(FrameworkAssembly)=True to determine if framework or user assembly
	/// * %(HasMonoAndroidReference)=True for incremental build performance
	/// * Modify %(DestinationSubDirectory) and %(DestinationSubPath) if an assembly has an architecture-specific version
	/// </summary>
	public class ProcessAssemblies : AndroidTask
	{
		public override string TaskPrefix => "PRAS";

		[Required]
		public string [] RuntimeIdentifiers { get; set; } = Array.Empty<string>();

		public bool DesignTimeBuild { get; set; }

		public bool AndroidIncludeDebugSymbols { get; set; }

		public bool PublishTrimmed { get; set; }

		public ITaskItem [] InputAssemblies { get; set; } = Array.Empty<ITaskItem> ();

		public ITaskItem [] InputJavaLibraries { get; set; } = Array.Empty<ITaskItem> ();

		[Output]
		public ITaskItem []? OutputAssemblies { get; set; }

		[Output]
		public ITaskItem []? OutputJavaLibraries { get; set; }

		[Output]
		public ITaskItem []? ShrunkAssemblies { get; set; }

		[Output]
		public ITaskItem []? ResolvedSymbols { get; set; }

		public override bool RunTask ()
		{
			var output = new List<ITaskItem> ();
			var symbols = new Dictionary<string, ITaskItem> ();

			if (ResolvedSymbols != null) {
				foreach (var symbol in ResolvedSymbols.Where (Filter)) {
					symbols [symbol.ItemSpec] = symbol;
				}
			}

			SetMetadataForAssemblies (output, symbols);
			OutputAssemblies = output.ToArray ();
			ResolvedSymbols = symbols.Values.ToArray ();

			// Set ShrunkAssemblies for _RemoveRegisterAttribute and <BuildApk/>
			// This should match the Condition on the _RemoveRegisterAttribute target
			if (PublishTrimmed) {
				if (!AndroidIncludeDebugSymbols) {
					var shrunkAssemblies = new List<ITaskItem> (OutputAssemblies.Length);
					foreach (var assembly in OutputAssemblies) {
						var dir = Path.GetDirectoryName (assembly.ItemSpec);
						var file = Path.GetFileName (assembly.ItemSpec);
						shrunkAssemblies.Add (new TaskItem (assembly) {
							ItemSpec = Path.Combine (dir, "shrunk", file),
						});
					}
					ShrunkAssemblies = shrunkAssemblies.ToArray ();
				} else {
					ShrunkAssemblies = OutputAssemblies;
				}
			}

			if (InputJavaLibraries != null) {
				var javaLibraries = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
				foreach (var item in InputJavaLibraries) {
					if (!MonoAndroidHelper.IsFromAKnownRuntimePack (item))
						continue;
					var name = Path.GetFileNameWithoutExtension(item.ItemSpec);
					if (!javaLibraries.ContainsKey (name)) {
						javaLibraries [name] = item;
					}
				}
				OutputJavaLibraries = javaLibraries.Values.ToArray ();
			}

			return !Log.HasLoggedErrors;
		}

		void SetAssemblyAbiMetadata (string abi, ITaskItem assembly, ITaskItem? symbol)
		{
			if (String.IsNullOrEmpty (abi)) {
				throw new ArgumentException ("must not be null or empty", nameof (abi));
			}

			assembly.SetMetadata ("Abi", abi);
			symbol?.SetMetadata ("Abi", abi);
		}

		void SetAssemblyAbiMetadata (ITaskItem assembly, ITaskItem? symbol)
		{
			string rid = assembly.GetMetadata ("RuntimeIdentifier");

			SetAssemblyAbiMetadata (AndroidRidAbiHelper.RuntimeIdentifierToAbi (rid), assembly, symbol);
		}

		void SetMetadataForAssemblies (List<ITaskItem> output, Dictionary<string, ITaskItem> symbols)
		{
			foreach (ITaskItem assembly in InputAssemblies) {
				if (DesignTimeBuild && !File.Exists (assembly.ItemSpec)) {
					// Designer builds don't produce assemblies, so library and main application DLLs might not
					// be there and would later cause an error when the `_CopyAssembliesForDesigner` task runs
					continue;
				}

				ITaskItem? symbol = GetOrCreateSymbolItem (symbols, assembly);
				SetAssemblyAbiMetadata (assembly, symbol);
				SetDestinationSubDirectory (assembly, symbol);
				assembly.SetMetadata ("FrameworkAssembly", MonoAndroidHelper.IsFrameworkAssembly (assembly).ToString ());

				if (!DesignTimeBuild) {
					// Designer builds don't produce assemblies, the HasMonoAndroidReference call would throw an exception in that case
					assembly.SetMetadata ("HasMonoAndroidReference", MonoAndroidHelper.HasMonoAndroidReference (assembly).ToString ());
				}
				output.Add (assembly);
			}
		}

		static ITaskItem? GetOrCreateSymbolItem (Dictionary<string, ITaskItem> symbols, ITaskItem assembly)
		{
			var symbolPath = Path.ChangeExtension (assembly.ItemSpec, ".pdb");
			if (!symbols.TryGetValue (symbolPath, out var symbol) || !string.IsNullOrEmpty (symbol.GetMetadata ("DestinationSubDirectory"))) {
				// Sometimes .pdb files are not included in @(ResolvedFileToPublish), so add them if they exist
				if (File.Exists (symbolPath)) {
					symbols [symbolPath] = symbol = new TaskItem (symbolPath);
					return symbol;
				}
			}
			return symbol;
		}

		bool Filter (ITaskItem item)
		{
			if (!File.Exists (item.ItemSpec)) {
				Log.LogDebugMessage ($"Skipping non-existent file '{item.ItemSpec}'.");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Sets %(DestinationSubDirectory) and %(DestinationSubPath) based on %(RuntimeIdentifier)
		/// </summary>
		void SetDestinationSubDirectory (ITaskItem assembly, ITaskItem? symbol)
		{
			string? rid = assembly.GetMetadata ("RuntimeIdentifier");
			if (String.IsNullOrEmpty (rid)) {
				throw new InvalidOperationException ($"Assembly '{assembly}' item is missing required ");
			}

			string? abi = AndroidRidAbiHelper.RuntimeIdentifierToAbi (rid);
			if (string.IsNullOrEmpty (abi)) {
				throw new InvalidOperationException ($"Unable to convert a runtime identifier '{rid}' to Android ABI for: {assembly.ItemSpec}");
			}

			SetIt (assembly);
			SetIt (symbol);

			void SetIt (ITaskItem? item)
			{
				if (item == null) {
					return;
				}

				string destination = Path.Combine (abi, item.GetMetadata ("DestinationSubDirectory"));
				if (destination.Length > 0 && destination [destination.Length - 1] != Path.DirectorySeparatorChar) {
					destination += Path.DirectorySeparatorChar;
				}
				item.SetMetadata ("DestinationSubDirectory", destination);
				item.SetMetadata ("DestinationSubPath", Path.Combine (destination, Path.GetFileName (item.ItemSpec)));
			}
		}
	}
}
