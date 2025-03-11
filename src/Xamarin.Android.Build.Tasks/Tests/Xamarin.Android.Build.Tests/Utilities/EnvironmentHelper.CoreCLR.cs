using System;
using System.Collections.Generic;

using NUnit.Framework;
namespace Xamarin.Android.Build.Tests;

partial class EnvironmentHelper
{
	// This must be identical to the like-named structure in src/native/clr/include/xamarin-app.hh
	public sealed class ApplicationConfigCLR
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
		public uint   android_runtime_jnienv_class_token;
		public uint   jnienv_initialize_method_token;
		public uint   jnienv_registerjninatives_method_token;
		public uint   jni_remapping_replacement_type_count;
		public uint   jni_remapping_replacement_method_index_entry_count;
		public string android_package_name = String.Empty;
		public bool   managed_marshal_methods_lookup_enabled;
	}

	const uint ApplicationConfigFieldCountCLR = 20;

	static readonly string[] requiredSharedLibrarySymbolsCLR = {
		"app_system_properties",
		"init_runtime_property_names",
		"init_runtime_property_values",
		"java_to_managed_hashes",
		"java_to_managed_map",
		"java_type_count",
		"java_type_names",
		"managed_to_java_map",
		"managed_to_java_map_module_count",
		AppEnvironmentVariablesSymbolName,
		ApplicationConfigSymbolName,
	};

	// Reads all the environment files, makes sure they all have identical contents in the
	// `application_config` structure and returns the config if the condition is true
	public static ApplicationConfigCLR? ReadApplicationConfigCLR (List<EnvironmentFile> envFilePaths)
	{
		if (envFilePaths.Count == 0) {
			return null;
		}

		ApplicationConfigCLR app_config = ReadApplicationConfigCLR (envFilePaths [0]);

		for (int i = 1; i < envFilePaths.Count; i++) {
			AssertApplicationConfigIsIdentical (app_config, envFilePaths [0].Path, ReadApplicationConfigCLR (envFilePaths[i]), envFilePaths[i].Path);
		}

		return app_config;
	}

	static ApplicationConfigCLR? ReadApplicationConfigCLR (EnvironmentFile envFile)
	{
		(NativeAssemblyParser parser, NativeAssemblyParser.AssemblerSymbol appConfigSymbol) = GetAssemblyParserAndValidateConfig (envFile);

		var pointers = new List <string> ();
		var ret = new ApplicationConfigCLR ();
		uint fieldCount = 0;
		string[] field;

		foreach (NativeAssemblyParser.AssemblerSymbolItem item in appConfigSymbol.Contents) {
			field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);

			if (CanIgnoreAssemblerField (field[0])) {
				continue;
			}

			switch (fieldCount) {
				case 0: // uses_assembly_preload: bool / .byte
					AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
					ret.uses_assembly_preload = ConvertFieldToBool ("uses_assembly_preload", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 1: // jni_add_native_method_registration_attribute_present: bool / .byte
					AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
					ret.jni_add_native_method_registration_attribute_present = ConvertFieldToBool ("jni_add_native_method_registration_attribute_present", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 2: // marshal_methods_enabled: bool / .byte
					AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
					ret.marshal_methods_enabled = ConvertFieldToBool ("marshal_methods_enabled", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 3: // ignore_split_configs: bool / .byte
					AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
					ret.ignore_split_configs = ConvertFieldToBool ("ignore_split_configs", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 4: // number_of_runtime_properties: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.number_of_runtime_properties = ConvertFieldToUInt32 ("number_of_runtime_properties", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 5: // package_naming_policy: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.package_naming_policy = ConvertFieldToUInt32 ("package_naming_policy", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 6: // environment_variable_count: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.environment_variable_count = ConvertFieldToUInt32 ("environment_variable_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 7: // system_property_count: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.system_property_count = ConvertFieldToUInt32 ("system_property_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 8: // number_of_assemblies_in_apk: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.number_of_assemblies_in_apk = ConvertFieldToUInt32 ("number_of_assemblies_in_apk", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 9: // bundled_assembly_name_width: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.bundled_assembly_name_width = ConvertFieldToUInt32 ("bundled_assembly_name_width", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 10: // number_of_dso_cache_entries: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.number_of_dso_cache_entries = ConvertFieldToUInt32 ("number_of_dso_cache_entries", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 11: // number_of_aot_cache_entries: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.number_of_aot_cache_entries = ConvertFieldToUInt32 ("number_of_aot_cache_entries", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 12: // number_of_shared_libraries: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.number_of_shared_libraries = ConvertFieldToUInt32 ("number_of_shared_libraries", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 13: // android_runtime_jnienv_class_token: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.android_runtime_jnienv_class_token = ConvertFieldToUInt32 ("android_runtime_jnienv_class_token", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 14: // jnienv_initialize_method_token: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.jnienv_initialize_method_token = ConvertFieldToUInt32 ("jnienv_initialize_method_token", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 15: // jnienv_registerjninatives_method_token: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.jnienv_registerjninatives_method_token = ConvertFieldToUInt32 ("jnienv_registerjninatives_method_token", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 16: // jni_remapping_replacement_type_count: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.jni_remapping_replacement_type_count = ConvertFieldToUInt32 ("jni_remapping_replacement_type_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 17: // jni_remapping_replacement_method_index_entry_count: uint32_t / .word | .long
					Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					ret.jni_remapping_replacement_method_index_entry_count = ConvertFieldToUInt32 ("jni_remapping_replacement_method_index_entry_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;

				case 18: // android_package_name: string / [pointer type]
					Assert.IsTrue (expectedPointerTypes.Contains (field [0]), $"Unexpected pointer field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
					pointers.Add (field [1].Trim ());
					break;

				case 19: // managed_marshal_methods_lookup_enabled: bool / .byte
					AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
					ret.managed_marshal_methods_lookup_enabled = ConvertFieldToBool ("managed_marshal_methods_lookup_enabled", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
					break;
			}
			fieldCount++;
		}

		Assert.AreEqual (1, pointers.Count, $"Invalid number of string pointers in 'application_config' structure in environment file '{envFile.Path}'");

		NativeAssemblyParser.AssemblerSymbol androidPackageNameSymbol = GetRequiredSymbol (pointers[0], envFile, parser);
		ret.android_package_name = GetStringContents (androidPackageNameSymbol, envFile, parser);

		Assert.AreEqual (ApplicationConfigFieldCountCLR, fieldCount, $"Invalid 'application_config' field count in environment file '{envFile.Path}'");
		Assert.IsFalse (String.IsNullOrEmpty (ret.android_package_name), $"Package name field in 'application_config' in environment file '{envFile.Path}' must not be null or empty");

		return ret;
	}

	static void AssertApplicationConfigIsIdentical (ApplicationConfigCLR firstAppConfig, string firstEnvFile, ApplicationConfigCLR secondAppConfig, string secondEnvFile)
	{
		Assert.AreEqual (firstAppConfig.uses_assembly_preload, secondAppConfig.uses_assembly_preload, $"Field 'uses_assembly_preload' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		Assert.AreEqual (firstAppConfig.marshal_methods_enabled, secondAppConfig.marshal_methods_enabled, $"Field 'marshal_methods_enabled' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		Assert.AreEqual (firstAppConfig.environment_variable_count, secondAppConfig.environment_variable_count, $"Field 'environment_variable_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		Assert.AreEqual (firstAppConfig.system_property_count, secondAppConfig.system_property_count, $"Field 'system_property_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		Assert.AreEqual (firstAppConfig.android_package_name, secondAppConfig.android_package_name, $"Field 'android_package_name' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		Assert.AreEqual (firstAppConfig.managed_marshal_methods_lookup_enabled, secondAppConfig.managed_marshal_methods_lookup_enabled, $"Field 'managed_marshal_methods_lookup_enabled' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
	}
}
