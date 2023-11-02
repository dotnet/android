using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_CopyExtraResultFilesForCI : Step
	{
		public Step_CopyExtraResultFilesForCI ()
			: base ("Copying extra result files to artifact directory")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			// Set when running on Azure Pipelines https://docs.microsoft.com/en-us/azure/devops/pipelines/build/variables
			var rootDir = Environment.GetEnvironmentVariable ("BUILD_STAGINGDIRECTORY");
			if (!Directory.Exists (rootDir))
				return false;

			await Task.Run (() => {
				CopyExtraTestFiles (Path.Combine (rootDir, $"Test{context.Configuration}"), context);
				CopyExtraBuildFiles (Path.Combine (rootDir, $"Build{context.Configuration}"), context);
			});
			return true;
		}

		string[] xaRootDirBuildFiles = {
			"Configuration.OperatingSystem.props",
			"Configuration.Override.props",
			"THIRD-PARTY-NOTICES.TXT",
			"config.log",
			"config.status",
			"config.h",
			"android-*.config.cache",
		};

		string [] buildConfigFiles = {
			"XABuildConfig.cs",
			"*.binlog",
			"prepare*log",
			"*.json",
			"*.mk",
			"*.projitems",
			"*.cmake",
			"*.targets",
			"CMakeCache.txt",
			".ninja_log",
			"clang-tidy*.log",
		};

		void CopyExtraBuildFiles (string destinationRoot, Context context)
		{
			Directory.CreateDirectory (destinationRoot);
			var filesToCopyPreserveRelative = new List<string> ();

			foreach (var fileMatch in xaRootDirBuildFiles) {
				filesToCopyPreserveRelative.AddRange (Directory.GetFiles (BuildPaths.XamarinAndroidSourceRoot, fileMatch, SearchOption.AllDirectories));
			}

			var cmakeFileDirs = Directory.GetDirectories (BuildPaths.XamarinAndroidSourceRoot, "CMakeFiles");
			foreach (var cmakeFileDir in cmakeFileDirs) {
				filesToCopyPreserveRelative.AddRange (Directory.GetFiles (cmakeFileDir, "*.log"));
			}

			var javaInteropBuildConfigDir = Path.Combine (context.Properties.GetRequiredValue (KnownProperties.JavaInteropFullPath), "bin", $"Build{context.Configuration}");
			if (Directory.Exists (javaInteropBuildConfigDir)) {
				filesToCopyPreserveRelative.AddRange (Directory.GetFiles (javaInteropBuildConfigDir, "*.props"));
			}

			filesToCopyPreserveRelative.AddRange (Directory.GetFiles (Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "src", "monodroid", "jni"), "*.include.*"));

			var buildConfigDir = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "bin", $"Build{context.Configuration}");
			if (Directory.Exists (buildConfigDir)) {
				foreach (var fileMatch in buildConfigFiles) {
					Utilities.CopyFilesSimple (Directory.GetFiles (buildConfigDir, fileMatch), destinationRoot, false);
				}
			}

			foreach (var file in filesToCopyPreserveRelative) {
				Utilities.CopyFile (file, file.Replace (BuildPaths.XamarinAndroidSourceRoot, destinationRoot), false);
			}
		}

		string [] testConfigFiles = {
			"*.apkdesc",
			"*.aabdesc",
			"logcat-*.txt",
			"*log",
			"TestOutput-*.txt",
			"Timing_*",
		};

		void CopyExtraTestFiles (string destinationRoot, Context context)
		{
			Directory.CreateDirectory (destinationRoot);

			var testConfigDir = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "bin", $"Test{context.Configuration}");
			if (Directory.Exists (testConfigDir)) {
				var matchedFiles = new List<string> ();
				foreach (var fileMatch in testConfigFiles) {
					// Handle files which might appear in multiple filters
					// eg logcat-Relase-full.log will appear in both logcat* AND *log
					foreach (var file in Directory.GetFiles (testConfigDir, fileMatch)) {
						if (matchedFiles.Contains (file))
							continue;
						matchedFiles.Add (file);
					}
				}
				Utilities.CopyFilesSimple (matchedFiles, destinationRoot, false);
			}

			var testConfigCompatDir = Path.Combine (testConfigDir, "compatibility");
			if (Directory.Exists (testConfigCompatDir)) {
				Utilities.CopyFilesSimple (Directory.GetFiles (testConfigCompatDir, "*"), Path.Combine (destinationRoot, "compatibility"));
			}

			var extraFilesToCopy = new List<string> ();
			extraFilesToCopy.AddRange (Directory.GetFiles (BuildPaths.XamarinAndroidSourceRoot, "TestResult*.xml"));
			extraFilesToCopy.AddRange (Directory.GetFiles (BuildPaths.XamarinAndroidSourceRoot, "*.csv"));
			extraFilesToCopy.AddRange (Directory.GetFiles (Path.GetTempPath (), "llc.exe-*"));
			if (extraFilesToCopy.Any ()) {
				Utilities.CopyFilesSimple (extraFilesToCopy, Path.Combine (destinationRoot, "test-extras"), false);
			}

			// Remove NuGet package directories, and any empty directories that may have been left behind before uploading
			var packagesDirs = Directory.EnumerateDirectories (destinationRoot, "packages", SearchOption.AllDirectories);
			foreach (var packagesDir in packagesDirs) {
				Utilities.DeleteDirectory (packagesDir, ignoreErrors: true);
			}

			DeleteEmptyDirectories (destinationRoot);

			void DeleteEmptyDirectories (string directory)
			{
				foreach (var dir in Directory.EnumerateDirectories (directory)) {
					DeleteEmptyDirectories (dir);

					if (!Directory.EnumerateFileSystemEntries (dir).Any ()) {
						Utilities.DeleteDirectory (dir, ignoreErrors: true, recurse: false);
					}
				}
			}
		}

	}
}
