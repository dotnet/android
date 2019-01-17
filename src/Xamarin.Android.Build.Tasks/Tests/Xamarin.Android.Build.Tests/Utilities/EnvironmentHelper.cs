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
		static readonly object ndkInitLock = new object ();
		static readonly char[] readElfFieldSeparator = new [] { ' ', '\t' };
		static readonly Regex stringLabelRegex = new Regex ("^\\.L\\.str\\.[0-9]+:", RegexOptions.Compiled);

		static readonly HashSet <string> expectedPointerTypes = new HashSet <string> (StringComparer.Ordinal) {
			".long",
			".quad",
			".xword"
		};

		static readonly string[] requiredDSOSymbols = {
			"app_environment_variables",
			"app_system_properties",
			"application_config",
			"jm_typemap",
			"jm_typemap_header",
			"mj_typemap",
			"mj_typemap_header",
			"mono_aot_mode_name",
		};

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
				string[] field;
				string line = lines [i];
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

		public static void AssertValidEnvironmentDSO (string outputDirectoryRoot, string sdkDirectory, string ndkDirectory, string supportedAbis)
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

				string envDSO = Path.Combine (outputDirectoryRoot, "app_dsos", abi, "libxamarin-app.so");
				Assert.IsTrue (File.Exists (envDSO), $"Application environment DSO '{envDSO}' must exist");

				// API level doesn't matter in this case
				AssertDSOHasRequiredSymbols (envDSO, NdkUtil.GetNdkTool (ndkDirectory, arch, "readelf", 0));
			}
		}

		static void AssertDSOHasRequiredSymbols (string dsoPath, string readElfPath)
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
					Assert.Fail ($"Failed to validate application environment DSO '{dsoPath}'");
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

			foreach (string symbol in requiredDSOSymbols) {
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
	}
}
