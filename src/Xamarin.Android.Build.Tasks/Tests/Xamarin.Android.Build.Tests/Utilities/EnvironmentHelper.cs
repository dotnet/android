using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests
{
	class EnvironmentHelper
	{
		// This must be identical to the like-named structure in src/monodroid/jni/xamarin-app.h
		public sealed class ApplicationConfig
		{
			public bool   uses_mono_llvm;
			public bool   uses_mono_aot;
			public bool   uses_embedded_dsos;
			public bool   uses_assembly_preload;
			public bool   is_a_bundled_app;
			public uint   environment_variable_count;
			public uint   system_property_count;
			public string android_package_name;
		};
		const uint ApplicationConfigFieldCount = 8;

		static readonly object ndkInitLock = new object ();
		static readonly char[] readElfFieldSeparator = new [] { ' ', '\t' };
		static readonly Regex stringLabelRegex = new Regex ("^\\.L\\.str\\.[0-9]+:", RegexOptions.Compiled);

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
			"jm_typemap",
			"jm_typemap_header",
			"mj_typemap",
			"mj_typemap_header",
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

					case 2: // uses_embedded_dsos: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.uses_embedded_dsos = ConvertFieldToBool ("uses_embedded_dsos", envFile, i, field [1]);
						break;

					case 3: // uses_assembly_preload: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.uses_assembly_preload = ConvertFieldToBool ("uses_assembly_preload", envFile, i, field [1]);
						break;

					case 4: // is_a_bundled_app: bool / .byte
						AssertFieldType (envFile, ".byte", field [0], i);
						ret.is_a_bundled_app = ConvertFieldToBool ("is_a_bundled_app", envFile, i, field [1]);
						break;

					case 5: // environment_variable_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.environment_variable_count = ConvertFieldToUInt32 ("environment_variable_count", envFile, i, field [1]);
						break;

					case 6: // system_property_count: uint32_t / .word | .long
						Assert.IsTrue (expectedUInt32Types.Contains (field [0]), $"Unexpected uint32_t field type in '{envFile}:{i}': {field [0]}");
						ret.system_property_count = ConvertFieldToUInt32 ("system_property_count", envFile, i, field [1]);
						break;

					case 7: // android_package_name: string / [pointer type]
						Assert.IsTrue (expectedPointerTypes.Contains (field [0]), $"Unexpected pointer field type in '{envFile}:{i}': {field [0]}");
						pointers.Add (field [1].Trim ());
						break;
				}
				fieldCount++;

				if (String.Compare (".size", field [0], StringComparison.Ordinal) == 0) {
					fieldCount--;
					Assert.IsTrue (field [1].StartsWith ("application_config", StringComparison.Ordinal), $"Mismatched .size directive in '{envFile}:{i}'");
					break; // We've reached the end of the application_config structure
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
					break; // We've reached the end of the environment variable array
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
			return !String.IsNullOrEmpty (l) && l.StartsWith ("/*", StringComparison.Ordinal);
		}

		static string[] GetField (string file, string line, int lineNumber)
		{
			string[] ret = line?.Trim ()?.Split ('\t');
			Assert.AreEqual (2, ret.Length, $"Invalid assembler field format in file '{file}:{lineNumber}': '{line}'");

			return ret;
		}

		static void AssertFieldType (string file, string expectedType, string value, int lineNumber)
		{
			Assert.AreEqual (expectedType, value, $"Expected the '{expectedType}' field type in file '{file}:{lineNumber}': {value}");
		}

		static string AssertIsAssemblerString (string file, string value, int lineNumber)
		{
			string v = value.Trim ();
			Assert.IsTrue (v.StartsWith ("\"") && v.EndsWith("\""), $"Field value is not a valid assembler string in '{file}:{lineNumber}': {v}");
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
			Assert.AreEqual (firstAppConfig.uses_embedded_dsos, secondAppConfig.uses_embedded_dsos, $"Field 'uses_embedded_dsos' has different value in environment file '{secondEnvFile}' than in environment file '{firstEnvFile}'");
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
			NdkUtil.Init (ndkDirectory);
			MonoAndroidHelper.AndroidSdk = new AndroidSdkInfo ((arg1, arg2) => {}, sdkDirectory, ndkDirectory);

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
				AssertSharedLibraryHasRequiredSymbols (envSharedLibrary, NdkUtil.GetNdkTool (ndkDirectory, arch, "readelf", 0));
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
			Assert.AreEqual (1, value.Length, $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid boolean value (too long)");

			uint fv;
			Assert.IsTrue (UInt32.TryParse (value, out fv), $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid boolean value (not a valid integer)");
			Assert.IsTrue (fv == 0 || fv == 1, $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid boolean value (not a valid boolean value 0 or 1)");

			return fv == 1;
		}

		static uint ConvertFieldToUInt32 (string fieldName, string envFile, int fileLine, string value)
		{
			Assert.IsTrue (value.Length > 0, $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid uint32_t value (not long enough)");

			uint fv;
			Assert.IsTrue (UInt32.TryParse (value, out fv), $"Field '{fieldName}' in {envFile}:{fileLine} is not a valid uint32_t value (not a valid integer)");

			return fv;
		}
	}
}
