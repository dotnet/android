using System;
using System.IO;

using Xamarin.Android.Tasks.LLVM.IR;

namespace Xamarin.Android.Tasks.New
{
	using ApplicationConfig = Xamarin.Android.Tasks.ApplicationConfig;

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

		protected override void Construct (LlvmIrModule module)
		{}
	}
}
