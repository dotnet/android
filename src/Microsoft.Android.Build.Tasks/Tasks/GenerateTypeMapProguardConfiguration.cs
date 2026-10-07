using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

public class GenerateTypeMapProguardConfiguration : AndroidTask
{
	public override string TaskPrefix => "GTMPC";

	[Required]
	public ITaskItem [] TypeMapKeyFiles { get; set; } = [];

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		var classes = new SortedSet<string> (StringComparer.Ordinal);
		string currentFile = "";
		try {
			if (TypeMapKeyFiles.Length == 0) {
				Log.LogCodedError ("XA4328", Properties.Resources.XA4328, OutputFile, Properties.Resources.XA4328_NoInputs);
				return !Log.HasLoggedErrors;
			}

			foreach (var file in TypeMapKeyFiles) {
				currentFile = file.ItemSpec;
				using var reader = new StreamReader (currentFile, new UTF8Encoding (false, true), detectEncodingFromByteOrderMarks: false);
				string? name;
				int lineNumber = 0;
				while ((name = reader.ReadLine ()) != null) {
					lineNumber++;
					if (string.IsNullOrWhiteSpace (name)) {
						continue;
					}
					if (!IsClassName (name)) {
						Log.LogCodedError ("XA4328", Properties.Resources.XA4328, currentFile,
							string.Format (CultureInfo.CurrentCulture, Properties.Resources.XA4328_InvalidName, lineNumber, name));
						return !Log.HasLoggedErrors;
					}
					classes.Add (name.Replace ('/', '.'));
				}
			}

			currentFile = OutputFile;
			var directory = Path.GetDirectoryName (OutputFile);
			if (directory != null && directory.Length > 0) {
				Directory.CreateDirectory (directory);
			}
			string temporaryOutputFile = OutputFile + "." + Guid.NewGuid ().ToString ("N") + ".tmp";
			try {
				using (var writer = new StreamWriter (temporaryOutputFile, append: false, new UTF8Encoding (false)) { NewLine = "\n" }) {
					foreach (var name in classes) {
						WriteClassRule (writer, name);
					}
				}
				File.Move (temporaryOutputFile, OutputFile, overwrite: true);
			} finally {
				File.Delete (temporaryOutputFile);
			}
		} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DecoderFallbackException ||
				ex is ArgumentException || ex is NotSupportedException) {
			Log.LogCodedError ("XA4328", Properties.Resources.XA4328, currentFile, ex.Message);
			return !Log.HasLoggedErrors;
		}
		return !Log.HasLoggedErrors;
	}

	protected virtual void WriteClassRule (TextWriter writer, string name)
	{
		writer.WriteLine ($"-keep class {name}");
		writer.WriteLine ($"-keep interface {name}");
	}

	internal static bool IsClassName (string name)
		=> TypeMapClassName.IsClassName (name);
}
