using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Provides static paths and directories used by the Xamarin.Android build system and test framework.
	/// Contains computed paths to build outputs, test directories, and other locations needed for
	/// building and testing Xamarin.Android projects.
	/// </summary>
	/// <remarks>
	/// This class discovers the Xamarin.Android repository structure and computes standard
	/// paths used throughout the build and test systems. It adapts to different build
	/// configurations (Debug/Release) and provides consistent path references.
	/// </remarks>
	/// <seealso cref="TestEnvironment"/>
	/// <seealso cref="XamarinProject.Root"/>
	public class XABuildPaths
	{
		#if DEBUG
		/// <summary>
		/// Gets the current build configuration, defaulting to Debug in debug builds.
		/// Can be overridden by the CONFIGURATION environment variable.
		/// </summary>
		public static string Configuration = Environment.GetEnvironmentVariable ("CONFIGURATION") ?? "Debug";
		#else
		/// <summary>
		/// Gets the current build configuration, defaulting to Release in release builds.
		/// Can be overridden by the CONFIGURATION environment variable.
		/// </summary>
		public static string Configuration = Environment.GetEnvironmentVariable ("CONFIGURATION") ?? "Release";
		#endif

		/// <summary>
		/// Gets the top-level directory of the Xamarin.Android repository.
		/// </summary>
		public static string TopDirectory = GetTopDirRecursive (Path.GetFullPath (
			Path.GetDirectoryName (typeof (XamarinProject).Assembly.Location)));

		/// <summary>
		/// Gets the prefix directory for build outputs (bin/{Configuration}).
		/// </summary>
		public static readonly string PrefixDirectory = Path.Combine (TopDirectory, "bin", Configuration);
		
		/// <summary>
		/// Gets the binary directory containing built executables and tools.
		/// </summary>
		public static readonly string BinDirectory = Path.Combine (PrefixDirectory, "bin");
		
		/// <summary>
		/// Gets the directory containing test assembly outputs.
		/// </summary>
		public static readonly string TestAssemblyOutputDirectory = Path.Combine (TopDirectory, "bin", $"Test{Configuration}");
		
		/// <summary>
		/// Gets the root directory for test project outputs and temporary files.
		/// </summary>
		/// <seealso cref="XamarinProject.Root"/>
		public static readonly string TestOutputDirectory = GetTestDirectoryRoot ();
		
		/// <summary>
		/// Gets the directory containing build outputs and intermediate files.
		/// </summary>
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
