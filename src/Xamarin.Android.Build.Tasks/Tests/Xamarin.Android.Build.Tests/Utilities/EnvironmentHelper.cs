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
		// This must be identical to the like-named structure in src/monodroid/jni/xamarin-app.h
		public sealed class ApplicationConfig
		{
			public bool   uses_mono_llvm;
			public bool   uses_mono_aot;
			public bool   aot_lazy_load;
			public bool   uses_assembly_preload;
			public bool   is_a_bundled_app;
			public bool   broken_exception_transitions;
			public bool   instant_run_enabled;
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
			public string android_package_name;
		};
		const uint ApplicationConfigFieldCount = 20;

		static readonly object ndkInitLock = new object ();
		static readonly char[] readElfFieldSeparator = new [] { ' ', '\t' };
		static readonly Regex stringLabelRegex = new Regex ("^\\.L\\.autostr\\.[0-9]+:", RegexOptions.Compiled);

		static readonly HashSet <string> expectedPointerTypes = new HashSet <string> (StringComparer.Ordinal) {
			".long",
			".quad",
			".xword",
		};

		static readonly HashSet <string> expectedUInt32Types = new HashSet <string> (StringComparer.Ordinal) {
			".word",
			".long",
		};

		static readonly string[] requiredSharedLibrarySymbols = {
			"app_environment_variables",
			"app_system_properties",
			"application_config",
			"map_modules",
			"map_module_count",
			"java_type_count",
			"java_name_width",
			"map_java",
			"mono_aot_mode_name",
		};

		// Reads all the environment files, makes sure they all have identical contents in the
		// `application_config` structure and returns the config if the condition is true
		public static ApplicationConfig ReadApplicationConfig (List<string> envFilePaths)
		{
			if (envFilePaths.Count == 0)
				return null;

			ApplicationConfig app_config = ReadApplicationConfig (envFilePaths [0]);

			for (int i = 1; i < envFilePaths.Count; i++) {
				AssertApplicationConfigIsIdentical (app_config, envFilePaths [0], ReadApplicationConfig (envFilePaths[i]), envFilePaths[i]);
			}

			return app_config;
		}

		static ApplicationConfig ReadApplicationConfig (string envFile)
		{
			string[] lines = File.ReadAllLines (envFile, Encoding.UTF8);
			var strings = new Dictionary<string, string> (StringComparer.Ordinal);
			var pointers = new List <string> ();

			var ret = new ApplicationConfig ();
			bool gatherFields = false;
			uint fieldCount = 0;
			for (int i = 0; i < lines.Length; i++) {
				string line = lines [i];
				if (IsCommentLine (line))
					continue;

				string[] field;
				if (stringLabelRegex.IsMatch (line)) {
					string label = line.Substring (0, line.Length - 1);

					line = lines [++i];
					field = GetField (envFile, line, i);
					AssertFieldType (envFile, ".asciz", field [0], i);
					strings [label] = AssertIsAssemblerString (envFile, field [1], i);
					continue;
				}

				if (String.Compare ("application_config:", line.Trim (), StringComparison.Ordinal) == 0) {
					gatherFields = true;
					continue;
				}

				if (!gatherFields)
					continue;

				field = GetField (envFile, line, i);
				if (String.Compare (".zero", field [0], StringComparison.Ordinal) == 0)
					continue; // structure padding

				switch (fieldCount) {
					case 0: // uses_mono_llvm: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.uses_mono_llvm = ConvertFieldToBool ("uses_mono_llvm", envFile, i, field [1]);
						break;

					case 1: // uses_mono_aot: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.uses_mono_aot = ConvertFieldToBool ("uses_mono_aot", envFile, i, field [1]);
						break;

					case 2:
						// aot_lazy_load: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.uses_mono_aot = ConvertFieldToBool ("aot_lazy_load", envFile, i, field [1]);
						break;

					case 3: // uses_assembly_preload: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.uses_assembly_preload = ConvertFieldToBool ("uses_assembly_preload", envFile, i, field [1]);
						break;

					case 4: // is_a_bundled_app: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.is_a_bundled_app = ConvertFieldToBool ("is_a_bundled_app", envFile, i, field [1]);
						break;

					case 5: // broken_exception_transitions: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.broken_exception_transitions = ConvertFieldToBool ("broken_exception_transitions", envFile, i, field [1]);
						break;

					case 6: // instant_run_enabled: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.instant_run_enabled = ConvertFieldToBool ("instant_run_enabled", envFile, i, field [1]);
						break;

					case 7: // jni_add_native_method_registration_attribute_present: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.jni_add_native_method_registration_attribute_present = ConvertFieldToBool ("jni_add_native_method_registration_attribute_present", envFile, i, field [1]);
						break;

					case 8: // have_runtime_config_blob: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.have_runtime_config_blob = ConvertFieldToBool ("have_runtime_config_blob", envFile, i, field [1]);
						break;

					case 9: // have_assemblies_blob: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.have_assemblies_blob = ConvertFieldToBool ("have_assemblies_blob", envFile, i, field [1]);
						break;

					case 10: // bound_stream_io_exception_type: byte / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.bound_stream_io_exception_type = ConvertFieldToByte ("bound_stream_io_exception_type", envFile, i, field [1]);
						break;

					case 11: // package_naming_policy: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.package_naming_policy = ConvertFieldToUInt32 ("package_naming_policy", envFile, i, field [1]);
						break;

					case 12: // environment_variable_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.environment_variable_count = ConvertFieldToUInt32 ("environment_variable_count", envFile, i, field [1]);
						break;

					case 13: // system_property_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.system_property_count = ConvertFieldToUInt32 ("system_property_count", envFile, i, field [1]);
						break;

					case 14: // number_of_assemblies_in_apk: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.number_of_assemblies_in_apk = ConvertFieldToUInt32 ("number_of_assemblies_in_apk", envFile, i, field [1]);
						break;

					case 15: // bundled_assembly_name_width: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.bundled_assembly_name_width = ConvertFieldToUInt32 ("bundled_assembly_name_width", envFile, i, field [1]);
						break;

					case 16: // number_of_assembly_store_files: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.number_of_assembly_store_files = ConvertFieldToUInt32 ("number_of_assembly_store_files", envFile, i, field [1]);
						break;

					case 17: // number_of_dso_cache_entries: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.number_of_dso_cache_entries = ConvertFieldToUInt32 ("number_of_dso_cache_entries", envFile, i, field [1]);
						break;

					case 18: // mono_components_mask: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.mono_components_mask = ConvertFieldToUInt32 ("mono_components_mask", envFile, i, field [1]);
						break;

					case 19: // android_package_name: string / [pointer type]
						Assert.IsTrue (expectedPointerTypes.Contains (field [0]), $"Unexpected pointer field type in '{envFile}:{i}': {field [0]}");
						pointers.Add (field [1].Trim ());
						break;
				}
				fieldCount++;

				if (String.Compare (".size", field [0], StringComparison.Ordinal) == 0) {
					fieldCount--;
					Assert.IsTrue (field [1].StartsWith ("application_config", StringComparison.Ordinal), $"Mismatched .size directive in '{envFile}:{i}'");
					gatherFields = false; // We've reached the end of the application_config structure
				}
			}
			Assert.AreEqual (ApplicationConfigFieldCount, fieldCount, $"Invalid 'application_config' field count in environment file '{envFile}'");
			Assert.AreEqual (1, pointers.Count, $"Invalid number of string pointers in 'application_config' structure in environment file '{envFile}'");
			Assert.IsTrue (strings.TryGetValue (pointers [0], out ret.android_package_name), $"Invalid package name string pointer in 'application_config' structure in environment file '{envFile}'");
			Assert.IsFalse (String.IsNullOrEmpty (ret.android_package_name), $"Package name field in 'application_config' in environment file '{envFile}' must not be null or empty");

			return ret;
		}

		// Reads all the environment files, makes sure they contain the same environment variables (both count
		// and contents) and then returns a dictionary filled with the variables.
		public static Dictionary<string, string> ReadEnvironmentVariables (List<string> envFilePaths)
		{
			if (envFilePaths.Count == 0)
				return null;

			Dictionary<string, string> envvars = ReadEnvironmentVariables (envFilePaths [0]);
			if (envFilePaths.Count == 1)
				return envvars;

			for (int i = 1; i < envFilePaths.Count; i++) {
				AssertDictionariesAreEqual (envvars, envFilePaths [0], ReadEnvironmentVariables (envFilePaths[i]), envFilePaths[i]);
			}

			return envvars;
		}

		static Dictionary<string, string> ReadEnvironmentVariables (string envFile)
		{
			string[] lines = File.ReadAllLines (envFile, Encoding.UTF8);
			var strings = new Dictionary<string, string> (StringComparer.Ordinal);
			var pointers = new List <string> ();

			bool gatherPointers = false;
			for (int i = 0; i < lines.Length; i++) {
				string line = lines [i];
				if (IsCommentLine (line))
					continue;

				string[] field;
				if (stringLabelRegex.IsMatch (line)) {
					string label = line.Substring (0, line.Length - 1);

					line = lines [++i];
					field = GetField (envFile, line, i);

					AssertFieldType (envFile, ".asciz", field [0], i);
					strings [label] = AssertIsAssemblerString (envFile, field [1], i);
					continue;
				}

				if (String.Compare ("app_environment_variables:", line.Trim (), StringComparison.Ordinal) == 0) {
					gatherPointers = true;
					continue;
				}

				if (!gatherPointers)
					continue;

				field = GetField (envFile, line, i);
				if (String.Compare (".size", field [0], StringComparison.Ordinal) == 0) {
					Assert.IsTrue (field [1].StartsWith ("app_environment_variables", StringComparison.Ordinal), $"Mismatched .size directive in '{envFile}:{i}'");
					gatherPointers = false; // We've reached the end of the environment variable array
					continue;
				}

				Assert.IsTrue (expectedPointerTypes.Contains (field [0]), $"Unexpected pointer field type in '{envFile}:{i}': {field [0]}");
				pointers.Add (field [1].Trim ());
			}

			var ret = new Dictionary <string, string> (StringComparer.Ordinal);
			if (pointers.Count == 0)
				return ret;

			Assert.IsTrue (pointers.Count % 2 == 0, "Environment variable array must have an even number of elements");
			for (int i = 0; i < pointers.Count; i += 2) {
				string name;

				Assert.IsTrue (strings.TryGetValue (pointers [i], out name), $"[name] String with label '{pointers [i]}' not found in '{envFile}'");
				Assert.IsFalse (String.IsNullOrEmpty (name), $"Environment variable name must not be null or empty in {envFile} for string label '{pointers [i]}'");

				string value;
				Assert.IsTrue (strings.TryGetValue (pointers [i + 1], out value), $"[value] String with label '{pointers [i + 1]}' not found in '{envFile}'");
				Assert.IsNotNull (value, $"Environnment variable value must not be null in '{envFile}' for string label '{pointers [i + 1]}'");

				ret [name] = value;
			}

			return ret;
		}

		static bool IsCommentLine (string line)
		{
			string l = line?.Trim ();
			if (String.IsNullOrEmpty (l)) {
				return false;
			}

			return l.StartsWith ("/*", StringComparison.Ordinal) ||
				l.StartsWith ("//", StringComparison.Ordinal) ||
				l.StartsWith ("#", StringComparison.Ordinal) ||
				l.StartsWith ("@", StringComparison.Ordinal);
		}

		static string[] GetField (string file, string line, int lineNumber)
		{
			string[] ret = line?.Trim ()?.Split ('\t');
			Assert.IsTrue (ret.Length >= 2, $"Invalid assembler field format in file '{file}:{lineNumber}': '{line}'");

			return ret;
		}

		static void AssertFieldType (string file, string expectedType, string value, int lineNumber)
		{
			Assert.AreEqual (expectedType, value, $"Expected the '{expectedType}' field type in file '{file}:{lineNumber}': {value}");
		}

		static string AssertIsAssemblerString (string file, string value, int lineNumber)
		{
			string v = value.Trim ();
			Assert.IsTrue (v.StartsWith ("\"", StringComparison.Ordinal) && v.EndsWith("\"", StringComparison.Ordinal), $"Field value is not a valid assembler string in '{file}:{lineNumber}': {v}");
			return v.Trim ('"');
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

		static void AssertApplicationConfigIsIdentical (ApplicationConfig firstAppConfig, string firstEnvFile, ApplicationConfig secondAppConfig, string secondEnvFile)
		{
			Assert.AreEqual (firstAppConfig.uses_mono_llvm, secondAppConfig.uses_mono_llvm, $"Field 'uses_mono_llvm' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.uses_mono_aot, secondAppConfig.uses_mono_aot, $"Field 'uses_mono_aot' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.is_a_bundled_app, secondAppConfig.is_a_bundled_app, $"Field 'is_a_bundled_app' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.environment_variable_count, secondAppConfig.environment_variable_count, $"Field 'environment_variable_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.system_property_count, secondAppConfig.system_property_count, $"Field 'system_property_count' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
			Assert.AreEqual (firstAppConfig.android_package_name, secondAppConfig.android_package_name, $"Field 'android_package_name' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
		}

		public static List<string> GatherEnvironmentFiles (string outputDirectoryRoot, string supportedAbis, bool required)
		{
			var environmentFiles = new List <string> ();

			foreach (string abi in supportedAbis.Split (';')) {
				string envFilePath = Path.Combine (outputDirectoryRoot, "android", $"environment.{abi}.s");

				Assert.IsTrue (File.Exists (envFilePath), $"Environment file {envFilePath} does not exist");
				environmentFiles.Add (envFilePath);
			}

			if (required)
				Assert.AreNotEqual (0, environmentFiles.Count, "No environment files found");

			return environmentFiles;
		}

		public static void AssertValidEnvironmentSharedLibrary (string outputDirectoryRoot, string sdkDirectory, string ndkDirectory, string supportedAbis)
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
				AssertSharedLibraryHasRequiredSymbols (envSharedLibrary, readelf);
			}
		}

		static void AssertSharedLibraryHasRequiredSymbols (string dsoPath, string readElfPath)
		{
			var psi = new ProcessStartInfo {
				FileName = readElfPath,
				Arguments = $"--dyn-syms \"{dsoPath}\"",
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
					Assert.Fail ($"Failed to validate application environment SharedLibrary '{dsoPath}'");
				}
			}

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

		static void DumpLines (string streamName, List <string> lines)
		{
			if (lines == null || lines.Count == 0)
				return;

			TestContext.Out.WriteLine ($"{streamName}:");
			foreach (string line in lines) {
				TestContext.Out.WriteLine (line);
			}
		}

		static bool ConvertFieldToBool (string fieldName, string envFile, int fileLine, string value)
		{
			// Allow both decimal and hexadecimal values
			Assert.IsTrue (value.Length > 0 && value.Length <= 3, $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid boolean value (length not between 1 and 3)");

			uint fv;
			Assert.IsTrue (TryParseInteger (value, out fv), $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid boolean value (not a valid integer)");
			Assert.IsTrue (fv == 0 || fv == 1, $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid boolean value (not a valid boolean value 0 or 1)");

			return fv == 1;
		}

		static uint ConvertFieldToUInt32 (string fieldName, string envFile, int fileLine, string value)
		{
			Assert.IsTrue (value.Length > 0, $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid uint32_t value (not long enough)");

			uint fv;
			Assert.IsTrue (TryParseInteger (value, out fv), $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid uint32_t value (not a valid integer)");

			return fv;
		}

		static byte ConvertFieldToByte (string fieldName, string envFile, int fileLine, string value)
		{
			Assert.IsTrue (value.Length > 0, $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid uint8_t value (not long enough)");

			byte fv;
			Assert.IsTrue (TryParseInteger (value, out fv), $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid uint8_t value (not a valid integer)");

			return fv;
		}

		static bool TryParseInteger (string value, out uint fv)
		{
			if (value.StartsWith ("0x", StringComparison.Ordinal)) {
				return UInt32.TryParse (value.Substring (2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out fv);
			}

			return UInt32.TryParse (value, out fv);
		}

		static bool TryParseInteger (string value, out byte fv)
		{
			if (value.StartsWith ("0x", StringComparison.Ordinal)) {
				return Byte.TryParse (value.Substring (2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out fv);
			}

			return Byte.TryParse (value, out fv);
		}
	}
}
