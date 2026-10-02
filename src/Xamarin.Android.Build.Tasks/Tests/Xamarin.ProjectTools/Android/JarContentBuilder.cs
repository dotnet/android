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
				Arguments = JavaSourceFileName,
				WorkingDirectory = BaseDirectory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			using (var javacPs = Process.Start (javacPsi) ?? throw new InvalidOperationException ("Failed to start `Javac`.")) {
				var stdout = javacPs.StandardOutput.ReadToEndAsync ();
				var stderr = javacPs.StandardError.ReadToEndAsync ();
				javacPs.WaitForExit ();
				string output = stdout.GetAwaiter ().GetResult ();
				string error = stderr.GetAwaiter ().GetResult ();
				if (javacPs.ExitCode != 0)
					throw new InvalidOperationException ("`Javac` command line tool did not successfully finish: " + error + Environment.NewLine + output);
			}
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
				Arguments = string.Join (" ", args.Concat (classes.Select (c => c.Substring (BaseDirectory.Length + 1)).ToArray ())),
				WorkingDirectory = BaseDirectory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			using (var jarPs = Process.Start (jarPsi) ?? throw new InvalidOperationException ("Failed to start `Jar`.")) {
				var stdout = jarPs.StandardOutput.ReadToEndAsync ();
				var stderr = jarPs.StandardError.ReadToEndAsync ();
				jarPs.WaitForExit ();
				string output = stdout.GetAwaiter ().GetResult ();
				string error = stderr.GetAwaiter ().GetResult ();
				if (jarPs.ExitCode != 0)
					throw new InvalidOperationException ("`Jar` command line tool did not successfully finish: " + error + Environment.NewLine + output);
			}
			return File.ReadAllBytes (jarfile);
		}
	}
}
