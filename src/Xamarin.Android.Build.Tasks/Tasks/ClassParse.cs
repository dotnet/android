// Copyright (C) 2012 Xamarin, Inc. All rights reserved.

using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class ClassParse : AndroidDotnetToolTask
	{
		public override string TaskPrefix => "CLP";

		[Required]
		public string OutputFile { get; set; }

		[Required]
		public ITaskItem[] SourceJars { get; set; }

		public ITaskItem [] DocumentationPaths { get; set; }

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = GetCommandLineBuilder ();

			var responseFile = Path.Combine (Path.GetDirectoryName (OutputFile), "class-parse.rsp");
			Log.LogDebugMessage ("[class-parse] response file: {0}", responseFile);

			using (var sw = new StreamWriter (responseFile, append: false, encoding: Files.UTF8withoutBOM)) {
				WriteLine (sw, $"--o=\"{OutputFile}\"");

				if (DocumentationPaths != null)
					foreach (var doc in DocumentationPaths)
						WriteLine (sw, $"--docspath=\"{doc}\"");

				foreach (var doc in SourceJars)
					WriteLine (sw, $"\"{doc}\"");
			}

			cmd.AppendSwitch ($"\"@{responseFile}\"");

			return cmd.ToString ();
		}

		void WriteLine (StreamWriter sw, string line)
		{
			sw.WriteLine (line);
			Log.LogDebugMessage (line);
		}
	}
}
