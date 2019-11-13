using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.Android.Prepare
{
	class GeneratedPlaceholdersFile : GeneratedFile
	{
		IDictionary<string, string> replacements;

		public GeneratedPlaceholdersFile (IDictionary<string, string> replacements, string inputPath, string outputPath)
			: base (inputPath, outputPath)
		{
			if (replacements == null)
				throw new ArgumentNullException (nameof (replacements));
			this.replacements = replacements;
		}

		public override void Generate (Context context)
		{
			var inputData = new StringBuilder (File.ReadAllText (InputPath, Encoding.UTF8));

			foreach (var kvp in replacements) {
				string placeholder = kvp.Key?.Trim ();
				if (String.IsNullOrEmpty (placeholder))
					continue;

				inputData.Replace (placeholder, kvp.Value ?? String.Empty);
			}

			EnsureOutputDir ();
			string outputData = inputData.ToString ();
			File.WriteAllText (OutputPath, outputData, Encoding.UTF8);

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
