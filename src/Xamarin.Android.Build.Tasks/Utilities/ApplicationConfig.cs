using System;

namespace Xamarin.Android.Tasks
{
	// Declaration order of fields and their types must correspond *exactly* to that in
	// src/monodroid/jni/xamarin-app.hh ApplicationConfig structure
	//
	// Type mappings:
	//
	//     C++           C#
	// -----------|----------
	//   bool     |  bool
	//   uint8_t  |  byte
	//   int8_t   |  sbyte
	//   uint16_t |  ushort
	//   int16_t  |  short
	//   uint32_t |  uint
	//   int32_t  |  int
	//   uint64_t |  ulong
	//   int64_t  |  long
	//   char*    |  string
	//   
	// Names should be the same as in the above struct, but it's not a requirement
	// (they will be used only to generate comments in the native code)
	sealed class ApplicationConfig
	{
		public bool   uses_mono_llvm;
		public bool   uses_mono_aot;
		public bool   uses_assembly_preload;
		public bool   is_a_bundled_app;
		public bool   broken_exception_transitions;
		public bool   instant_run_enabled ;
		public bool   jni_add_native_method_registration_attribute_present;
		public bool   have_runtime_config_blob;
		public bool   have_assemblies_blob;
		public byte   bound_stream_io_exception_type;
		public uint   package_naming_policy;
		public uint   environment_variable_count;
		public uint   system_property_count;
		public uint   number_of_assemblies_in_apk;
		public uint   bundled_assembly_name_width;
		public uint   number_of_assembly_store_files;
		public uint   number_of_dso_cache_entries;
		public uint   mono_components_mask;
		public string android_package_name = String.Empty;
	}
}
