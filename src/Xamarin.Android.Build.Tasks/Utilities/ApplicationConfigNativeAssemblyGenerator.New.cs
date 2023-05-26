using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVM.IR;

namespace Xamarin.Android.Tasks.New
{
	// TODO: remove these aliases once the refactoring is done
	using ApplicationConfig = Xamarin.Android.Tasks.ApplicationConfig;
	using LlvmIrVariableOptions = LLVMIR.LlvmIrVariableOptions;

	// Must match the MonoComponent enum in src/monodroid/jni/xamarin-app.hh
	[Flags]
	enum MonoComponent
	{
		None      = 0x00,
		Debugger  = 0x01,
		HotReload = 0x02,
		Tracing   = 0x04,
	}

	class ApplicationConfigNativeAssemblyGenerator : LlvmIrComposer
	{
		sealed class DSOCacheEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var dso_entry = data as DSOCacheEntry;
				if (dso_entry == null) {
					throw new InvalidOperationException ("Invalid data type, expected an instance of DSOCacheEntry");
				}

				if (String.Compare ("hash", fieldName, StringComparison.Ordinal) == 0) {
					return $"hash 0x{dso_entry.hash:x}, from name: {dso_entry.HashedName}";
				}

				if (String.Compare ("name", fieldName, StringComparison.Ordinal) == 0) {
					return $"name: {dso_entry.name}";
				}

				return String.Empty;
			}
		}

		// Order of fields and their type must correspond *exactly* (with exception of the
		// ignored managed members) to that in
		// src/monodroid/jni/xamarin-app.hh DSOCacheEntry structure
		[NativeAssemblerStructContextDataProvider (typeof (DSOCacheEntryContextDataProvider))]
		sealed class DSOCacheEntry
		{
			[NativeAssembler (Ignore = true)]
			public string HashedName;

			[NativeAssembler (UsesDataProvider = true)]
			public ulong hash;
			public bool ignore;

			public string name;
			public IntPtr handle = IntPtr.Zero;
		}

		// Keep in sync with FORMAT_TAG in src/monodroid/jni/xamarin-app.hh
		const ulong FORMAT_TAG = 0x015E6972616D58;

		StructureInfo<ApplicationConfig>? applicationConfigStructureInfo;
		StructureInfo<DSOCacheEntry>? dsoCacheEntryStructureInfo;

		public bool UsesMonoAOT { get; set; }
		public bool UsesMonoLLVM { get; set; }
		public bool UsesAssemblyPreload { get; set; }
		public string MonoAOTMode { get; set; }
		public bool AotEnableLazyLoad { get; set; }
		public string AndroidPackageName { get; set; }
		public bool BrokenExceptionTransitions { get; set; }
		public global::Android.Runtime.BoundExceptionType BoundExceptionType { get; set; }
		public bool InstantRunEnabled { get; set; }
		public bool JniAddNativeMethodRegistrationAttributePresent { get; set; }
		public bool HaveRuntimeConfigBlob { get; set; }
		public bool HaveAssemblyStore { get; set; }
		public int NumberOfAssembliesInApk { get; set; }
		public int NumberOfAssemblyStoresInApks { get; set; }
		public int BundledAssemblyNameWidth { get; set; } // including the trailing NUL
		public int AndroidRuntimeJNIEnvToken { get; set; }
		public int JNIEnvInitializeToken { get; set; }
		public int JNIEnvRegisterJniNativesToken { get; set; }
		public int JniRemappingReplacementTypeCount { get; set; }
		public int JniRemappingReplacementMethodIndexEntryCount { get; set; }
		public MonoComponent MonoComponents { get; set; }
		public PackageNamingPolicy PackageNamingPolicy { get; set; }
		public List<ITaskItem> NativeLibraries { get; set; }
		public bool MarshalMethodsEnabled { get; set; }

		public ApplicationConfigNativeAssemblyGenerator (IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties, TaskLoggingHelper log)
		{
		}

		protected override void Construct (LlvmIrModule module)
		{
			MapStructures (module);

			var format_tag = new LlvmIrGlobalVariable (FORMAT_TAG.GetType (), "format_tag") {
				Value = FORMAT_TAG,
			};
			module.Add (format_tag);
		}

		void MapStructures (LlvmIrModule module)
		{
			applicationConfigStructureInfo = module.MapStructure<ApplicationConfig> ();
			dsoCacheEntryStructureInfo = module.MapStructure<DSOCacheEntry> ();
		}
	}
}
