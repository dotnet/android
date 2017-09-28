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

			var androidSdk = new AndroidSdkInfo (logger);
			JavacFullPath = Path.Combine (androidSdk.JavaSdkPath, "bin", "javac");
			JarFullPath = Path.Combine (androidSdk.JavaSdkPath, "bin", "jar");
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
			var javacPs = Process.Start (javacPsi);
			javacPs.WaitForExit ();
			if (javacPs.ExitCode != 0)
				throw new InvalidOperationException ("`Javac` command line tool did not successfully finish: " + javacPs.StandardError.ReadToEnd ());
			if (File.Exists (jarfile))
				File.Delete (jarfile);
			var args = new string [] { "cvf", JarFileName };
			var classes = Directory.GetFiles (Path.GetDirectoryName (src), "*.class", SearchOption.AllDirectories);
			var jarPsi = new ProcessStartInfo () {
				FileName = JarFullPath,
				Arguments = string.Join (" ", args.Concat (classes.Select (c => c.Substring (BaseDirectory.Length + 1)).ToArray ())),
				WorkingDirectory = BaseDirectory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			var jarPs = Process.Start (jarPsi);
			jarPs.WaitForExit ();
			if (jarPs.ExitCode != 0)
				throw new InvalidOperationException ("`Jar` command line tool did not successfully finish: " + jarPs.StandardError.ReadToEnd ());
			Process.Start (jarPsi).WaitForExit ();
			return File.ReadAllBytes (jarfile);
		}
	}
}
