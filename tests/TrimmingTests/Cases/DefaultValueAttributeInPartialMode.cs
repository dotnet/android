#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.defaultvalueattributeinpartialmode
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/9525 (https://github.com/dotnet/runtime/issues/109724):
// trimmed apps disable DefaultValueAttribute (Type, string), so XmlSerializer failed at runtime on a property with such
// an attribute. TrimMode=partial suppresses the warning that explains it, so partial mode keeps the attribute working.

using System;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using Android.App;
using Android.OS;

[Activity (Label = "DefaultValueAttributeInPartialMode", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			// XmlSerializer omits members that equal their [DefaultValue].
			using var writer = new StringWriter ();
			new XmlSerializer (typeof (Settings)).Serialize (writer, new Settings { Retries = 5, Name = "custom" });
			var xml = writer.ToString ();
			if (xml.Contains ("<Retries>", StringComparison.Ordinal) || !xml.Contains ("<Name>custom</Name>", StringComparison.Ordinal)) {
				throw new InvalidOperationException ($"Unexpected XML: {xml}");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

public class Settings
{
	[DefaultValue (typeof (int), "5")]
	public int Retries { get; set; }

	public string? Name { get; set; }
}
