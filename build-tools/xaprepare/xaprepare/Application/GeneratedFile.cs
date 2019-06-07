using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	abstract class GeneratedFile : AppObject
	{
		public string InputPath { get; }
		public string OutputPath { get; }
		public bool EchoOutput { get; set; }

		protected GeneratedFile (string outputPath)
		{
			OutputPath = outputPath?.Trim ();
			if (String.IsNullOrEmpty (OutputPath))
				throw new ArgumentException ("must not be null or empty", nameof (outputPath));
		}

		protected GeneratedFile (string inputPath, string outputPath)
			: this (outputPath)
		{
			InputPath = inputPath?.Trim ();
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
}
