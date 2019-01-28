using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.XPath;

using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

using XABuildPaths = global::Xamarin.Android.Build.Paths;

namespace Xamarin.Android.MakeBundle.UnitTests
{
	sealed class LocalBuilder : Builder
	{
		public LocalBuilder ()
		{
			BuildingInsideVisualStudio = false;
		}

		public bool Build (string projectOrSolution, string target, string[] parameters = null, Dictionary<string, string> environmentVariables = null)
		{
			return BuildInternal (projectOrSolution, target, parameters, environmentVariables);
		}
	}

	[Parallelizable (ParallelScope.Children)]
	public class BuildTests_EmbeddedDSOBuildTests
	{
		const string ProjectName = "Xamarin.Android.MakeBundle-Tests";
		const string ProjectAssemblyName = "Xamarin.Android.MakeBundle-Tests";
		const string ProjectPackageName = "Xamarin.Android.MakeBundle_Tests";

		static readonly char[] ElfDynamicFieldSep = new [] { ' ', '\t' };
		static readonly string TestProjectRootDirectory;
		static readonly string TestOutputDir;

		static readonly List <string> produced_binaries = new List <string> {
			$"{ProjectAssemblyName}.dll",
			$"{ProjectPackageName}-Signed.apk",
			$"{ProjectPackageName}.apk",
		};

		static readonly List <string> log_files = new List <string> {
			"process.log",
			"msbuild.binlog",
		};

		static readonly string[] bundles = new [] {
			"lib/x86/libmonodroid_bundle_app.so",
			"lib/armeabi-v7a/libmonodroid_bundle_app.so",
		};

		string elfReaderPath;
		bool elfReaderLlvm;
		string testProjectPath;
		string apk;

		static BuildTests_EmbeddedDSOBuildTests ()
		{
			TestProjectRootDirectory = Path.GetFullPath (Path.Combine (XABuildPaths.TopDirectory, "tests", "CodeGen-MkBundle", "Xamarin.Android.MakeBundle-Tests"));
			TestOutputDir = Path.Combine (XABuildPaths.TestOutputDirectory, "CodeGen-MkBundle");
		}

		[TestFixtureSetUp]
		public void BuildProject ()
		{
			if (File.Exists (Config.LlvmReadobj)) {
				elfReaderPath = Config.LlvmReadobj;
				elfReaderLlvm = true;
			} else if (File.Exists (Config.GccReadelf)) {
				elfReaderPath = Config.GccReadelf;
				elfReaderLlvm = false;
			} else
				Assert.Fail ($"No ELF reader found. Tried '{Config.LlvmReadobj}' and '{Config.GccReadelf}'");

			Console.WriteLine ($"Will use the following ELF reader: {elfReaderPath}");

			testProjectPath = PrepareProject (ProjectName);
			apk = Path.Combine (testProjectPath, "bin", XABuildPaths.Configuration, $"{ProjectPackageName}-Signed.apk");
			string projectPath = Path.Combine (testProjectPath, $"{ProjectName}.csproj");
			LocalBuilder builder = GetBuilder ("Xamarin.Android.MakeBundle-Tests");
			bool success = builder.Build (projectPath, "SignAndroidPackage", new [] { "UnitTestsMode=true" });

			Assert.That (success, Is.True, "Should have been built");
		}

		[Test]
		public void BinariesExist ()
		{
			foreach (string binary in produced_binaries) {
				string fp = Path.Combine (testProjectPath, "bin", XABuildPaths.Configuration, binary);

				Assert.That (new FileInfo (fp), Does.Exist, $"File {fp} should exist");
			}
		}

		[Test]
		public void PackageIsBundled ()
		{
			Assert.That (new FileInfo (apk), Does.Exist, $"File {apk} should exist");

			using (ZipArchive zip = ZipArchive.Open (apk, FileMode.Open)) {
				Assert.That (zip, Is.Not.Null, $"{apk} couldn't be opened as a zip archive");

				foreach (string bundle in bundles) {
					Assert.That (zip.ContainsEntry (bundle), Is.True, $"`{bundle}` file not found in {apk}");
				}
			}
		}

		[Test]
		public void BundleHasSoname ()
		{
			Assert.That (new FileInfo (apk), Does.Exist, $"File {apk} should exist");

			using (ZipArchive zip = ZipArchive.Open (apk, FileMode.Open)) {
				Assert.That (zip, Is.Not.Null, $"{apk} couldn't be opened as a zip archive");

				foreach (string bundle in bundles) {
					Assert.That (zip.ContainsEntry (bundle), Is.True, $"`{bundle}` file not found in {apk}");
					CheckBundleForSoname (zip, bundle);
				}
			}
		}

		void CheckBundleForSoname (ZipArchive zip, string bundlePath)
		{
			string tempFile = Path.GetTempFileName ();
			using (Stream fs = File.Open (tempFile, FileMode.Create)) {
				ZipEntry entry = zip.FirstOrDefault (e => String.Compare (e.FullName, bundlePath, StringComparison.Ordinal) == 0);
				Assert.That (entry, Is.Not.Null, $"Unable to open the `{bundlePath}` entry in {apk}");

				entry.Extract (fs);
			}

			var stdout = new List<string> ();
			string arguments;
			string sonameField;

			if (elfReaderLlvm) {
				arguments = "-dynamic-table";
				sonameField = "SONAME";
			} else {
				arguments = "-d";
				sonameField = "(SONAME)";
			}
			bool success;

			try {
				Console.WriteLine ($"Checking bundle {bundlePath} in {tempFile}");
				success = RunCommand (elfReaderPath, $"{arguments} \"{tempFile}\"", stdout);
			} finally {
				File.Delete (tempFile);
			}

			Assert.That (success, Is.True, $"Command {elfReaderPath} failed");

			string soname = null;
			foreach (string l in stdout) {
				string line = l?.Trim ();
				if (String.IsNullOrEmpty (line))
					continue;

				string[] parts = line.Split (ElfDynamicFieldSep, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 3)
					continue;

				if (String.Compare (sonameField, parts [1], StringComparison.Ordinal) != 0)
					continue;

				if (parts.Length > 3)
					soname = String.Join (" ", parts, 3, parts.Length - 3);
				else
					soname = String.Empty;
				break;
			}

			const string expectedSoname = "libmonodroid_bundle_app.so";
			Assert.That (soname, Is.Not.Null, $"Bundle {bundlePath} doesn't have DT_SONAME in ELF header");
			Assert.That (soname.Length, Is.GreaterThanOrEqualTo (0), $"Unknown DT_SONAME field format in {bundlePath}");
			Assert.That (soname.IndexOf (expectedSoname, StringComparison.Ordinal), Is.GreaterThanOrEqualTo (0), $"Unexpected bundle {bundlePath} SONAME (expected {expectedSoname}");
		}

		bool RunCommand (string command, string arguments, List<string> stdout)
		{
			var psi = new ProcessStartInfo () {
				FileName		= command,
				Arguments		= arguments,
				UseShellExecute		= false,
				RedirectStandardInput	= false,
				RedirectStandardOutput	= true,
				RedirectStandardError	= true,
				CreateNoWindow		= true,
				WindowStyle		= ProcessWindowStyle.Hidden,
			};

			var stderr_completed = new ManualResetEvent (false);
			var stdout_completed = new ManualResetEvent (false);

			var p = new Process () {
				StartInfo   = psi,
			};

			p.ErrorDataReceived += (sender, e) => {
				if (e.Data == null)
					stderr_completed.Set ();
				else
					Console.WriteLine ($"stderr: {e.Data}");
			};

			p.OutputDataReceived += (sender, e) => {
				if (e.Data == null)
					stdout_completed.Set ();
				else {
					stdout.Add (e.Data);
					Console.WriteLine ($"stdout: {e.Data}");
				}
			};

			using (p) {
				p.StartInfo = psi;
				p.Start ();
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();

				bool success = p.WaitForExit (60000);

				// We need to call the parameter-less WaitForExit only if any of the standard
				// streams have been redirected (see
				// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.7.2#System_Diagnostics_Process_WaitForExit)
				//
				p.WaitForExit ();
				stderr_completed.WaitOne (TimeSpan.FromSeconds (60));
				stdout_completed.WaitOne (TimeSpan.FromSeconds (60));

				if (!success || p.ExitCode != 0) {
					Console.Error.WriteLine ($"Process `{command} {arguments}` exited with value {p.ExitCode}.");
					return false;
				}

				return true;
			}
		}

		string PrepareProject (string testName)
		{
			string tempRoot = Path.Combine (TestOutputDir, $"{testName}.build", XABuildPaths.Configuration);
			string temporaryProjectPath = Path.Combine (tempRoot, "project");

			var ignore = new HashSet <string> {
				Path.Combine (TestProjectRootDirectory, "bin"),
				Path.Combine (TestProjectRootDirectory, "obj"),
			};

			CopyRecursively (TestProjectRootDirectory, temporaryProjectPath, ignore);
			return temporaryProjectPath;
		}

		void CopyRecursively (string fromDir, string toDir, HashSet <string> ignoreDirs)
		{
			if (String.IsNullOrEmpty (fromDir))
				throw new ArgumentException ($"{nameof (fromDir)} is must have a non-empty value");
			if (String.IsNullOrEmpty (toDir))
				throw new ArgumentException ($"{nameof (toDir)} is must have a non-empty value");

			if (ignoreDirs.Contains (fromDir))
				return;

			var fdi = new DirectoryInfo (fromDir);
			if (!fdi.Exists)
				throw new InvalidOperationException ($"Source directory '{fromDir}' does not exist");

			if (Directory.Exists (toDir))
				Directory.Delete (toDir, true);

			foreach (FileSystemInfo fsi in fdi.EnumerateFileSystemInfos ("*", SearchOption.TopDirectoryOnly)) {
				if (fsi is FileInfo finfo)
					CopyFile (fsi.FullName, Path.Combine (toDir, finfo.Name));
				else
					CopyRecursively (fsi.FullName, Path.Combine (toDir, fsi.Name), ignoreDirs);
			}
		}

		void CopyFile (string from, string to)
		{
			string dir = Path.GetDirectoryName (to);
			if (!Directory.Exists (dir))
				Directory.CreateDirectory (dir);
			File.Copy (from, to, true);
		}

		LocalBuilder GetBuilder (string baseLogFileName)
		{
			return new LocalBuilder {
				Verbosity = LoggerVerbosity.Diagnostic,
				BuildLogFile = $"{baseLogFileName}.log"
			};
		}
	}
}
