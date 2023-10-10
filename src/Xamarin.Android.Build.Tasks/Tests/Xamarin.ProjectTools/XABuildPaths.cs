using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	public class XABuildPaths
	{
		#if DEBUG
		public static string Configuration = Environment.GetEnvironmentVariable ("CONFIGURATION") ?? "Debug";
		#else
		public static string Configuration = Environment.GetEnvironmentVariable ("CONFIGURATION") ?? "Release";
		#endif

		public static string TopDirectory = GetTopDirRecursive (Path.GetFullPath (
			Path.GetDirectoryName (new Uri (typeof (XamarinProject).Assembly.CodeBase).LocalPath)));

		public static readonly string PrefixDirectory = Path.Combine (TopDirectory, "bin", Configuration);
		public static readonly string BinDirectory = Path.Combine (PrefixDirectory, "bin");
		public static readonly string TestAssemblyOutputDirectory = Path.Combine (TopDirectory, "bin", $"Test{Configuration}");
		public static readonly string TestOutputDirectory = GetTestDirectoryRoot ();
		public static readonly string BuildOutputDirectory = Path.Combine (TopDirectory, "bin", $"Build{Configuration}");

		static string GetTopDirRecursive (string searchDirectory, int maxSearchDepth = 5)
		{
			if (File.Exists (Path.Combine (searchDirectory, "Configuration.props")))
				return searchDirectory;

			if (maxSearchDepth <= 0)
				throw new DirectoryNotFoundException ("Unable to locate root xamarin-android directory!");

			return GetTopDirRecursive (Directory.GetParent (searchDirectory).FullName, --maxSearchDepth);
		}

		static string _testOutputDirectory;
		static string GetTestDirectoryRoot ()
		{
			if (Directory.Exists (_testOutputDirectory))
				return _testOutputDirectory;

			// Set when running on Azure Pipelines https://docs.microsoft.com/en-us/azure/devops/pipelines/build/variables
			var rootDir = Environment.GetEnvironmentVariable ("BUILD_STAGINGDIRECTORY");
			if (!Directory.Exists (rootDir)) {
				_testOutputDirectory = TestAssemblyOutputDirectory;
			} else {
				var timeStamp = DateTime.UtcNow.ToString ("MM-dd_HH.mm.ss");
				_testOutputDirectory = Path.Combine (rootDir, $"Test{Configuration}", timeStamp);
			}

			Directory.CreateDirectory (_testOutputDirectory);
			return _testOutputDirectory;
		}

	}
}
