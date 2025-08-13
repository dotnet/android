#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;
using static Xamarin.Android.Tasks.TypeMapGenerator;

namespace Xamarin.Android.Tasks
{
	class TypeMapGenerator
	{
		public bool RunCheckedBuild { get; set; }

		internal sealed class ModuleUUIDArrayComparer : IComparer<ModuleReleaseData>
		{
			int Compare (byte[] left, byte[] right)
			{
				int ret;

				for (int i = 0; i < 16; i++) {
					ret = left[i].CompareTo (right[i]);
					if (ret != 0)
						return ret;
				}

				return 0;
			}

			public int Compare (ModuleReleaseData left, ModuleReleaseData right)
			{
				return Compare (left.MvidBytes, right.MvidBytes);
			}
		}

		internal sealed class TypeMapReleaseEntry
		{
			public string JavaName = "";
			public string ManagedTypeName = "";
			public uint Token;
			public int ModuleIndex = -1;
			public bool SkipInJavaToManaged;
		}

		internal sealed class ModuleReleaseData
		{
			public Guid Mvid;
			public byte[] MvidBytes = [];
			public TypeMapReleaseEntry[] Types = [];
			public List<TypeMapReleaseEntry> DuplicateTypes = [];
			public string AssemblyName = "";

			public Dictionary<string, TypeMapReleaseEntry> TypesScratch = [];
		}

		internal sealed class TypeMapDebugEntry
		{
			public string JavaName = "";
			public string ManagedName = "";
			public uint ManagedTypeTokenId;
			public bool SkipInJavaToManaged;
			public TypeMapDebugEntry? DuplicateForJavaToManaged;
			public string AssemblyName = "";

			// This field is only used by the Cecil adapter for temp storage while reading.
			// It is not used to create the typemap.
			public TypeDefinition? TypeDefinition;

			// These fields are only used by the XML adapter for temp storage while reading.
			// It is not used to create the typemap.
			public string? DuplicateForJavaToManagedKey { get; set; }
			public bool IsInvoker { get; set; }
			public bool IsMonoAndroid { get; set; } // Types in Mono.Android take precedence over other assemblies

			public string Key => $"{JavaName}|{ManagedName}";

			public override string ToString ()
			{
				return $"TypeMapDebugEntry{{JavaName={JavaName}, ManagedName={ManagedName}, SkipInJavaToManaged={SkipInJavaToManaged}, DuplicateForJavaToManaged={DuplicateForJavaToManaged}}}";
			}
		}

		internal sealed class TypeMapDebugAssembly
		{
			public Guid MVID;
			public byte[] MVIDBytes = [];
			public string Name = "";
		}

		internal sealed class TypeMapDebugDataSets
		{
			public List<TypeMapDebugEntry> JavaToManaged = [];
			public List<TypeMapDebugEntry> ManagedToJava = [];
			public List<TypeMapDebugAssembly>? UniqueAssemblies;
		}

		// Widths include the terminating nul character but not the padding!
		internal sealed class ModuleDebugData
		{
			public uint EntryCount;
			public List<TypeMapDebugEntry> JavaToManagedMap = [];
			public List<TypeMapDebugEntry> ManagedToJavaMap = [];
			public List<TypeMapDebugAssembly>? UniqueAssemblies;
		}

		internal sealed class ReleaseGenerationState
		{
			// This field is only used by the Cecil adapter for temp storage while reading.
			// It is not used to create the typemap.
			public readonly Dictionary<Guid, byte []> MvidCache;

			public readonly Dictionary<byte [], ModuleReleaseData> TempModules;

			public ReleaseGenerationState ()
			{
				MvidCache = new Dictionary<Guid, byte []> ();
				TempModules = new Dictionary<byte [], ModuleReleaseData> ();
			}
		}

		readonly TaskLoggingHelper log;
		readonly ITypeMapGeneratorAdapter state;
		readonly AndroidRuntime runtime;

		public IList<string> GeneratedBinaryTypeMaps { get; } = new List<string> ();

		public TypeMapGenerator (TaskLoggingHelper log, ITypeMapGeneratorAdapter state, AndroidRuntime runtime)
		{
			if (log == null)
				throw new ArgumentNullException (nameof (log));
			if (state == null)
				throw new ArgumentNullException (nameof (state));
			this.log = log;
			this.state = state;
			this.runtime = runtime;
		}

		public void Generate (bool debugBuild, bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory)
		{
			if (String.IsNullOrEmpty (outputDirectory)) {
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));
			}
			Directory.CreateDirectory (outputDirectory);

			state.JniAddNativeMethodRegistrationAttributePresent = skipJniAddNativeMethodRegistrationAttributeScan;
			string typemapsOutputDirectory = Path.Combine (outputDirectory, "typemaps");
			if (debugBuild) {
				GenerateDebugNativeAssembly (typemapsOutputDirectory);
				return;
			}

			GenerateRelease (typemapsOutputDirectory);
		}

		void GenerateDebugNativeAssembly (string outputDirectory)
		{
			TypeMapDebugDataSets dataSets = state.GetDebugNativeEntries (needUniqueAssemblies: runtime == AndroidRuntime.CoreCLR);

			var data = new ModuleDebugData {
				EntryCount = (uint)dataSets.JavaToManaged.Count,
				JavaToManagedMap = dataSets.JavaToManaged,
				ManagedToJavaMap = dataSets.ManagedToJava,
				UniqueAssemblies = dataSets.UniqueAssemblies,
			};

			data.JavaToManagedMap.Sort ((TypeMapDebugEntry a, TypeMapDebugEntry b) => String.Compare (a.JavaName, b.JavaName, StringComparison.Ordinal));
			data.ManagedToJavaMap.Sort ((TypeMapDebugEntry a, TypeMapDebugEntry b) => String.Compare (a.ManagedName, b.ManagedName, StringComparison.Ordinal));

			LLVMIR.LlvmIrComposer composer = runtime switch {
				AndroidRuntime.MonoVM => new TypeMappingDebugNativeAssemblyGenerator (log, data),
				AndroidRuntime.CoreCLR => new TypeMappingDebugNativeAssemblyGeneratorCLR (log, data),
				_ => throw new NotSupportedException ($"Internal error: unsupported runtime {runtime}")
			};
			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);
		}

		void GenerateRelease (string outputDirectory)
		{
			var genState = state.GetReleaseGenerationState ();

			ModuleReleaseData [] modules = genState.TempModules.Values.ToArray ();
			Array.Sort (modules, new ModuleUUIDArrayComparer ());

			foreach (ModuleReleaseData module in modules) {
				if (module.TypesScratch.Count == 0) {
					module.Types = [];
					continue;
				}

				// No need to sort here, the LLVM IR generator will compute hashes and sort
				// the array on write.
				module.Types = module.TypesScratch.Values.ToArray ();
			}

			LLVMIR.LlvmIrComposer composer = runtime switch {
				AndroidRuntime.MonoVM => new TypeMappingReleaseNativeAssemblyGenerator (log, new NativeTypeMappingData (log, modules)),
				AndroidRuntime.CoreCLR => new TypeMappingReleaseNativeAssemblyGeneratorCLR (log, new NativeTypeMappingData (log, modules)),
				_ => throw new NotSupportedException ($"Internal error: unsupported runtime {runtime}")
			};

			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);
		}

		void GenerateNativeAssembly (LLVMIR.LlvmIrComposer composer, LLVMIR.LlvmIrModule typeMapModule, string baseFileName)
		{
			string outputFile = $"{baseFileName}.{MonoAndroidHelper.ArchToAbi (state.TargetArch)}.ll";

			// TODO: each .ll file should have a comment which lists paths to all the DLLs that were used to generate
			// the native code
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				try {
					composer.Generate (typeMapModule, state.TargetArch, sw, outputFile);
				} catch {
					throw;
				} finally {
					sw.Flush ();

					if (RunCheckedBuild) {
						if (Files.HasStreamChanged (sw.BaseStream, outputFile)) {
							Files.CopyIfStreamChanged (sw.BaseStream, outputFile + "2");
							log.LogError ("Output file changed");
						} else {
							log.LogMessage ($"RunCheckedBuild: Output file '{outputFile}' unchanged");
						}
					} else {
						Files.CopyIfStreamChanged (sw.BaseStream, outputFile);
					}
				}
			}
		}
	}

	// This abstraction is temporary to facilitate the transition from the old
	// typemap generator to the new one.  It will be removed once the transition
	// is complete.
	interface ITypeMapGeneratorAdapter
	{
		AndroidTargetArch TargetArch { get; }
		bool JniAddNativeMethodRegistrationAttributePresent { get; set; }
		TypeMapDebugDataSets GetDebugNativeEntries (bool needUniqueAssemblies);
		ReleaseGenerationState GetReleaseGenerationState ();
	}

	class NativeCodeGenStateAdapter : ITypeMapGeneratorAdapter
	{
		readonly NativeCodeGenState state;

		public NativeCodeGenStateAdapter (NativeCodeGenState state)
		{
			this.state = state ?? throw new ArgumentNullException (nameof (state));
		}

		public AndroidTargetArch TargetArch => state.TargetArch;

		public bool JniAddNativeMethodRegistrationAttributePresent {
			get => state.JniAddNativeMethodRegistrationAttributePresent;
			set => state.JniAddNativeMethodRegistrationAttributePresent = value;
		}

		public TypeMapDebugDataSets GetDebugNativeEntries (bool needUniqueAssemblies)
		{
			return TypeMapCecilAdapter.GetDebugNativeEntries (state, needUniqueAssemblies);
		}

		public ReleaseGenerationState GetReleaseGenerationState ()
		{
			return TypeMapCecilAdapter.GetReleaseGenerationState (state);
		}
	}

	class TypeMapObjectsFileAdapter : ITypeMapGeneratorAdapter
	{
		public List<TypeMapObjectsXmlFile> XmlFiles { get; } = [];
		public AndroidTargetArch TargetArch { get; }

		public TypeMapObjectsFileAdapter (AndroidTargetArch targetArch)
		{
			TargetArch = targetArch;
		}

		public bool JniAddNativeMethodRegistrationAttributePresent { get; set; }

		public TypeMapDebugDataSets GetDebugNativeEntries (bool needUniqueAssemblies)
		{
			var javaToManaged = new List<TypeMapDebugEntry> ();
			var managedToJava = new List<TypeMapDebugEntry> ();
			var uniqueAssemblies = new Dictionary<string, TypeMapDebugAssembly> (StringComparer.OrdinalIgnoreCase);

			foreach (var xml in XmlFiles) {
				if (!uniqueAssemblies.ContainsKey (xml.AssemblyName)) {
					var assm = new TypeMapDebugAssembly {
						MVID = xml.AssemblyMvid,
						MVIDBytes = xml.AssemblyMvid.ToByteArray (),
						Name = xml.AssemblyName,
					};
					uniqueAssemblies.Add (xml.AssemblyName, assm);
				}

				javaToManaged.AddRange (xml.JavaToManagedDebugEntries);
				managedToJava.AddRange (xml.ManagedToJavaDebugEntries);
			}

			// Handle entries with duplicate JavaNames
			GroupDuplicateDebugEntries (javaToManaged);
			GroupDuplicateDebugEntries (managedToJava);

			return new TypeMapDebugDataSets {
				JavaToManaged = javaToManaged,
				ManagedToJava = managedToJava,
				UniqueAssemblies = uniqueAssemblies.Values.ToList (),
			};
		}

		void GroupDuplicateDebugEntries (List<TypeMapDebugEntry> debugEntries)
		{
			foreach (var group in debugEntries.GroupBy (ent => ent.JavaName).Where (g => g.Count () > 1)) {
				// We need to sort:
				// - Types in Mono.Android come first
				// - Types that are not invokers come first
				var entries = group
					.OrderBy (e => e.IsMonoAndroid ? 0 : 1)
					.ThenBy (e => e.IsInvoker ? 1 : 0)
					.ToList ();

				for (var i = 1; i < entries.Count; i++)
					entries [i].DuplicateForJavaToManaged = entries [0];
			}
		}

		public ReleaseGenerationState GetReleaseGenerationState ()
		{
			var state = new ReleaseGenerationState ();

			foreach (var xml in XmlFiles)
				if (xml.HasReleaseEntries)
					state.TempModules.Add (xml.ModuleReleaseData.MvidBytes, xml.ModuleReleaseData);

			return state;
		}

		public static TypeMapObjectsFileAdapter? Create (AndroidTargetArch targetArch, List<ITaskItem> assemblies, TaskLoggingHelper log)
		{
			var adapter = new TypeMapObjectsFileAdapter (targetArch);

			foreach (var assembly in assemblies) {
				var typeMapPath = TypeMapObjectsXmlFile.GetTypeMapObjectsXmlFilePath (assembly.ItemSpec);

				if (!File.Exists (typeMapPath)) {
					log.LogError ($"'{typeMapPath}' not found.");
					return null;
				}

				var xml = TypeMapObjectsXmlFile.Import (typeMapPath);

				if (xml.WasScanned)
					adapter.XmlFiles.Add (xml);
			}

			return adapter;
		}
	}
}
