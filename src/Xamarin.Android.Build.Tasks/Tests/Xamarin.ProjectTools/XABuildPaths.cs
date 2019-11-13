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
		public static readonly string XABuildScript = Path.Combine (BinDirectory, "xabuild");
		public static readonly string XABuildExe = Path.Combine (BinDirectory, "xabuild.exe");
		public static readonly string TestOutputDirectory = Path.Combine (TopDirectory, "bin", $"Test{Configuration}");

		static string GetTopDirRecursive (string searchDirectory, int maxSearchDepth = 5)
		{
			if (File.Exists (Path.Combine (searchDirectory, "Configuration.props")))
				return searchDirectory;

			if (maxSearchDepth <= 0)
				throw new DirectoryNotFoundException ("Unable to locate root xamarin-android directory!");

			return GetTopDirRecursive (Directory.GetParent (searchDirectory).FullName, --maxSearchDepth);
		}
	}
}
