using System;

namespace Xamarin.Android.Tasks;

// Declaration order of fields and their types must correspond *exactly* to that in
// src/native/clr/xamarin-app-stub/xamarin-app.hh ApplicationConfig structure
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
sealed class ApplicationConfigCLR
{
	public bool   uses_assembly_preload;
	public bool   jni_add_native_method_registration_attribute_present;
	public bool   marshal_methods_enabled;
	public bool   ignore_split_configs;
	public uint   number_of_runtime_properties;
	public uint   package_naming_policy;
	public uint   environment_variable_count;
	public uint   system_property_count;
	public uint   number_of_assemblies_in_apk;
	public uint   bundled_assembly_name_width;
	public uint   number_of_dso_cache_entries;
	public uint   number_of_aot_cache_entries;
	public uint   number_of_shared_libraries;

	[NativeAssembler (NumberFormat = LLVMIR.LlvmIrVariableNumberFormat.Hexadecimal)]
	public uint   android_runtime_jnienv_class_token;

	[NativeAssembler (NumberFormat = LLVMIR.LlvmIrVariableNumberFormat.Hexadecimal)]
	public uint   jnienv_initialize_method_token;

	[NativeAssembler (NumberFormat = LLVMIR.LlvmIrVariableNumberFormat.Hexadecimal)]
	public uint   jnienv_registerjninatives_method_token;
	public uint   jni_remapping_replacement_type_count;
	public uint   jni_remapping_replacement_method_index_entry_count;
	public string android_package_name = String.Empty;
}
