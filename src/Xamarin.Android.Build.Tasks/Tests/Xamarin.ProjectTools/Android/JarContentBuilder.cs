using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public class JarContentBuilder : ContentBuilder
	{
		public string BaseDirectory { get; set; }
		public string JavacFullPath { get; set; }
		public string JarFullPath { get; set; }
		public string JarFileName { get; set; }
		// It can support more than one file but we don't need compllicated one yet.
		public string JavaSourceFileName { get; set; }
		public string JavaSourceText { get; set; }

		public string AdditionalFileExtensions { get; set; }

		public JarContentBuilder ()
		{
			Action<TraceLevel, string> logger = (level, value) => {
				switch (level) {
					case TraceLevel.Error:
						throw new Exception ($"AndroidSdkInfo {level}: {value}");
					default:
						Console.WriteLine ($"AndroidSdkInfo {level}: {value}");
						break;
				}
			};

			var jdkPath = AndroidSdkResolver.GetJavaSdkPath ();
			JavacFullPath = Path.Combine (jdkPath, "bin", "javac");
			JarFullPath = Path.Combine (jdkPath, "bin", "jar");
		}

		public override byte [] Build ()
		{
			var src = Path.Combine (BaseDirectory, JavaSourceFileName);
			var jarfile = Path.Combine (BaseDirectory, JarFileName);
			File.WriteAllText (src, JavaSourceText);
			// It can support additional arguments but we don't need compllicated one yet.
			var javacPsi = new ProcessStartInfo () {
				FileName = JavacFullPath,
				WorkingDirectory = BaseDirectory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				InheritedHandles = [],
			};
			javacPsi.ArgumentList.Add (JavaSourceFileName);
			var javacResult = Process.RunAndCaptureText (javacPsi, TimeSpan.FromMinutes (5));
			if (javacResult.ExitStatus.Canceled || javacResult.ExitStatus.ExitCode != 0)
				throw new InvalidOperationException ("`Javac` command line tool did not successfully finish: " +
					$"exit code {javacResult.ExitStatus.ExitCode}, canceled: {javacResult.ExitStatus.Canceled}{Environment.NewLine}" +
					javacResult.StandardError + Environment.NewLine + javacResult.StandardOutput);
			if (File.Exists (jarfile))
				File.Delete (jarfile);
			var args = new string [] { "cvf", JarFileName };
			var classes = Directory.GetFiles (Path.GetDirectoryName (src), "*.class", SearchOption.AllDirectories);
			if (!string.IsNullOrEmpty (AdditionalFileExtensions)) {
				var additionalFiles = Directory.GetFiles (Path.GetDirectoryName (BaseDirectory), AdditionalFileExtensions, SearchOption.AllDirectories);
				classes = classes.Concat (additionalFiles).ToArray ();
			}
			var jarPsi = new ProcessStartInfo () {
				FileName = JarFullPath,
				WorkingDirectory = BaseDirectory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				InheritedHandles = [],
			};
			foreach (var argument in args.Concat (classes.Select (c => Path.GetRelativePath (BaseDirectory, c))))
				jarPsi.ArgumentList.Add (argument);
			var jarResult = Process.RunAndCaptureText (jarPsi, TimeSpan.FromMinutes (5));
			if (jarResult.ExitStatus.Canceled || jarResult.ExitStatus.ExitCode != 0)
				throw new InvalidOperationException ("`Jar` command line tool did not successfully finish: " +
					$"exit code {jarResult.ExitStatus.ExitCode}, canceled: {jarResult.ExitStatus.Canceled}{Environment.NewLine}" +
					jarResult.StandardError + Environment.NewLine + jarResult.StandardOutput);
			return File.ReadAllBytes (jarfile);
		}
	}
}
