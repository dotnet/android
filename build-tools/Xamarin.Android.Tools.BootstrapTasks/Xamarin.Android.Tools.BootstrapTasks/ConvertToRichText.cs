using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	/// <summary>
	/// Used for converting plain text license files to .rtf format.
	/// Assumes the file is line wrapped with Environment.NewLine:
	/// * Double new lines are preserved.
	/// * Single new lines are replaced with spaces.
	///
	/// For a Unicode escape the control word \u is used, followed by
	/// a 16-bit signed decimal integer giving the Unicode UTF-16 code
	/// unit number. More information under 'Character encoding' here:
	/// https://en.wikipedia.org/wiki/Rich_Text_Format
	/// </summary>
	public class ConvertToRichText : Task
	{
		[Required]
		public string SourceFile { get; set; }

		[Required]
		public string DestinationFile { get; set; }

		public override bool Execute ()
		{
			var text = File.ReadAllText (SourceFile);

			text = text
				.Replace (@"\", @"\\")
				.Replace ("{", @"\{")
				.Replace ("}", @"\}")
				// Only want to keep "double" new lines
				.Replace (Environment.NewLine + Environment.NewLine, $@"\par{Environment.NewLine} \par{Environment.NewLine} ")
				.Replace (Environment.NewLine, " ");

			Directory.CreateDirectory (Path.GetDirectoryName (DestinationFile));
			using (var writer = File.CreateText (DestinationFile)) {
				writer.Write (@"{\rtf1\ansi\ansicpg1250\deff0{\fonttbl\f0\fcharset0 Courier New;}\f0\pard ");
				foreach (char letter in text) {
					if (letter <= 0x7f) {
						writer.Write (letter);
					} else {
						writer.Write ("\\u");
						writer.Write (Convert.ToUInt32 (letter));
						writer.Write ("?");
					}
				}
				writer.Write (" } ");
			}

			return !Log.HasLoggedErrors;
		}
	}
}
