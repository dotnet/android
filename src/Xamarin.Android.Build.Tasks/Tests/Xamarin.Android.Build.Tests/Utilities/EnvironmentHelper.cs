using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	class EnvironmentHelper
	{
		public sealed class EnvironmentFile
		{
			public readonly string Path;
			public readonly string ABI;

			public EnvironmentFile (string path, string abi)
			{
				if (String.IsNullOrEmpty (path)) {
					throw new ArgumentException ("must not be null or empty", nameof (path));
				}

				if (String.IsNullOrEmpty (abi)) {
					throw new ArgumentException ("must not be null or empty", nameof (abi));
				}

				Path = path;
				ABI = abi;
			}
		}

		public interface IApplicationConfig
		{};

		// This must be identical to the ApplicationConfig structure in src/native/clr/include/xamarin-app.hh
		public sealed class ApplicationConfig_CoreCLR : IApplicationConfig
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

		const uint ApplicationConfigFieldCount_CoreCLR = 20;

		// This must be identical to the ApplicationConfig structure in src/native/mono/xamarin-app-stub/xamarin-app.hh
		public sealed class ApplicationConfig_MonoVM : IApplicationConfig
		{
			public bool   uses_mono_llvm;
			public bool   uses_mono_aot;
			public bool   aot_lazy_load;
			public bool   uses_assembly_preload;
			public bool   broken_exception_transitions;
			public bool   jni_add_native_method_registration_attribute_present;
			public bool   have_runtime_config_blob;
			public bool   have_assemblies_blob;
			public bool   marshal_methods_enabled;
			public bool   ignore_split_configs;
			public byte   bound_stream_io_exception_type;
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
			public uint   mono_components_mask;
			public string android_package_name = String.Empty;
			public bool   managed_marshal_methods_lookup_enabled;
		}

		// This is shared between MonoVM and CoreCLR hosts, not used by NativeAOT
		public sealed class DSOCacheEntry64
		{
			// Hardcoded, by design - we want to know if there are any changes in the
			// native assembly layout.
			public const uint NativeSize_CoreCLR = 32;
			public const uint NativeSize_MonoVM = 40;

			public ulong hash;
			public ulong real_name_hash;
			public bool ignore;
			public bool is_jni_library;
			public string name; // real structure has an index here, we fetch the string to make it easier
			public IntPtr handle;
		}

		// This is a synthetic class, not reflecting what's in the generated LLVM IR/assembler source
		public sealed class JniPreloadsEntry
		{
			public uint Index;
			public string LibraryName;
		}

		// This is a synthetic class, not reflecting what's in the generated LLVM IR/assembler source
		public sealed class JniPreloads
		{
			public uint IndexStride;
			public List<JniPreloadsEntry> Entries;
			public string SourceFile;
		}

		const uint ApplicationConfigFieldCount_MonoVM = 27;

		const string ApplicationConfigSymbolName = "application_config";
		const string AppEnvironmentVariablesSymbolName = "app_environment_variables";
		const string AppEnvironmentVariableContentsSymbolName = "app_environment_variable_contents";

		const string AppEnvironmentVariablesNativeAOTSymbolName = "__naot_android_app_environment_variables";
		const string AppEnvironmentVariableContentsNativeAOTSymbolName = "__naot_android_app_environment_variable_contents";

		const string DsoJniPreloadsIdxStrideSymbolName = "dso_jni_preloads_idx_stride";
		const string DsoJniPreloadsIdxCountSymbolName = "dso_jni_preloads_idx_count";
		const string DsoJniPreloadsIdxSymbolName = "dso_jni_preloads_idx";
		const string DsoCacheSymbolName = "dso_cache";
		const string DsoNamesDataSymbolName = "dso_names_data";

		static readonly object ndkInitLock = new object ();
		static readonly char[] readElfFieldSeparator = new [] { ' ', '\t' };
		static readonly Regex assemblerLabelRegex = new Regex ("^[_.a-zA-Z0-9]+:", RegexOptions.Compiled);

		static readonly HashSet <string> expectedPointerTypes = new HashSet <string> (StringComparer.Ordinal) {
			".long",
			".quad",
			".xword",
		};

		static readonly HashSet <string> expectedUInt32Types = new HashSet <string> (StringComparer.Ordinal) {
			".word",
			".long",
		};

		static readonly HashSet <string> expectedUInt64Types = new HashSet <string> (StringComparer.Ordinal) {
			".xword",
			".quad",
		};

		static readonly string[] requiredSharedLibrarySymbolsMonoVM = {
			AppEnvironmentVariablesSymbolName,
			"app_system_properties",
			ApplicationConfigSymbolName,
			"format_tag",
			"map_modules",
			"map_module_count",
			"java_type_count",
			"map_java_hashes",
			"map_java",
			"mono_aot_mode_name",
		};

		static readonly string[] requiredSharedLibrarySymbolsCoreCLR = {
			"app_system_properties",
			"app_system_property_contents",
			"format_tag",
			"java_to_managed_hashes",
			"java_to_managed_map",
			"java_type_count",
			"managed_to_java_map",
			"managed_to_java_map_module_count",
			"runtime_properties",
			"runtime_properties_data",
			AppEnvironmentVariableContentsSymbolName,
			AppEnvironmentVariablesSymbolName,
			ApplicationConfigSymbolName,
		};

		static readonly string executableExtension;

		static EnvironmentHelper ()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				executableExtension = ".exe";
			} else {
				executableExtension = String.Empty;
			}
		}

		static string GenerateNativeAssemblyFromLLVM (string llvmIrFilePath)
		{
			Assert.AreEqual (".ll", Path.GetExtension (llvmIrFilePath), $"Invalid file extension for '{llvmIrFilePath}'");

			// Should match the arguments passed to llc by the CompileNativeAssembly task (except for the `--filetype` argument)
			const string assemblerOptions =
				"-O2 " +
				"--debugger-tune=lldb " + // NDK uses lldb now
				"--debugify-level=location+variables " +
				"--fatal-warnings " +
				"--filetype=asm " +
				"--relocation-model=pic";

			string llc = Path.Combine (TestEnvironment.OSBinDirectory, "binutils", "bin", $"llc{executableExtension}");
			string outputFilePath = Path.ChangeExtension (llvmIrFilePath, ".s");
			RunCommand (llc, $"{assemblerOptions} -o \"{outputFilePath}\" \"{llvmIrFilePath}\"");

			Assert.IsTrue (File.Exists (outputFilePath), $"Generated native assembler file '{outputFilePath}' does not exist");

			return outputFilePath;
		}

		static NativeAssemblyParser CreateAssemblyParser (EnvironmentFile envFile)
		{
			string assemblerFilePath = GenerateNativeAssemblyFromLLVM (envFile.Path);
			return new NativeAssemblyParser (assemblerFilePath, envFile.ABI);
		}

		static NativeAssemblyParser.AssemblerSymbol GetRequiredSymbol (string symbolName, EnvironmentFile envFile, NativeAssemblyParser parser)
		{
			if (!parser.Symbols.TryGetValue (symbolName, out NativeAssemblyParser.AssemblerSymbol symbol)) {
				Assert.Fail ($"Required symbol '{symbolName}' cannot be found in the '{envFile.Path}' file.");
			}

			Assert.IsNotNull (symbol, $"Assembler symbol '{symbolName}' must not be null in environment file '{envFile.Path}'");
			Assert.IsTrue (symbol.Contents.Count > 0, $"Assembler symbol '{symbolName}' must have at least one line of contents in environment file '{envFile.Path}'");

			return symbol;
		}

		static string GetStringContents (NativeAssemblyParser.AssemblerSymbol symbol, EnvironmentFile envFile, NativeAssemblyParser parser, int minimumLength = 1)
		{
			string[] field = GetField (envFile.Path, parser.SourceFilePath, symbol.Contents[0].Contents, symbol.Contents[0].LineNumber);

			Assert.IsTrue (field.Length >= 2, $"First line of symbol '{symbol.Name}' must have at least two fields in environment file '{parser.SourceFilePath}'. File generated from '{envFile.Path}'");
			Assert.IsTrue (String.Compare (".asciz", field[0], StringComparison.Ordinal) == 0, $"First line of symbol '{symbol.Name}' must begin with the '.asciz' directive in environment file '{parser.SourceFilePath}'. Instead it was '{field[0]}'. File generated from '{envFile.Path}'");

			// the extra 2 characters account for the enclosing '"'
			Assert.IsTrue (field[1].Length > 2 + minimumLength, $"Symbol '{symbol.Name}' must not be a string at least {minimumLength} characters long in environment file '{parser.SourceFilePath}'. File generated from '{envFile.Path}'");

			string s = field[1].Trim ();
			Assert.IsTrue (s[0] == '"' && s[s.Length - 1] == '"', $"Symbol '{symbol.Name}' value must be enclosed in double quotes in environment file '{parser.SourceFilePath}'. File generated from '{envFile.Path}'");

			return field[1].Trim ('"');
		}

		// Reads all the environment files, makes sure they all have identical contents in the
		// `application_config` structure and returns the config if the condition is true
		public static IApplicationConfig ReadApplicationConfig (List<EnvironmentFile> envFilePaths, AndroidRuntime runtime)
		{
			if (envFilePaths.Count == 0)
				return null;

			IApplicationConfig app_config = ReadApplicationConfig (envFilePaths [0], runtime);

			for (int i = 1; i < envFilePaths.Count; i++) {
				AssertApplicationConfigIsIdentical (app_config, envFilePaths [0].Path, ReadApplicationConfig (envFilePaths[i], runtime), envFilePaths[i].Path, runtime);
			}

			return app_config;
		}

		static IApplicationConfig ReadApplicationConfig (EnvironmentFile envFile, AndroidRuntime runtime)
		{
			return runtime switch {
				AndroidRuntime.MonoVM => ReadApplicationConfig_MonoVM (envFile),
				AndroidRuntime.CoreCLR => ReadApplicationConfig_CoreCLR (envFile),
				_ => throw new InvalidOperationException ($"Unsupported runtime '{runtime}'")
			};
		}

		static IApplicationConfig ReadApplicationConfig_CoreCLR (EnvironmentFile envFile)
		{
			NativeAssemblyParser parser = CreateAssemblyParser (envFile);

			if (!parser.Symbols.TryGetValue (ApplicationConfigSymbolName, out NativeAssemblyParser.AssemblerSymbol appConfigSymbol)) {
				Assert.Fail ($"Symbol '{ApplicationConfigSymbolName}' not found in LLVM IR file '{envFile.Path}'");
			}

			Assert.IsTrue (appConfigSymbol.Size != 0, $"{ApplicationConfigSymbolName} size as specified in the '.size' directive must not be 0");

			var pointers = new List <string> ();
			var ret = new ApplicationConfig_CoreCLR ();
			uint fieldCount = 0;
			string[] field;

			foreach (NativeAssemblyParser.AssemblerSymbolItem item in appConfigSymbol.Contents) {
				field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);

				if (String.Compare (".zero", field[0], StringComparison.Ordinal) == 0) {
					continue; // padding, we can safely ignore it
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
						ret.number_of_runtime_properties = ConvertFieldToByte ("number_of_runtime_properties", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
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

			Assert.AreEqual (ApplicationConfigFieldCount_CoreCLR, fieldCount, $"Invalid 'application_config' field count in environment file '{envFile.Path}'");
			Assert.IsFalse (String.IsNullOrEmpty (ret.android_package_name), $"Package name field in 'application_config' in environment file '{envFile.Path}' must not be null or empty");

			return ret;
		}

		static IApplicationConfig ReadApplicationConfig_MonoVM (EnvironmentFile envFile)
		{
			NativeAssemblyParser parser = CreateAssemblyParser (envFile);

			if (!parser.Symbols.TryGetValue (ApplicationConfigSymbolName, out NativeAssemblyParser.AssemblerSymbol appConfigSymbol)) {
				Assert.Fail ($"Symbol '{ApplicationConfigSymbolName}' not found in LLVM IR file '{envFile.Path}'");
			}

			Assert.IsTrue (appConfigSymbol.Size != 0, $"{ApplicationConfigSymbolName} size as specified in the '.size' directive must not be 0");

			var pointers = new List <string> ();
			var ret = new ApplicationConfig_MonoVM ();
			uint fieldCount = 0;
			string[] field;

			foreach (NativeAssemblyParser.AssemblerSymbolItem item in appConfigSymbol.Contents) {
				field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);

				if (String.Compare (".zero", field[0], StringComparison.Ordinal) == 0) {
					continue; // padding, we can safely ignore it
				}

				switch (fieldCount) {
					case 0: // uses_mono_llvm: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.uses_mono_llvm = ConvertFieldToBool ("uses_mono_llvm", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 1: // uses_mono_aot: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.uses_mono_aot = ConvertFieldToBool ("uses_mono_aot", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 2:
						// aot_lazy_load: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.aot_lazy_load = ConvertFieldToBool ("aot_lazy_load", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 3: // uses_assembly_preload: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.uses_assembly_preload = ConvertFieldToBool ("uses_assembly_preload", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 4: // broken_exception_transitions: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.broken_exception_transitions = ConvertFieldToBool ("broken_exception_transitions", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 5: // jni_add_native_method_registration_attribute_present: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.jni_add_native_method_registration_attribute_present = ConvertFieldToBool ("jni_add_native_method_registration_attribute_present", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 6: // have_runtime_config_blob: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.have_runtime_config_blob = ConvertFieldToBool ("have_runtime_config_blob", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 7: // have_assemblies_blob: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.have_assemblies_blob = ConvertFieldToBool ("have_assemblies_blob", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 8: // marshal_methods_enabled: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.marshal_methods_enabled = ConvertFieldToBool ("marshal_methods_enabled", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 9: // ignore_split_configs: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.ignore_split_configs = ConvertFieldToBool ("ignore_split_configs", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 10: // bound_stream_io_exception_type: byte / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.bound_stream_io_exception_type = ConvertFieldToByte ("bound_stream_io_exception_type", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 11: // package_naming_policy: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.package_naming_policy = ConvertFieldToUInt32 ("package_naming_policy", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 12: // environment_variable_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.environment_variable_count = ConvertFieldToUInt32 ("environment_variable_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 13: // system_property_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.system_property_count = ConvertFieldToUInt32 ("system_property_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 14: // number_of_assemblies_in_apk: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.number_of_assemblies_in_apk = ConvertFieldToUInt32 ("number_of_assemblies_in_apk", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 15: // bundled_assembly_name_width: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.bundled_assembly_name_width = ConvertFieldToUInt32 ("bundled_assembly_name_width", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 16: // number_of_dso_cache_entries: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.number_of_dso_cache_entries = ConvertFieldToUInt32 ("number_of_dso_cache_entries", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 17: // number_of_aot_cache_entries: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.number_of_aot_cache_entries = ConvertFieldToUInt32 ("number_of_aot_cache_entries", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 18: // number_of_shared_libraries: uint32_t / .word | long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.number_of_shared_libraries = ConvertFieldToUInt32 ("number_of_shared_libraries", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 19: // android_runtime_jnienv_class_token: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.android_runtime_jnienv_class_token = ConvertFieldToUInt32 ("android_runtime_jnienv_class_token", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 20: // jnienv_initialize_method_token: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.jnienv_initialize_method_token = ConvertFieldToUInt32 ("jnienv_initialize_method_token", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 21: // jnienv_registerjninatives_method_token: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.jnienv_registerjninatives_method_token = ConvertFieldToUInt32 ("jnienv_registerjninatives_method_token", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 22: // jni_remapping_replacement_type_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.jni_remapping_replacement_type_count = ConvertFieldToUInt32 ("jni_remapping_replacement_type_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 23: // jni_remapping_replacement_method_index_entry_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.jni_remapping_replacement_method_index_entry_count = ConvertFieldToUInt32 ("jni_remapping_replacement_method_index_entry_count", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 24: // mono_components_mask: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						ret.mono_components_mask = ConvertFieldToUInt32 ("mono_components_mask", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;

					case 25: // android_package_name: string / [pointer type]
						Assert.IsTrue (expectedPointerTypes.Contains (field [0]), $"Unexpected pointer field type in '{envFile.Path}:{item.LineNumber}': {field [0]}");
						pointers.Add (field [1].Trim ());
						break;

					case 26: // managed_marshal_methods_lookup_enabled: bool / .byte
						AssertFieldType (envFile.Path, parser.SourceFilePath, ".byte", field [0], item.LineNumber);
						ret.managed_marshal_methods_lookup_enabled = ConvertFieldToBool ("managed_marshal_methods_lookup_enabled", envFile.Path, parser.SourceFilePath, item.LineNumber, field [1]);
						break;
				}
				fieldCount++;
			}

			Assert.AreEqual (1, pointers.Count, $"Invalid number of string pointers in 'application_config' structure in environment file '{envFile.Path}'");

			NativeAssemblyParser.AssemblerSymbol androidPackageNameSymbol = GetRequiredSymbol (pointers[0], envFile, parser);
			ret.android_package_name = GetStringContents (androidPackageNameSymbol, envFile, parser);

			Assert.AreEqual (ApplicationConfigFieldCount_MonoVM, fieldCount, $"Invalid 'application_config' field count in environment file '{envFile.Path}'");
			Assert.IsFalse (String.IsNullOrEmpty (ret.android_package_name), $"Package name field in 'application_config' in environment file '{envFile.Path}' must not be null or empty");

			return ret;
		}

		// Reads all the environment files, makes sure they contain the same environment variables (both count
		// and contents) and then returns a dictionary filled with the variables.
		public static Dictionary<string, string> ReadEnvironmentVariables (List<EnvironmentFile> envFilePaths, AndroidRuntime runtime)
		{
			if (envFilePaths.Count == 0)
				return null;

			Dictionary<string, string> envvars = ReadEnvironmentVariables (envFilePaths [0], runtime);
			if (envFilePaths.Count == 1)
				return envvars;

			for (int i = 1; i < envFilePaths.Count; i++) {
				AssertDictionariesAreEqual (envvars, envFilePaths [0].Path, ReadEnvironmentVariables (envFilePaths[i], runtime), envFilePaths[i].Path);
			}

			return envvars;
		}

		static Dictionary<string, string> ReadEnvironmentVariables (EnvironmentFile envFile, AndroidRuntime runtime)
		{
			return runtime switch {
				AndroidRuntime.MonoVM => ReadEnvironmentVariables_MonoVM (envFile),
				AndroidRuntime.CoreCLR => ReadEnvironmentVariables_CoreCLR_NativeAOT (envFile, AppEnvironmentVariablesSymbolName, AppEnvironmentVariableContentsSymbolName),
				AndroidRuntime.NativeAOT => ReadEnvironmentVariables_CoreCLR_NativeAOT (envFile, AppEnvironmentVariablesNativeAOTSymbolName, AppEnvironmentVariableContentsNativeAOTSymbolName),
				_ => throw new InvalidOperationException ($"Unsupported runtime '{runtime}'")
			};
		}

		static Dictionary<string, string> ReadEnvironmentVariables_CoreCLR_NativeAOT (EnvironmentFile envFile, string envvarsSymbolName, string envvarsContentsSymbolName)
		{
			NativeAssemblyParser parser = CreateAssemblyParser (envFile);
			if (!parser.Symbols.TryGetValue (envvarsSymbolName, out NativeAssemblyParser.AssemblerSymbol appEnvvarsSymbol)) {
				Assert.Fail ($"Symbol '{envvarsSymbolName}' not found in LLVM IR file '{envFile.Path}'");
			}
			Assert.IsTrue (appEnvvarsSymbol.Size != 0, $"{envvarsSymbolName} size as specified in the '.size' directive must not be 0");
			Assert.IsTrue (appEnvvarsSymbol.Contents.Count % 2 == 0, $"{envvarsSymbolName} must contain an even number of items (contains {appEnvvarsSymbol.Contents.Count})");

			if (!parser.Symbols.TryGetValue (envvarsContentsSymbolName, out NativeAssemblyParser.AssemblerSymbol appEnvvarsContentsSymbol)) {
				Assert.Fail ($"Symbol '{envvarsContentsSymbolName}' not found in LLVM IR file '{envFile.Path}'");
			}
			Assert.IsTrue (appEnvvarsContentsSymbol.Size != 0, $"{envvarsContentsSymbolName} size as specified in the '.size' directive must not be 0");
			Assert.IsTrue (appEnvvarsContentsSymbol.Contents.Count == 1, $"{envvarsContentsSymbolName} symbol must have a single value.");

			string[] field;
			string contents = ReadStringBlob (envFile, appEnvvarsContentsSymbol, parser);
			var indexes = new List<(uint nameIdx, uint valueIdx)> ();

			// Environment variables are pairs of indexes into the contents array
			for (int i = 0; i < appEnvvarsSymbol.Contents.Count; i += 2) {
				NativeAssemblyParser.AssemblerSymbolItem varName = appEnvvarsSymbol.Contents[i];
				NativeAssemblyParser.AssemblerSymbolItem varValue = appEnvvarsSymbol.Contents[i + 1];

				indexes.Add ((GetIndex (varName, "name"), GetIndex (varValue, "value")));
			}

			// Contents array is a collection of strings terminated with the NUL character
			var ret = new Dictionary <string, string> (StringComparer.Ordinal);

			const string ContentsAssertionTag = "Environment Variables";
			foreach (var envvar in indexes) {
				Assert.IsTrue (envvar.nameIdx < appEnvvarsContentsSymbol.Size, $"Environment variable name index {envvar.nameIdx} is out of range of the contents array");
				Assert.IsTrue (envvar.valueIdx < appEnvvarsContentsSymbol.Size, $"Environment variable value index {envvar.valueIdx} is out of range of the contents array");

				ret.Add (
					GetStringFromBlobContents (ContentsAssertionTag, contents, envvar.nameIdx),
					GetStringFromBlobContents (ContentsAssertionTag, contents, envvar.valueIdx)
				);
			}

			return ret;

			uint GetIndex (NativeAssemblyParser.AssemblerSymbolItem item, string name)
			{
				// If the value is invalid, let it throw. This is by design.

				field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);
				Assert.IsTrue (expectedUInt32Types.Contains (field[0]), $"Environment variable {name} index field has invalid type '${field[0]}'");
				return UInt32.Parse (field[1], CultureInfo.InvariantCulture);
			}
		}

		static Dictionary<string, string> ReadEnvironmentVariables_MonoVM (EnvironmentFile envFile)
		{
			NativeAssemblyParser parser = CreateAssemblyParser (envFile);

			if (!parser.Symbols.TryGetValue (AppEnvironmentVariablesSymbolName, out NativeAssemblyParser.AssemblerSymbol appEnvvarsSymbol)) {
				Assert.Fail ($"Symbol '{AppEnvironmentVariablesSymbolName}' not found in LLVM IR file '{envFile.Path}'");
			}

			Assert.IsTrue (appEnvvarsSymbol.Size != 0, $"{AppEnvironmentVariablesSymbolName} size as specified in the '.size' directive must not be 0");

			string[] field;
			var pointers = new List <string> ();
			foreach (NativeAssemblyParser.AssemblerSymbolItem item in appEnvvarsSymbol.Contents) {
				field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);

				Assert.IsTrue (expectedPointerTypes.Contains (field [0]), $"Unexpected pointer field type in '{parser.SourceFilePath}:{item.LineNumber}': {field [0]}. File generated from '{envFile.Path}'");
				pointers.Add (field [1].Trim ());
			}

			var ret = new Dictionary <string, string> (StringComparer.Ordinal);
			if (pointers.Count == 0)
				return ret;

			Assert.IsTrue (pointers.Count % 2 == 0, "Environment variable array must have an even number of elements");
			for (int i = 0; i < pointers.Count; i += 2) {
				NativeAssemblyParser.AssemblerSymbol symbol = GetRequiredSymbol (pointers[i], envFile, parser);
				string name = GetStringContents (symbol, envFile, parser);

				symbol = GetRequiredSymbol (pointers[i + 1], envFile, parser);
				string value = GetStringContents (symbol, envFile, parser, minimumLength: 0);

				ret [name] = value;
			}

			return ret;
		}

		static string[] GetField (string llvmAssemblerFile, string nativeAssemblerFile, string line, ulong lineNumber)
		{
			string[] ret = line?.Trim ()?.Split ('\t');
			Assert.IsTrue (ret != null && ret.Length >= 2, $"Invalid assembler field format in file '{nativeAssemblerFile}:{lineNumber}': '{line}'. File generated from '{llvmAssemblerFile}'");

			return ret;
		}

		static void AssertFieldType (string llvmAssemblerFile, string nativeAssemblerFile, string expectedType, string value, ulong lineNumber)
		{
			Assert.AreEqual (expectedType, value, $"Expected the '{expectedType}' field type in file '{nativeAssemblerFile}:{lineNumber}': {value}. File generated from '{llvmAssemblerFile}'");
		}

		static void AssertDictionariesAreEqual (Dictionary <string, string> d1, string d1FileName, Dictionary <string, string> d2, string d2FileName)
		{
			Assert.AreEqual (d1.Count, d2.Count, $"File '{d2FileName}' has a different number of environment variables than file '{d2FileName}'");

			foreach (var kvp in d1) {
				string value;

				Assert.IsTrue (d2.TryGetValue (kvp.Key, out value), $"File '{d2FileName}' does not contain environment variable '{kvp.Key}'");
				Assert.AreEqual (kvp.Value, value, $"Value of environnment variable '{kvp.Key}' is different in file '{d2FileName}' than in file '{d1FileName}'");
			}
		}

		static void AssertApplicationConfigIsIdentical (IApplicationConfig firstAppConfig, string firstEnvFile, IApplicationConfig secondAppConfig, string secondEnvFile, AndroidRuntime runtime)
		{
			switch (runtime) {
				case AndroidRuntime.MonoVM:
					AssertApplicationConfigIsIdentical (
						(ApplicationConfig_MonoVM)firstAppConfig,
						firstEnvFile,
						(ApplicationConfig_MonoVM)secondAppConfig,
						secondEnvFile
					);
					break;

				case AndroidRuntime.CoreCLR:
					AssertApplicationConfigIsIdentical (
						(ApplicationConfig_CoreCLR)firstAppConfig,
						firstEnvFile,
						(ApplicationConfig_CoreCLR)secondAppConfig,
						secondEnvFile
					);
					break;

				default:
					throw new NotSupportedException ($"Unsupported runtime '{runtime}'");
			}
		}

		static void AssertApplicationConfigIsIdentical (ApplicationConfig_CoreCLR firstAppConfig, string firstEnvFile, ApplicationConfig_CoreCLR secondAppConfig, string secondEnvFile)
		{
			Assert.AreEqual (firstAppConfig.uses_assembly_preload, secondAppConfig.uses_assembly_preload, $"Field 'uses_assembly_preload' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.marshal_methods_enabled, secondAppConfig.marshal_methods_enabled, $"Field 'marshal_methods_enabled' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.environment_variable_count, secondAppConfig.environment_variable_count, $"Field 'environment_variable_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.system_property_count, secondAppConfig.system_property_count, $"Field 'system_property_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.android_package_name, secondAppConfig.android_package_name, $"Field 'android_package_name' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		}

		static void AssertApplicationConfigIsIdentical (ApplicationConfig_MonoVM firstAppConfig, string firstEnvFile, ApplicationConfig_MonoVM secondAppConfig, string secondEnvFile)
		{
			Assert.AreEqual (firstAppConfig.uses_mono_llvm, secondAppConfig.uses_mono_llvm, $"Field 'uses_mono_llvm' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.uses_mono_aot, secondAppConfig.uses_mono_aot, $"Field 'uses_mono_aot' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.environment_variable_count, secondAppConfig.environment_variable_count, $"Field 'environment_variable_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.system_property_count, secondAppConfig.system_property_count, $"Field 'system_property_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.android_package_name, secondAppConfig.android_package_name, $"Field 'android_package_name' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		}

		// TODO: remove the default from the `runtime` parameter once all tests are updated
		public static List<EnvironmentFile> GatherEnvironmentFiles (string outputDirectoryRoot, string supportedAbis, bool required, AndroidRuntime runtime = AndroidRuntime.CoreCLR)
		{
			var environmentFiles = new List <EnvironmentFile> ();
			bool isNativeAOT = runtime == AndroidRuntime.NativeAOT;

			foreach (string abi in supportedAbis.Split (';')) {
				string prefixDir = isNativeAOT ? MonoAndroidHelper.AbiToRid (abi) : String.Empty;
				string envFilePath = Path.Combine (outputDirectoryRoot, prefixDir, "android", $"environment.{abi}.ll");

				Assert.IsTrue (File.Exists (envFilePath), $"Environment file {envFilePath} does not exist");
				environmentFiles.Add (new EnvironmentFile (envFilePath, abi));
			}

			if (required)
				Assert.AreNotEqual (0, environmentFiles.Count, "No environment files found");

			return environmentFiles;
		}

		public static Dictionary<string, string> ReadNativeAotEnvironmentVariables (string outputDirectoryRoot, bool required = true)
		{
			var ret = new Dictionary<string, string> (StringComparer.Ordinal);

			string javaSourcePath = Path.Combine (outputDirectoryRoot, "android", "src", "net", "dot", "jni", "nativeaot", "NativeAotEnvironmentVars.java");
			bool exists = File.Exists (javaSourcePath);
			if (required) {
				Assert.IsTrue (exists, $"NativeAOT Java source with environment variables does not exist: {javaSourcePath}");
			} else if (!exists) {
				return ret;
			}

			var names = new List<string> ();
			var values = new List<string> ();
			bool collectingNames = false;
			bool collectingValues = false;
			int lineNum = 0;

			foreach (string l in File.ReadAllLines (javaSourcePath)) {
				lineNum++;
				string line = l.Trim ();
				switch (line) {
					case "static String[] envNames = new String[] {":
						collectingNames = true;
						collectingValues = false;
						continue;

					case "static String[] envValues = new String[] {":
						collectingValues = true;
						collectingNames = false;
						continue;

					case "};":
						collectingValues = false;
						collectingNames = false;
						continue;

					case "":
						continue;
				}

				if (!collectingValues && !collectingNames) {
					continue;
				}

				Assert.IsTrue (line[0] == '"', $"Line {lineNum} in '{javaSourcePath}' doesn't start with a double quote: '{line}'");
				Assert.IsTrue (line[line.Length - 1] == ',', $"Line {lineNum} in '{javaSourcePath}' doesn't end with a comma: '{line}'");
				Assert.IsTrue (line[line.Length - 2] == '"', $"Line {lineNum} in '{javaSourcePath}' doesn't close the quoted string properly: '{line}'");

				string data = line.Substring (1, line.Length - 3);
				if (collectingNames) {
					names.Add (data);
				} else if (collectingValues) {
					values.Add (data);
				}
			}

			Assert.AreEqual (names.Count, values.Count, $"Environment variable name and value arrays aren't of the same size in '{javaSourcePath}'");
			for (int i = 0; i < names.Count; i++) {
				ret.Add (names[i], values[i]);
			}

			return ret;
		}

		public static void AssertValidEnvironmentSharedLibrary (string outputDirectoryRoot, string sdkDirectory, string ndkDirectory, string supportedAbis, AndroidRuntime runtime)
		{
			NdkTools ndk = NdkTools.Create (ndkDirectory);
			MonoAndroidHelper.AndroidSdk = new AndroidSdkInfo ((arg1, arg2) => {}, sdkDirectory, ndkDirectory, AndroidSdkResolver.GetJavaSdkPath ());

			AndroidTargetArch arch;

			foreach (string abi in supportedAbis.Split (';')) {
				switch (abi) {
					case "armeabi-v7a":
						arch = AndroidTargetArch.Arm;
						break;

					case "arm64":
					case "arm64-v8a":
					case "aarch64":
						arch = AndroidTargetArch.Arm64;
						break;

					case "x86":
						arch = AndroidTargetArch.X86;
						break;

					case "x86_64":
						arch = AndroidTargetArch.X86_64;
						break;

					default:
						throw new Exception ("Unsupported Android target architecture ABI: " + abi);
				}

				string envSharedLibrary = Path.Combine (outputDirectoryRoot, "app_shared_libraries", abi, "libxamarin-app.so");
				Assert.IsTrue (File.Exists (envSharedLibrary), $"Application environment SharedLibrary '{envSharedLibrary}' must exist");

				// API level doesn't matter in this case
				var readelf = ndk.GetToolPath ("readelf", arch, 0);
				if (!File.Exists (readelf)) {
					readelf = ndk.GetToolPath ("llvm-readelf", arch, 0);
				}

				string[] requiredSharedLibrarySymbols = runtime switch {
					AndroidRuntime.MonoVM  => requiredSharedLibrarySymbolsMonoVM,
					AndroidRuntime.CoreCLR => requiredSharedLibrarySymbolsCoreCLR,
					_                      => throw new NotSupportedException ($"Unsupported runtime '{runtime}'")
				};

				AssertSharedLibraryHasRequiredSymbols (envSharedLibrary, readelf, requiredSharedLibrarySymbols);
			}
		}

		static void AssertSharedLibraryHasRequiredSymbols (string dsoPath, string readElfPath, string[] requiredSharedLibrarySymbols)
		{
			(List<string> stdout_lines, List<string> _) = RunCommand (readElfPath, $"--dyn-syms \"{dsoPath}\"");

			var symbols = new HashSet<string> (StringComparer.Ordinal);
			foreach (string line in stdout_lines) {
				string[] fields = line.Split (readElfFieldSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (fields.Length < 8 || !fields [0].EndsWith (":", StringComparison.Ordinal))
					continue;
				string symbolName = fields [7].Trim ();
				if (String.IsNullOrEmpty (symbolName))
					continue;

				symbols.Add (symbolName);
			}

			foreach (string symbol in requiredSharedLibrarySymbols) {
				Assert.IsTrue (symbols.Contains (symbol), $"Symbol '{symbol}' is missing from '{dsoPath}'");
			}
		}

		public static List<JniPreloads> ReadJniPreloads (List<EnvironmentFile> envFilePaths, uint expectedDsoCacheEntryCount, AndroidRuntime runtime)
		{
			var ret = new List<JniPreloads> ();

			foreach (EnvironmentFile envFile in envFilePaths) {
				JniPreloads preloads = runtime switch {
					AndroidRuntime.CoreCLR => ReadJniPreloads_CoreCLR (envFile, expectedDsoCacheEntryCount),
					AndroidRuntime.MonoVM  => ReadJniPreloads_MonoVM (envFile, expectedDsoCacheEntryCount),
					_                      => throw new NotSupportedException ($"Unsupported runtime '{runtime}'")
				};

				ret.Add (preloads);
			}

			return ret;
		}

		delegate List<DSOCacheEntry64> ReadDsoCacheFn (NativeAssemblyParser parser, EnvironmentFile envFile, NativeAssemblyParser.AssemblerSymbol dsoCacheSym);

		static JniPreloads ReadJniPreloads_Common (EnvironmentFile envFile, uint expectedDsoCacheEntryCount, uint dsoCacheEntrySize, ReadDsoCacheFn dsoReader)
		{
			NativeAssemblyParser parser = CreateAssemblyParser (envFile);

			NativeAssemblyParser.AssemblerSymbol dsoCache = GetNonEmptyRequiredSymbol (parser, envFile, DsoCacheSymbolName);
			uint calculatedDsoCacheEntryCount = (uint)(dsoCache.Size / dsoCacheEntrySize);
			Assert.IsTrue (calculatedDsoCacheEntryCount == expectedDsoCacheEntryCount, $"Calculated DSO cache entry count should be {expectedDsoCacheEntryCount} but was {calculatedDsoCacheEntryCount} instead.");

			uint calculatedDsoCacheEntrySize = (uint)(dsoCacheEntrySize * expectedDsoCacheEntryCount);
			Assert.IsTrue (calculatedDsoCacheEntrySize == dsoCache.Size, $"Calculated DSO cache size should be {dsoCache.Size} but was {calculatedDsoCacheEntrySize} instead.");

			List<DSOCacheEntry64> dsoCacheEntries = dsoReader (parser, envFile, dsoCache);
			Assert.IsTrue ((uint)dsoCacheEntries.Count == expectedDsoCacheEntryCount, $"DSO cache read from the source should have {expectedDsoCacheEntryCount} entries, it had {dsoCacheEntries.Count} instead.");

			NativeAssemblyParser.AssemblerSymbol dsoJniPreloadsIdxStride = GetNonEmptyRequiredSymbol (parser, envFile, DsoJniPreloadsIdxStrideSymbolName);
			uint preloadsStride = GetSymbolValueAsUInt32 (dsoJniPreloadsIdxStride);
			Assert.IsTrue (preloadsStride > 0, $"Symbol {dsoJniPreloadsIdxStride.Name} must have value larger than 0.");

			NativeAssemblyParser.AssemblerSymbol dsoJniPreloadsIdxCount = GetNonEmptyRequiredSymbol (parser, envFile, DsoJniPreloadsIdxCountSymbolName);
			ulong preloadsCount = GetSymbolValueAsUInt64 (dsoJniPreloadsIdxCount);
			Assert.IsTrue (preloadsCount > 0, $"Symbol {dsoJniPreloadsIdxCount.Name} must have value larger than 0.");

			NativeAssemblyParser.AssemblerSymbol dsoJniPreloadsIdx = GetNonEmptyRequiredSymbol (parser, envFile, DsoJniPreloadsIdxSymbolName);
			ulong calculatedPreloadsIdxSize = preloadsCount * 4; // single index field is a 32-bit integer
			Assert.IsTrue (dsoJniPreloadsIdx.Size == calculatedPreloadsIdxSize, $"JNI preloads index should have size of {calculatedPreloadsIdxSize} instead of {dsoJniPreloadsIdx.Size}");

			var preloadsIndex = new List<JniPreloadsEntry> ();
			for (int i = 0; i < (int)preloadsCount; i++) {
				(ulong lineNumber, string value) = ReadNextArrayIndex (envFile, parser, dsoJniPreloadsIdx, i, expectedUInt32Types);
				uint index = ConvertFieldToUInt32 ("index", envFile.Path, parser.SourceFilePath, lineNumber, value);

				Assert.True (index < (uint)dsoCacheEntries.Count, $"JNI preload index {index} is larger than the number of items in the DSO cache array ({dsoCacheEntries.Count})");
				preloadsIndex.Add (
					new JniPreloadsEntry {
						Index = index,
						LibraryName = dsoCacheEntries[(int)index].name,
					}
				);
			}
			Assert.IsTrue (preloadsCount == (uint)preloadsIndex.Count, $"JNI preload index count should be equal to {preloadsCount}, but was {preloadsIndex.Count} instead.");

			return new JniPreloads {
				IndexStride = preloadsStride,
				Entries = preloadsIndex,
				SourceFile = envFile.Path,
			};

			uint GetSymbolValueAsUInt32 (NativeAssemblyParser.AssemblerSymbol symbol)
			{
				NativeAssemblyParser.AssemblerSymbolItem item = symbol.Contents[0];
				string[] field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);
				Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected 32-bit integer field type for symbol {symbol.Name} in '{envFile.Path}:{item.LineNumber}': {field [0]}");
				return ConvertFieldToUInt32 (DsoJniPreloadsIdxStrideSymbolName, envFile.Path, parser.SourceFilePath, item.LineNumber, field[1]);
			}

			ulong GetSymbolValueAsUInt64 (NativeAssemblyParser.AssemblerSymbol symbol)
			{
				NativeAssemblyParser.AssemblerSymbolItem item = symbol.Contents[0];
				string[] field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);
				Assert.IsTrue (expectedUInt64Types.Contains (field [0]), $"Unexpected 64-bit integer field type for symbol {symbol.Name} in '{envFile.Path}:{item.LineNumber}': {field [0]}");
				return ConvertFieldToUInt64 (DsoJniPreloadsIdxStrideSymbolName, envFile.Path, parser.SourceFilePath, item.LineNumber, field[1]);
			}
		}

		static NativeAssemblyParser.AssemblerSymbol GetNonEmptyRequiredSymbol (NativeAssemblyParser parser, EnvironmentFile envFile, string symbolName)
		{
			var symbol = GetRequiredSymbol (symbolName, envFile, parser);

			Assert.IsTrue (symbol.Size != 0, $"{symbolName} size as specified in the '.size' directive must not be 0");
			return symbol;
		}

		static JniPreloads ReadJniPreloads_MonoVM (EnvironmentFile envFile, uint expectedDsoCacheEntryCount)
		{
			return ReadJniPreloads_Common (
				envFile,
				expectedDsoCacheEntryCount,
				DSOCacheEntry64.NativeSize_MonoVM,
				(NativeAssemblyParser parser, EnvironmentFile envFile, NativeAssemblyParser.AssemblerSymbol dsoCacheSym) => {
					return ReadDsoCache64_MonoVM (envFile, parser, dsoCacheSym);
				}
			);
		}

		static List<DSOCacheEntry64> ReadDsoCache64_MonoVM (EnvironmentFile envFile, NativeAssemblyParser parser, NativeAssemblyParser.AssemblerSymbol dsoCache)
		{
			var ret = new List<DSOCacheEntry64> ();

			// This follows a VERY strict format, by design. If anything changes in the generated source this is supposed
			// to break.
			//
			// The code is almost identical to that of CoreCLR, but it is kept completely separate on purpose - it makes the code simpler, since
			// it doesn't have to account for the small differences between runtimes and it also provides for independence of the two runtime
			// hosts.
			const int itemsPerEntry = 7; // Includes padding entries
			for (int i = 0; i < dsoCache.Contents.Count; i += itemsPerEntry) {
				ulong lineNumber;
				string value;
				int index = i;

				// uint64_t hash
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, expectedUInt64Types);
				ulong hash = ConvertFieldToUInt64 ("hash", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// uint64_t real_name_hash
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, expectedUInt64Types);
				ulong real_name_hash = ConvertFieldToUInt64 ("real_name_hash", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// bool ignore
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, ".byte");
				bool ignore = ConvertFieldToBool ("ignore", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// bool is_jni_library
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, ".byte");
				bool is_jni_library = ConvertFieldToBool ("is_jni_library", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// padding, 6 bytes
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index, ".zero");
				uint padding1 = ConvertFieldToUInt32 ("padding1", envFile.Path, parser.SourceFilePath, lineNumber, value);
				Assert.IsTrue (padding1 == 6, $"Padding field #1 at index {index} of symbol '{dsoCache.Name}' should have had a value of 6, instead it was set to {padding1}");
				index++;

				// .pointer_type SYMBOL_NAME
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, expectedUInt64Types);
				NativeAssemblyParser.AssemblerSymbol dsoLibNameSymbol = GetRequiredSymbol (value, envFile, parser);

				// void* handle
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index, expectedUInt64Types);
				ulong handle = ConvertFieldToUInt64 ("handle", envFile.Path, parser.SourceFilePath, lineNumber, value);
				Assert.IsTrue (handle == 0, $"Handle field at index {index} of symbol '{dsoCache.Name}' should have had a value of 0, instead it was set to {handle}");

				string name = GetStringContents (dsoLibNameSymbol, envFile, parser);

				ret.Add (
					new DSOCacheEntry64 {
						hash = hash,
						real_name_hash = real_name_hash,
						ignore = ignore,
						is_jni_library = is_jni_library,
						name = name,
						handle = IntPtr.Zero,
					}
				);
			}

			return ret;
		}

		static JniPreloads ReadJniPreloads_CoreCLR (EnvironmentFile envFile, uint expectedDsoCacheEntryCount)
		{
			return ReadJniPreloads_Common (
				envFile,
				expectedDsoCacheEntryCount,
				DSOCacheEntry64.NativeSize_CoreCLR,
				(NativeAssemblyParser parser, EnvironmentFile envFile, NativeAssemblyParser.AssemblerSymbol dsoCacheSym) => {
					NativeAssemblyParser.AssemblerSymbol dsoNamesData = GetNonEmptyRequiredSymbol (parser, envFile, DsoNamesDataSymbolName);
					Assert.IsTrue (dsoNamesData.Size > 0, "DSO names data must have size larger than zero");

					string dsoNames = ReadStringBlob (envFile, dsoNamesData, parser);
					Assert.IsTrue (dsoNames.Length > 0, "DSO names read from source mustn't be empty");

					return ReadDsoCache64_CoreCLR (envFile, parser, dsoCacheSym, dsoNames);
				}
			);
		}

		static List<DSOCacheEntry64> ReadDsoCache64_CoreCLR (EnvironmentFile envFile, NativeAssemblyParser parser, NativeAssemblyParser.AssemblerSymbol dsoCache, string dsoNamesBlob)
		{
			var ret = new List<DSOCacheEntry64> ();

			// This follows a VERY strict format, by design. If anything changes in the generated source this is supposed
			// to break.
			const int itemsPerEntry = 7; // Includes padding entries
			for (int i = 0; i < dsoCache.Contents.Count; i += itemsPerEntry) {
				ulong lineNumber;
				string value;
				int index = i;

				// uint64_t hash
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, expectedUInt64Types);
				ulong hash = ConvertFieldToUInt64 ("hash", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// uint64_t real_name_hash
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, expectedUInt64Types);
				ulong real_name_hash = ConvertFieldToUInt64 ("real_name_hash", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// bool ignore
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, ".byte");
				bool ignore = ConvertFieldToBool ("ignore", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// bool is_jni_library
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, ".byte");
				bool is_jni_library = ConvertFieldToBool ("is_jni_library", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// padding, 2 bytes
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index, ".zero");
				uint padding1 = ConvertFieldToUInt32 ("padding1", envFile.Path, parser.SourceFilePath, lineNumber, value);
				Assert.IsTrue (padding1 == 2, $"Padding field #1 at index {index} of symbol '{dsoCache.Name}' should have had a value of 2, instead it was set to {padding1}");
				index++;

				// uint32_t name_index
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index++, expectedUInt32Types);
				uint name_index = ConvertFieldToUInt32 ("name_index", envFile.Path, parser.SourceFilePath, lineNumber, value);

				// void* handle
				(lineNumber, value) = ReadNextArrayIndex (envFile, parser, dsoCache, index, expectedUInt64Types);
				ulong handle = ConvertFieldToUInt64 ("handle", envFile.Path, parser.SourceFilePath, lineNumber, value);
				Assert.IsTrue (handle == 0, $"Handle field at index {index} of symbol '{dsoCache.Name}' should have had a value of 0, instead it was set to {handle}");

				string name = GetStringFromBlobContents ("DSO JNI preloads", dsoNamesBlob, name_index);
				ret.Add (
					new DSOCacheEntry64 {
						hash = hash,
						real_name_hash = real_name_hash,
						ignore = ignore,
						is_jni_library = is_jni_library,
						name = name,
						handle = IntPtr.Zero,
					}
				);
			}

			return ret;
		}

		static (ulong line, string value) ReadNextArrayIndex (EnvironmentFile envFile, NativeAssemblyParser parser, NativeAssemblyParser.AssemblerSymbol array, int index, string expectedType)
		{
			return ReadNextArrayIndex (envFile, parser, array, index, new HashSet<string> (StringComparer.Ordinal) { expectedType });
		}

		static (ulong line, string value) ReadNextArrayIndex (EnvironmentFile envFile, NativeAssemblyParser parser, NativeAssemblyParser.AssemblerSymbol array,
		  int index, HashSet<string> expectedTypes)
		{
			Assert.IsFalse (index >= array.Contents.Count, $"Index {index} exceeds the number of items in the {array.Name} array.");
			NativeAssemblyParser.AssemblerSymbolItem item = array.Contents[index];

			string[] field = GetField (envFile.Path, parser.SourceFilePath, item.Contents, item.LineNumber);
			Assert.IsTrue (field.Length == 2, $"Item {index} of symbol {array.Name} at {envFile.Path}:{item.LineNumber} has an invalid value.");

			string expectedTypesList = String.Join (" | ", expectedTypes);
			Assert.IsTrue (expectedTypes.Contains (field[0]), $"Item {index} of symbol {array.Name} at {parser.SourceFilePath}:{item.LineNumber} should be of type '{expectedTypesList}', but was '{field[0]}' instead.");

			return (item.LineNumber, field[1]);
		}

		static (List<string> stdout, List<string> stderr) RunCommand (string executablePath, string arguments = null)
		{
			var psi = new ProcessStartInfo {
				FileName = executablePath,
				Arguments = arguments,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
			};

			psi.StandardOutputEncoding = Encoding.UTF8;
			psi.StandardErrorEncoding = Encoding.UTF8;

			var stdout_completed = new ManualResetEventSlim (false);
			var stderr_completed = new ManualResetEventSlim (false);
			var stdout_lines = new List <string> ();
			var stderr_lines = new List <string> ();

			using (var process = new Process ()) {
				process.StartInfo = psi;
				process.OutputDataReceived += (s, e) => {
					if (e.Data != null)
						stdout_lines.Add (e.Data);
					else
						stdout_completed.Set ();
				};

				process.ErrorDataReceived += (s, e) => {
					if (e.Data != null)
						stderr_lines.Add (e.Data);
					else
						stderr_completed.Set ();
				};

				process.Start ();
				process.BeginOutputReadLine ();
				process.BeginErrorReadLine ();
				bool exited = process.WaitForExit ((int)TimeSpan.FromSeconds (60).TotalMilliseconds);
				bool stdout_done = stdout_completed.Wait (TimeSpan.FromSeconds (30));
				bool stderr_done = stderr_completed.Wait (TimeSpan.FromSeconds (30));

				if (!exited)
					TestContext.Out.WriteLine ($"{psi.FileName} {psi.Arguments} timed out");
				if (process.ExitCode != 0)
					TestContext.Out.WriteLine ($"{psi.FileName} {psi.Arguments} returned with error code {process.ExitCode}");

				if (!exited || process.ExitCode != 0) {
					DumpLines ("stdout", stdout_lines);
					DumpLines ("stderr", stderr_lines);
					Assert.Fail ($"Command '{psi.FileName} {psi.Arguments}' failed to run or exited with error code");
				}
			}

			return (stdout_lines, stderr_lines);
		}

		static void DumpLines (string streamName, List <string> lines)
		{
			if (lines == null || lines.Count == 0)
				return;

			TestContext.Out.WriteLine ($"{streamName}:");
			foreach (string line in lines) {
				TestContext.Out.WriteLine (line);
			}
		}

		static bool ConvertFieldToBool (string fieldName, string llvmAssemblerEnvFile, string nativeAssemblerEnvFile, ulong fileLine, string value)
		{
			// Allow both decimal and hexadecimal values
			Assert.IsTrue (value.Length > 0 && value.Length <= 3, $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid boolean value (length not between 1 and 3). File generated from '{llvmAssemblerEnvFile}'");

			uint fv;
			Assert.IsTrue (TryParseInteger (value, out fv), $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid boolean value (not a valid integer). File generated from '{llvmAssemblerEnvFile}'");
			Assert.IsTrue (fv == 0 || fv == 1, $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid boolean value (not a valid boolean value 0 or 1). File generated from '{llvmAssemblerEnvFile}'");

			return fv == 1;
		}

		static uint ConvertFieldToUInt32 (string fieldName, string llvmAssemblerEnvFile, string nativeAssemblerEnvFile, ulong fileLine, string value)
		{
			Assert.IsTrue (value.Length > 0, $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid uint32_t value (not long enough). File generated from '{llvmAssemblerEnvFile}'");

			uint fv;
			Assert.IsTrue (TryParseInteger (value, out fv), $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid uint32_t value ('{value}' is not a valid integer). File generated from '{llvmAssemblerEnvFile}'");

			return fv;
		}

		static ulong ConvertFieldToUInt64 (string fieldName, string llvmAssemblerEnvFile, string nativeAssemblerEnvFile, ulong fileLine, string value)
		{
			Assert.IsTrue (value.Length > 0, $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid uint64_t value (not long enough). File generated from '{llvmAssemblerEnvFile}'");

			ulong fv;
			Assert.IsTrue (TryParseInteger (value, out fv), $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid uint64_t value ('{value}' is not a valid integer). File generated from '{llvmAssemblerEnvFile}'");

			return fv;
		}

		static byte ConvertFieldToByte (string fieldName, string llvmAssemblerEnvFile, string nativeAssemblerEnvFile, ulong fileLine, string value)
		{
			Assert.IsTrue (value.Length > 0, $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid uint8_t value (not long enough). File generated from '{llvmAssemblerEnvFile}'");

			byte fv;
			Assert.IsTrue (TryParseInteger (value, out fv), $"Field '{fieldName}' in {nativeAssemblerEnvFile}:{fileLine} is not a valid uint8_t value ('{value}' is not a valid integer). File generated from '{llvmAssemblerEnvFile}'");

			return fv;
		}

		// Integers are parsed as signed, since llc will always output signed integers.
		static bool TryParseInteger (string value, out uint fv)
		{
			if (value.StartsWith ("0x", StringComparison.Ordinal)) {
				return UInt32.TryParse (value.Substring (2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out fv);
			}

			fv = 0;
			if (!Int32.TryParse (value, out int signedFV)) {
				return false;
			}

			fv = (uint)signedFV;
			return true;
		}

		static bool TryParseInteger (string value, out ulong fv)
		{
			if (value.StartsWith ("0x", StringComparison.Ordinal)) {
				return UInt64.TryParse (value.Substring (2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out fv);
			}

			fv = 0;
			if (!Int64.TryParse (value, out long signedFV)) {
				return false;
			}

			fv = (ulong)signedFV;
			return true;
		}

		static bool TryParseInteger (string value, out byte fv)
		{
			if (value.StartsWith ("0x", StringComparison.Ordinal)) {
				return Byte.TryParse (value.Substring (2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out fv);
			}

			return Byte.TryParse (value, out fv);
		}

		static string ReadStringBlob (EnvironmentFile envFile, NativeAssemblyParser.AssemblerSymbol contentsSymbol, NativeAssemblyParser parser)
		{
			NativeAssemblyParser.AssemblerSymbolItem contentsItem = contentsSymbol.Contents[0];
			string[] field = GetField (envFile.Path, parser.SourceFilePath, contentsItem.Contents, contentsItem.LineNumber);
			Assert.IsTrue (field[0] == ".asciz", $"{contentsSymbol.Name} must be of '.asciz' type");

			var sb = new StringBuilder ();
			// We need to get rid of the '"' delimiter llc outputs..
			sb.Append (field[1].Trim ('"'));

			// ...and llc outputs NUL as the octal '\000' sequence, we need an actual NUL...
			sb.Replace ("\\000", "\0");

			// ...and since it's an .asciz variable, the string doesn't contain explicit terminating NUL, but we need one
			sb.Append ('\0');

			return sb.ToString ();
		}

		static string GetStringFromBlobContents (string assertionTag, string contents, uint idx)
		{
			var sb = new StringBuilder ();
			bool foundNull = false;

			for (int i = (int)idx; i < contents.Length; i++) {
				if (contents[i] == '\0') {
					foundNull = true;
					break;
				}
				sb.Append (contents[i]);
			}

			Assert.IsTrue (foundNull, $"[{assertionTag} string starting at index {idx} of a string blob is not NUL-terminated");
			return sb.ToString ();
		}
	}
}
