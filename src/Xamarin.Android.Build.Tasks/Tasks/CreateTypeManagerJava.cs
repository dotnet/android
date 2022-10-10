using System.IO;
using System.Reflection;
using System.Text;
using System;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class CreateTypeManagerJava : AndroidTask
	{
		public override string TaskPrefix => "CTMJ";

		[Required]
		public string ResourceName { get; set; }

		[Required]
		public string OutputFilePath { get; set; }

		static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly ();

		public override bool RunTask ()
		{
			string? content = ReadResource (ResourceName);

			if (String.IsNullOrEmpty (content)) {
				return false;
			}

			var result = new StringBuilder ();
			bool ignoring = false;
			foreach (string line in content.Split ('\n')) {
				if (!ignoring) {
					if (ignoring = line.StartsWith ("//#MARSHAL_METHODS:START", StringComparison.Ordinal)) {
						continue;
					}
					result.AppendLine (line);
				} else if (line.StartsWith ("//#MARSHAL_METHODS:END", StringComparison.Ordinal)) {
					ignoring = false;
				}
			}

			if (result.Length == 0) {
				Log.LogDebugMessage ("TypeManager.java not generated, empty resource data");
				return false;
			}

			using (var ms = new MemoryStream ()) {
				using (var sw = new StreamWriter (ms)) {
					sw.Write (result.ToString ());
					sw.Flush ();

					if (Files.CopyIfStreamChanged (ms, OutputFilePath)) {
						Log.LogDebugMessage ($"Wrote resource {OutputFilePath}.");
					} else {
						Log.LogDebugMessage ($"Resource {OutputFilePath} is unchanged. Skipping.");
					}
				}
			}

			return !Log.HasLoggedErrors;
		}

		string? ReadResource (string resourceName)
		{
			using (var from = ExecutingAssembly.GetManifestResourceStream (resourceName)) {
				if (from == null) {
					Log.LogCodedError ("XA0116", Properties.Resources.XA0116, resourceName);
					return null;
				}

				using (var sr = new StreamReader (from)) {
					return sr.ReadToEnd ();
				}
			}
		}
	}
}
