using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

public sealed class GetNativeAotRuntimeProviders : AndroidTask
{
	const string PackagePrefix = "net.dot.jni.nativeaot.";
	const string ProviderPrefix = PackagePrefix + "NativeAotRuntimeProvider_";

	public override string TaskPrefix => "GNARP";

	[Required]
	public string ManifestFile { get; set; } = "";

	[Output]
	public string [] AdditionalProviderSources { get; set; } = [];

	public override bool RunTask ()
	{
		using var reader = XmlReader.Create (ManifestFile, new XmlReaderSettings {
			DtdProcessing = DtdProcessing.Prohibit,
			XmlResolver = null,
		});
		XNamespace android = "http://schemas.android.com/apk/res/android";
		AdditionalProviderSources = XDocument.Load (reader).Descendants ("provider")
			.Select (provider => provider.Attribute (android + "name")?.Value)
			.OfType<string> ()
			.Where (name => name.StartsWith (ProviderPrefix, StringComparison.Ordinal))
			.Select (name => name.Substring (PackagePrefix.Length))
			.Distinct (StringComparer.Ordinal)
			.ToArray ();
		return !Log.HasLoggedErrors;
	}
}
