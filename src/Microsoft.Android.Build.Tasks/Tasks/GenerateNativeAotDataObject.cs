using System;
using System.IO;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

[MSBuildMultiThreadableTask]
public sealed class GenerateNativeAotDataObject : MultithreadedAndroidTask
{
	internal const string RemappingSymbol = "dotnet_android_nativeaot_remapping_data";

	public override string TaskPrefix => "GNADO";

	[Required]
	public string OutputFile { get; set; } = "";

	[Required]
	public string Abi { get; set; } = "";

	public ITaskItem []? Environments { get; set; }
	public string? RemappingXmlFilePath { get; set; }
	public bool Remapping { get; set; }
	public string? CompletionStamp { get; set; }

	public override bool RunTask ()
	{
		bool success = false;
		try {
			if (!string.IsNullOrEmpty (CompletionStamp)) {
				File.Delete (TaskEnvironment.GetAbsolutePath (CompletionStamp));
			}
			var arch = MonoAndroidHelper.AbiToTargetArch (Abi);
			if (arch is not (AndroidTargetArch.Arm or AndroidTargetArch.Arm64 or AndroidTargetArch.X86_64)) {
				throw new NotSupportedException ($"Unsupported NativeAOT data-object ABI: {Abi}");
			}
			byte [] blob;
			string symbol;
			if (Remapping) {
				symbol = RemappingSymbol;
				blob = JniRemappingBinaryBlob.Create (RemappingXmlFilePath, compress: false, TaskEnvironment);
			} else {
				symbol = NativeAotBootstrapBlob.Symbol;
				var environment = new EnvironmentBuilder ();
				environment.Read (Environments, TaskEnvironment);
				blob = JniRemappingBinaryBlob.Wrap (
					NativeAotBootstrapBlob.Create (environment.EnvironmentVariables, environment.SystemProperties), compress: false);
			}
			if (blob.Length == 0) {
				File.Delete (TaskEnvironment.GetAbsolutePath (OutputFile));
			} else {
				using var output = new MemoryStream ();
				NativeAotDataObjectWriter.Write (blob, symbol, output, arch);
				Directory.CreateDirectory (TaskEnvironment.GetAbsolutePath (Path.GetDirectoryName (TaskEnvironment.GetAbsolutePath (OutputFile))
					?? throw new InvalidDataException ("No data-object output directory.")));
				File.WriteAllBytes (TaskEnvironment.GetAbsolutePath (OutputFile), output.ToArray ());
			}
			if (!string.IsNullOrEmpty (CompletionStamp)) {
				Directory.CreateDirectory (TaskEnvironment.GetAbsolutePath (Path.GetDirectoryName (TaskEnvironment.GetAbsolutePath (CompletionStamp))
					?? throw new InvalidDataException ("No data-object completion directory.")));
				File.WriteAllText (TaskEnvironment.GetAbsolutePath (CompletionStamp), blob.Length == 0 ? "false" : "true");
			}
			success = true;
		} catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or
			NotSupportedException or OverflowException or XmlException) {
			Log.LogCodedError ("XA4325", Properties.Resources.XA4325, ex.Message);
		} finally {
			if (!success) {
				DeleteFailedOutput (CompletionStamp);
				DeleteFailedOutput (OutputFile);
			}
		}
		return !Log.HasLoggedErrors;
	}

	void DeleteFailedOutput (string? path)
	{
		if (string.IsNullOrEmpty (path)) {
			return;
		}
		try {
			File.Delete (TaskEnvironment.GetAbsolutePath (path));
		} catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) {
			Log.LogCodedError ("XA4325", Properties.Resources.XA4325, ex.Message);
		}
	}
}
