using System;
using System.IO;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class GenerateMonoDroidIncludes : Task
	{
		[Required]
		public ITaskItem [] SourceFiles { get; set; }

		[Required]
		public ITaskItem [] DestinationFiles { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GenerateMonoDroidIncludes)}");

			if (SourceFiles.Length != DestinationFiles.Length) {
				Log.LogError ($"{nameof(SourceFiles)}.Length must equal {nameof(DestinationFiles)}.Length.");
				return false;
			}

			Log.LogMessage (MessageImportance.Low, $"\t{nameof(SourceFiles)} : ");
			for (int i = 0; i < SourceFiles.Length; i++) {
				var source = SourceFiles [i];
				var destination = DestinationFiles [i];
				Log.LogMessage (MessageImportance.Low, "\t\t{0} -> {1}", source.ItemSpec, destination.ItemSpec);

				var bytes = File.ReadAllBytes (source.ItemSpec);

				//Should be equivalent of "xxd -i %(_EmbeddedBlob.Config) | sed 's/^unsigned /static const unsigned /g' > jni/%(_EmbeddedBlob.Include)"
				using (var fs = File.Create (destination.ItemSpec))
				using (var writer = new StreamWriter(fs)) {
					var variableName = "monodroid_" + Path.GetFileNameWithoutExtension (source.ItemSpec).Replace ('.', '_');
					writer.Write ("static const unsigned char ");
					writer.Write (variableName);
					writer.Write ("[] = {");

					for (int j = 0; j < bytes.Length; j++) {
						if (j != 0)
							writer.Write (",");

						//12 per line
						if (j % 12 == 0) {
							writer.WriteLine ();
							writer.Write ("  ");
						} else {
							writer.Write (" ");
						}

						writer.Write ("0x");
						writer.Write (bytes [j].ToString ("x2", CultureInfo.InvariantCulture));
					}

					//Needs to be a null-terminating string and include a trailing \0
					writer.WriteLine (", 0x00");
					writer.WriteLine ("};");

					//Length
					writer.Write ("static const unsigned int ");
					writer.Write (variableName);
					writer.Write ("_len = ");
					writer.Write (bytes.Length + 1);
					writer.WriteLine (";");
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
