using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	abstract class GeneratedFile : AppObject
	{
		public string InputPath { get; }
		public string OutputPath { get; protected set; }
		public bool EchoOutput { get; set; }
		public bool IsExecutable { get; set; }

		protected GeneratedFile (string outputPath)
		{
			InputPath = String.Empty;
			OutputPath = outputPath.Trim ();
			if (String.IsNullOrEmpty (OutputPath))
				throw new ArgumentException ("must not be null or empty", nameof (outputPath));
		}

		protected GeneratedFile (string inputPath, string outputPath)
			: this (outputPath)
		{
			InputPath = inputPath.Trim ();
			if (String.IsNullOrEmpty (InputPath))
				throw new ArgumentException ("must not be null or empty", nameof (inputPath));

			if (!File.Exists (InputPath))
				throw new InvalidOperationException ($"Input file {InputPath} must exist");
		}

		public abstract void Generate (Context context);

		protected void EnsureOutputDir ()
		{
			Utilities.CreateDirectory (Path.GetDirectoryName (OutputPath));
		}
	}

	sealed class SkipGeneratedFile : GeneratedFile {

		public SkipGeneratedFile ()
			: base (Path.Combine (BuildPaths.XAPrepareSourceDir, "shall-not-exist.txt"))
		{
		}

		public override void Generate (Context context)
		{
		}
	}
}
