using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	class TypeMapGenerator
	{
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
			public string JavaName;
			public string ManagedTypeName;
			public uint Token;
			public int ModuleIndex = -1;
			public bool SkipInJavaToManaged;
		}

		internal sealed class ModuleReleaseData
		{
			public Guid Mvid;
			public byte[] MvidBytes;
			public TypeMapReleaseEntry[] Types;
			public List<TypeMapReleaseEntry> DuplicateTypes;
			public string AssemblyName;

			public Dictionary<string, TypeMapReleaseEntry> TypesScratch;
		}

		internal sealed class TypeMapDebugEntry
		{
			public string JavaName;
			public string ManagedName;
			public bool SkipInJavaToManaged;
			public TypeMapDebugEntry DuplicateForJavaToManaged;

			// This field is only used by the Cecil adapter for temp storage while reading.
			// It is not used to create the typemap.
			public TypeDefinition TypeDefinition;

			public override string ToString ()
			{
				return $"TypeMapDebugEntry{{JavaName={JavaName}, ManagedName={ManagedName}, SkipInJavaToManaged={SkipInJavaToManaged}, DuplicateForJavaToManaged={DuplicateForJavaToManaged}}}";
			}
		}

		// Widths include the terminating nul character but not the padding!
		internal sealed class ModuleDebugData
		{
			public uint EntryCount;
			public List<TypeMapDebugEntry> JavaToManagedMap;
			public List<TypeMapDebugEntry> ManagedToJavaMap;
		}

		internal sealed class ReleaseGenerationState
		{
			int assemblyId = 0;

			public readonly Dictionary<string, int> KnownAssemblies;
			public readonly Dictionary <Guid, byte[]> MvidCache;
			public readonly Dictionary<byte[], ModuleReleaseData> TempModules;

			public ReleaseGenerationState ()
			{
				KnownAssemblies = new Dictionary<string, int> (StringComparer.Ordinal);
				MvidCache = new Dictionary<Guid, byte[]> ();
				TempModules = new Dictionary<byte[], ModuleReleaseData> ();
			}

			public void AddKnownAssembly (string assemblyName)
			{
				if (KnownAssemblies.ContainsKey (assemblyName)) {
					return;
				}

				KnownAssemblies.Add (assemblyName, ++assemblyId);
			}
		}

		readonly TaskLoggingHelper log;
		readonly NativeCodeGenState state;
		readonly AndroidRuntime runtime;

		public IList<string> GeneratedBinaryTypeMaps { get; } = new List<string> ();

		public TypeMapGenerator (TaskLoggingHelper log, NativeCodeGenState state, AndroidRuntime runtime)
		{
			this.log = log ?? throw new ArgumentNullException (nameof (log));
			this.state = state ?? throw new ArgumentNullException (nameof (state));
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
			(var javaToManaged, var managedToJava) = TypeMapCecilAdapter.GetDebugNativeEntries (state);

			var data = new ModuleDebugData {
				EntryCount = (uint)javaToManaged.Count,
				JavaToManagedMap = javaToManaged,
				ManagedToJavaMap = managedToJava,
			};

			data.JavaToManagedMap.Sort ((TypeMapDebugEntry a, TypeMapDebugEntry b) => String.Compare (a.JavaName, b.JavaName, StringComparison.Ordinal));
			data.ManagedToJavaMap.Sort ((TypeMapDebugEntry a, TypeMapDebugEntry b) => String.Compare (a.ManagedName, b.ManagedName, StringComparison.Ordinal));

			var composer = new TypeMappingDebugNativeAssemblyGenerator (log, data);
			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);
		}

		void GenerateRelease (string outputDirectory)
		{
			var genState = TypeMapCecilAdapter.GetReleaseGenerationState (state);

			ModuleReleaseData [] modules = genState.TempModules.Values.ToArray ();
			Array.Sort (modules, new ModuleUUIDArrayComparer ());

			foreach (ModuleReleaseData module in modules) {
				if (module.TypesScratch.Count == 0) {
					module.Types = Array.Empty<TypeMapReleaseEntry> ();
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
					Files.CopyIfStreamChanged (sw.BaseStream, outputFile);
				}
			}
		}
	}
}
