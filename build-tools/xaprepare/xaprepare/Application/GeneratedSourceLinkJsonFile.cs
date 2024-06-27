using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Prepare
{
	class GeneratedSourceLinkJsonFile : GeneratedFile
	{
		IEnumerable<GitSubmoduleInfo> submodules;
		string xaCommit;

		public GeneratedSourceLinkJsonFile (IEnumerable<GitSubmoduleInfo> submodules, string xaCommit, string outputPath)
			: base (outputPath)
		{
			this.submodules = submodules ?? throw new ArgumentNullException (nameof (submodules));
			this.xaCommit   = !string.IsNullOrEmpty (xaCommit) ? xaCommit : throw new ArgumentNullException (nameof (xaCommit));
		}

		public override void Generate (Context context)
		{
			var json    = new StringBuilder ();
			json.AppendLine ("{");
			json.AppendLine ("  \"documents\": {");

			foreach (var submodule in submodules.OrderBy (s => s.Name)) {
				var localPath   = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, submodule.LocalPath);

				var contentUri  = new UriBuilder (submodule.RepositoryUrl);
				contentUri.Host = "raw.githubusercontent.com";
				contentUri.Path += $"/{submodule.CommitHash}";

				json.AppendLine ($"    \"{localPath}/*\": \"{contentUri.Uri}/*\",");
			}
			json.AppendLine ($"    \"{BuildPaths.XamarinAndroidSourceRoot}/*\": \"https://raw.githubusercontent.com/dotnet/android/{xaCommit}/*\"");
			json.AppendLine ("  }");
			json.AppendLine ("}");

			EnsureOutputDir ();
			string outputData = json.ToString ();
			File.WriteAllText (OutputPath, outputData, Utilities.UTF8NoBOM);

			if (!EchoOutput)
				return;

			Log.DebugLine ();
			Log.DebugLine ("--------------------------------------------");
			Log.DebugLine (outputData);
			Log.DebugLine ("--------------------------------------------");
			Log.DebugLine ();
		}
	}
}
