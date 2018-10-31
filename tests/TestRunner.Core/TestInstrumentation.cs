using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;

namespace Xamarin.Android.UnitTests
{
	public abstract class TestInstrumentation <TRunner> : Instrumentation where TRunner: TestRunner
	{
		protected sealed class KnownArguments
		{
			public const string LogLevel = "loglevel";
		}

		const string ResultExecutedTests = "run";
		const string ResultPassedTests = "passed";
		const string ResultSkippedTests = "skipped";
		const string ResultFailedTests = "failed";
		const string ResultInconclusiveTests = "inconclusive";
		const string ResultTotalTests = "total";
		const string ResultFilteredTests = "filtered";
		const string ResultResultsFilePath = "nunit2-results-path";
		const string ResultError = "error";

		Bundle arguments;

		protected abstract string LogTag { get; set; }
		protected string TestsDirectory { get; set; }
		protected IList<string> TestAssemblyDirectories { get; set; }
		protected bool GCAfterEachFixture { get; set; }
		protected LogWriter Logger { get; } = new LogWriter ();

		protected TestInstrumentation ()
		{}

		protected TestInstrumentation (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{}

		string FindTestAssembly (string name)
		{
			if (TestAssemblyDirectories == null || TestAssemblyDirectories.Count == 0)
				return null;

			AssemblyName aname = null;
			try {
				aname = new AssemblyName (name);
			} catch (Exception ex) {
				Log.Warn (LogTag, $"Failed to parse assembly name: {name}");
				Log.Warn (LogTag, ex.ToString ());
			}

			if (aname == null)
				return null;
			
			foreach (string dir in TestAssemblyDirectories) {
				if (String.IsNullOrEmpty (dir))
					continue;

				string path = Path.Combine (dir, aname.Name + ".dll");
				if (!File.Exists (path))
					continue;

				return path;
			}

			return null;
		}

		public override void OnCreate (Bundle arguments)
		{
			base.OnCreate (arguments);
			ProcessArguments (arguments);
			this.arguments = arguments;
			Start ();
		}

		protected virtual void ProcessArguments (Bundle arguments)
		{
			// Because of the way GetStringExtrasFromBundle below is implemented, we need to remove from the
			// bundle the arguments we know. GetStringExtrasFromBundle treats all entries as category
			// filters.
			if (arguments == null)
				return;

			if (arguments.ContainsKey (KnownArguments.LogLevel)) {
				Logger.SetMinimuLogLevelFromString (arguments.GetString (KnownArguments.LogLevel)?.Trim ());
				arguments.Remove (KnownArguments.LogLevel);
			}
		}

		public override void OnStart ()
		{
			base.OnStart ();

			bool success = false;
			Bundle results = null;

			LogDeviceInfo ();
			try {
				success = RunTests (ref results);
			} catch (Exception ex) {
				Log.Error (LogTag, $"Error: {ex}");
				results.PutString (ResultError, ex.ToString ());
				success = false;
			} finally {
				Finish (success ? Result.Ok : Result.Canceled, results);
			}
		}

		void LogPaddedInfo (string name, string value, int alignColumn)
		{
			int padding = alignColumn - (name.Length + 1);
			if (padding <= 0)
				padding = 0;

			Logger.OnInfo (LogTag, $"[{name}:{new String (' ', padding)}{value}]");
		}

		void LogDeviceInfo ()
		{
			int sdkInt;
#if __ANDROID_4__
			sdkInt = (int)Build.VERSION.SdkInt;
#else
			sdkInt = 1;
#endif
			Assembly mfa = typeof (Bundle).Assembly;
			var attribute = mfa.GetCustomAttribute <AssemblyInformationalVersionAttribute> ();
			string mfaVer = attribute == null ? "unknown" : attribute.InformationalVersion;

			int alignColumn = 25;
			LogPaddedInfo ("Xamarin.Android Version", mfaVer, alignColumn);

			var aver = new List<string> ();
			aver.Add (Build.VERSION.Release);
			aver.Add ($"API {(int)Build.VERSION.SdkInt} ({Build.VERSION.SdkInt})");
#if __ANDROID_23__
			if (sdkInt >= 23 && !String.IsNullOrEmpty (Build.VERSION.BaseOs))
				aver.Add (Build.VERSION.BaseOs);
#endif
#if __ANDROID_4__
			if (sdkInt >= 4 && !String.IsNullOrEmpty (Build.VERSION.Codename))
				aver.Add (Build.VERSION.Codename);
#endif
			if (!String.IsNullOrEmpty (Build.VERSION.Incremental))
				aver.Add ($"Incremental: {Build.VERSION.Incremental}");
#if __ANDROID_23__
			if (sdkInt >= 23 && !String.IsNullOrEmpty (Build.VERSION.SecurityPatch))
				aver.Add ($"Security patch: {Build.VERSION.SecurityPatch}");
#endif
			LogPaddedInfo ("Android version", String.Join ("; ", aver), alignColumn);
			LogPaddedInfo ("Board", Build.Board, alignColumn);

#if __ANDROID_8__
			if (sdkInt >= 8) {
				LogPaddedInfo ("Bootloader", Build.Bootloader, alignColumn);
            }
#endif
			LogPaddedInfo ("Brand", Build.Brand, alignColumn);

			string cpuAbi = Build.CpuAbi;
#if __ANDROID_8__
			if (sdkInt >= 8) {
				cpuAbi += $" {Build.CpuAbi2}";
            }
#endif
			LogPaddedInfo ("CpuAbi", cpuAbi, alignColumn);
#if __ANDROID_21__
			if (sdkInt >= 21) {
				string supported;
				if (Build.SupportedAbis?.Count > 0) {
					supported = String.Join (", ", Build.SupportedAbis);
					LogPaddedInfo ("Supported ABIs", supported, alignColumn);
				}

				if (Build.Supported32BitAbis?.Count > 0) {
					supported = String.Join (", ", Build.Supported32BitAbis);
					LogPaddedInfo ("Supported 32-bit ABIs", supported, alignColumn);
				}

				if (Build.Supported64BitAbis?.Count > 0) {
					supported = String.Join (", ", Build.Supported64BitAbis);
					LogPaddedInfo ("Supported 64-bit ABIs", supported, alignColumn);
				}
			}
#endif
			LogPaddedInfo ("Device", Build.Device, alignColumn);
			LogPaddedInfo ("Display", Build.Display, alignColumn);
			LogPaddedInfo ("Fingerprint", Build.Fingerprint, alignColumn);
#if __ANDROID_8__
			if (sdkInt >= 8) {
				LogPaddedInfo ("Hardware", Build.Hardware, alignColumn);
			}
#endif
			LogPaddedInfo ("Host", Build.Host, alignColumn);
			LogPaddedInfo ("Id", Build.Id, alignColumn);
			LogPaddedInfo ("Manufacturer", Build.Manufacturer, alignColumn);
			LogPaddedInfo ("Model", Build.Model, alignColumn);
			LogPaddedInfo ("Product", Build.Product, alignColumn);
#if __ANDROID_9__
			if (sdkInt >= 8) {
#if __ANDROID_26__
				// .Serial was deprecated in API26, .GetSerial () is the recommended replacement
				if (((int) Build.VERSION.SdkInt) >= 26)
					LogPaddedInfo ("Serial", Build.GetSerial (), alignColumn);		
				else
#endif
					LogPaddedInfo ("Serial", Build.Serial, alignColumn);
			}
#endif

#if __ANDROID_8__
			if (sdkInt >= 8) {
#if __ANDROID_14__
				// .Radio was deprecated in API14, RadioVersion is the recommended replacement
				if (((int) Build.VERSION.SdkInt) >= 14)
					LogPaddedInfo ("Radio", Build.RadioVersion, alignColumn);
				else
#endif
					LogPaddedInfo ("Radio", Build.Radio, alignColumn);
			}
#endif

			LogPaddedInfo ("Tags", Build.Tags, alignColumn);
			LogPaddedInfo ("Time", Build.Time.ToString (), alignColumn);
			LogPaddedInfo ("Type", Build.Type, alignColumn);
			LogPaddedInfo ("User", Build.User, alignColumn);
			LogPaddedInfo ("VERSION.Codename:", Build.VERSION.Codename, alignColumn);
			LogPaddedInfo ("VERSION.Incremental", Build.VERSION.Incremental, alignColumn);
			LogPaddedInfo ("VERSION.Release", Build.VERSION.Release, alignColumn);
			LogPaddedInfo ("VERSION.Sdk", Build.VERSION.Sdk, alignColumn);
			LogPaddedInfo ("VERSION.SdkInt", Build.VERSION.SdkInt.ToString (), alignColumn);
			LogPaddedInfo ("Device Date/Time", DateTime.UtcNow.ToString (), alignColumn); 

			// FIXME: add data about how the app was compiled (e.g. ARMvX, LLVM, Linker options)
		}

		bool RunTests (ref Bundle results)
		{
			IList<TestAssemblyInfo> assemblies = GetTestAssemblies ();
			if (assemblies == null || assemblies.Count == 0) {
				Log.Info (LogTag, "No test assemblies loaded");
				return false;
			}

			Log.Info (LogTag, "Test assemblies:");
			foreach (var asm in assemblies)
				Log.Info (LogTag, $"\t{asm.FullPath}");

			TRunner runner = CreateRunner (Logger, arguments);
			runner.LogTag = LogTag;
			ConfigureFilters (runner);

			Log.Info (LogTag, "Starting unit tests");
			runner.Run (assemblies);
			Log.Info (LogTag, "Unit tests completed");

			string resultsFilePath = runner.WriteResultsToFile ();
			results = new Bundle ();

			/*
			if (runner.FailureInfos?.Count > 0) {
				foreach (TestFailureInfo info in runner.FailureInfos) {
					if (info == null || !info.HasInfo)
						continue;

					results.PutString ($"failure: {info.TestName}", info.Message);
				}
			}*/

			results.PutLong (ResultExecutedTests, runner.ExecutedTests);
			results.PutLong (ResultPassedTests, runner.PassedTests);
			results.PutLong (ResultSkippedTests, runner.SkippedTests);
			results.PutLong (ResultFailedTests, runner.FailedTests);
			results.PutLong (ResultInconclusiveTests, runner.InconclusiveTests);
			results.PutLong (ResultTotalTests, runner.TotalTests);
			results.PutLong (ResultFilteredTests, runner.FilteredTests);
			results.PutString (ResultResultsFilePath, ToAdbPath (resultsFilePath));

			Log.Info (LogTag, $"Passed: {runner.PassedTests}, Failed: {runner.FailedTests}, Skipped: {runner.SkippedTests}, Inconclusive: {runner.InconclusiveTests}, Total: {runner.TotalTests}, Filtered: {runner.FilteredTests}");

			return runner.FailedTests == 0;
		}

		protected abstract TRunner CreateRunner (LogWriter logger, Bundle bundle);

		protected virtual IList<TestAssemblyInfo> GetTestAssemblies ()
		{
			var ret = new List<TestAssemblyInfo> ();

			if (TestAssemblyDirectories != null && TestAssemblyDirectories.Count > 0) {
				foreach (string adir in TestAssemblyDirectories)
					GetTestAssembliesFromDirectory (adir, ret);
			}

			return ret;
		}

		protected virtual void GetTestAssembliesFromDirectory (string directoryPath, IList<TestAssemblyInfo> assemblies)
		{
			if (String.IsNullOrEmpty (directoryPath))
				throw new ArgumentException ("must not be null or empty", nameof (directoryPath));

			if (assemblies == null)
				throw new ArgumentNullException (nameof (assemblies));

			foreach (string file in Directory.EnumerateFiles (directoryPath, "*.dll", SearchOption.AllDirectories)) {
				Log.Info (LogTag, $"Adding test assembly: {file}");
				Assembly asm;
				Exception ex = null;
				try {
					asm = LoadTestAssembly (file);
				} catch (Exception e) {
					asm = null;
					ex = e;
				}

				if (asm == null) {
					if (ex == null)
						continue;
					throw new InvalidOperationException ($"Unable to load test assembly: {file}", ex);
				}

				// We store full path since Assembly.Location is not reliable on Android - it may hold a relative
				// path or no path at all
				assemblies.Add (new TestAssemblyInfo (asm, file));
			}
		}

		protected virtual Assembly LoadTestAssembly (string filePath)
		{
			return Assembly.LoadFrom (filePath);
		}

		protected Dictionary<string, string> GetStringExtrasFromBundle ()
		{
			var filtersFromBundle = new Dictionary<string, string> ();
			foreach (var key in arguments.KeySet ()) {
				string value = arguments.GetString (key);
				if (!string.IsNullOrEmpty (value))
					filtersFromBundle.Add (key, value);
			}
			return filtersFromBundle;
		}

		protected virtual void ConfigureFilters (TRunner runner)
		{}

		protected virtual void ExtractAssemblies (string targetDir, Stream zipStream)
		{
			TestAssemblyDirectories = new List<string> {
				targetDir
			};

			if (Directory.Exists (targetDir)) {
				foreach (string fi in Directory.EnumerateFiles (targetDir, "*", SearchOption.AllDirectories)) {
					File.Delete (fi);
				}
			} else
				Directory.CreateDirectory (targetDir);

			Log.Info (LogTag, $"Extracting test assemblies to {targetDir}");
			using (var zip = new ZipArchive (zipStream, ZipArchiveMode.Read)) {
				zip.ExtractToDirectory (targetDir);
			}

			Log.Info (LogTag, "Extracted assemblies:");
			foreach (string fi in Directory.EnumerateFiles (targetDir, "*.dll", SearchOption.AllDirectories)) {
				Log.Info (LogTag, $"  {fi}");
			}
		}

		protected HashSet<string> LoadExcludedTests (TextReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException (nameof (reader));

			HashSet<string> excludedTestNames = null;
			do {
				string line = reader.ReadLine ()?.Trim ();
				if (line == null)
					return excludedTestNames;
				
				if (line.Length == 0 || line.StartsWith ("#", StringComparison.Ordinal))
					continue;

				if (excludedTestNames == null)
					excludedTestNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
				if (excludedTestNames.Contains (line))
					continue;

				excludedTestNames.Add (line);
			} while (true);
		}

		// On some Android targets, the external storage directory is "emulated",
		// in which case the paths used on-device by the application are *not*
		// paths that can be used off-device with `adb pull`.
		// For example, `Contxt.GetExternalFilesDir()` may return `/storage/emulated/foo`,
		// but `adb pull /storage/emulated/foo` will *fail*; instead, we may need
		// `adb pull /mnt/shell/emulated/foo`.
		// The `$EMULATED_STORAGE_SOURCE` and `$EMULATED_STORAGE_TARGET` environment
		// variables control the "on-device" (`$EMULATED_STORAGE_TARGET`) and
		// "off-device" (`$EMULATED_STORAGE_SOURCE`) directory prefixes
		string ToAdbPath (string path)
		{
			string source = global::System.Environment.GetEnvironmentVariable ("EMULATED_STORAGE_SOURCE")?.Trim ();
			string target = global::System.Environment.GetEnvironmentVariable ("EMULATED_STORAGE_TARGET")?.Trim ();

			if (!String.IsNullOrEmpty (source) && !String.IsNullOrEmpty (target) && path.StartsWith (target, StringComparison.Ordinal)) {
				return path.Replace (target, source);
			}

			return path;
		}
	}
}
