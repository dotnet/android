#nullable enable

using System;
using System.IO;
using System.Text;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Properties = Xamarin.Android.Tasks.Properties;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

public class GenerateJniRemappingAsset : AndroidTask
{
	public override string TaskPrefix => "GJRA";

	public string? RemappingXmlFilePath { get; set; }

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		try {
			var entries = RemappingXmlFilePath.IsNullOrEmpty ()
				? new JniRemappingEntries ()
				: JniRemappingXmlReader.Read (RemappingXmlFilePath, Log);
			if (Log.HasLoggedErrors)
				return false;
			byte [] data = JniRemappingAssetWriter.Write (entries);
			string? directory = Path.GetDirectoryName (OutputFile);
			if (!directory.IsNullOrEmpty ())
				Directory.CreateDirectory (directory);
			string temporaryOutputFile = OutputFile + "." + Guid.NewGuid ().ToString ("N") + ".tmp";
			try {
				WriteOutputFile (temporaryOutputFile, data);
				File.Move (temporaryOutputFile, OutputFile, overwrite: true);
			} finally {
				File.Delete (temporaryOutputFile);
			}
		} catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is XmlException ||
				ex is EncoderFallbackException || ex is OverflowException || ex is ArgumentException || ex is NotSupportedException) {
			Log.LogCodedError ("XA4331", Properties.Resources.XA4331, RemappingXmlFilePath ?? "", OutputFile, ex.Message);
		}
		return !Log.HasLoggedErrors;
	}

	protected virtual void WriteOutputFile (string outputFile, byte [] data)
		=> File.WriteAllBytes (outputFile, data);
}
