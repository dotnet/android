using System;
using System.IO;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

public sealed class GenerateNativeAotDataObject : AndroidTask
{
	public override string TaskPrefix => "GNADO";

	[Required]
	public string OutputFile { get; set; } = "";

	[Required]
	public string Abi { get; set; } = "";

	public ITaskItem []? Environments { get; set; }
	public string? RemappingXmlFilePath { get; set; }
	public bool Remapping { get; set; }
	public bool Compress { get; set; }
	public string? CompletionStamp { get; set; }

	public override bool RunTask ()
	{
		bool success = false;
		try {
			if (!string.IsNullOrEmpty (CompletionStamp)) {
				File.Delete (CompletionStamp);
			}
			var arch = MonoAndroidHelper.AbiToTargetArch (Abi);
			if (arch is not (AndroidTargetArch.Arm or AndroidTargetArch.Arm64 or AndroidTargetArch.X86_64)) {
				throw new NotSupportedException ($"Unsupported NativeAOT data-object ABI: {Abi}");
			}
			byte [] blob;
			string symbol;
			if (Remapping) {
				symbol = JniRemappingBinaryBlob.Symbol;
				blob = JniRemappingBinaryBlob.Create (RemappingXmlFilePath, Compress);
			} else {
				symbol = NativeAotBootstrapBlob.Symbol;
				var environment = new EnvironmentBuilder (escapeValues: false);
				environment.Read (Environments);
				blob = JniRemappingBinaryBlob.Wrap (
					NativeAotBootstrapBlob.Create (environment.EnvironmentVariables, environment.SystemProperties), Compress);
			}
			if (blob.Length == 0) {
				File.Delete (OutputFile);
			} else {
				using var output = new MemoryStream ();
				NativeAotDataObjectWriter.Write (blob, symbol, output, arch);
				Directory.CreateDirectory (Path.GetDirectoryName (Path.GetFullPath (OutputFile))
					?? throw new InvalidDataException ("No data-object output directory."));
				File.WriteAllBytes (OutputFile, output.ToArray ());
			}
			if (!string.IsNullOrEmpty (CompletionStamp)) {
				Directory.CreateDirectory (Path.GetDirectoryName (Path.GetFullPath (CompletionStamp))
					?? throw new InvalidDataException ("No data-object completion directory."));
				File.WriteAllText (CompletionStamp, blob.Length == 0 ? "false" : "true");
			}
			success = true;
		} catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or
			NotSupportedException or OverflowException or XmlException) {
			Log.LogCodedError ("XA4325", Properties.Resources.XA4325, ex.Message);
		} finally {
			if (!success && File.Exists (OutputFile)) {
				File.Delete (OutputFile);
			}
		}
		return !Log.HasLoggedErrors;
	}
}
