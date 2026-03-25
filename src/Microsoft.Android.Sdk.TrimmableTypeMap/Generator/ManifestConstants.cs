#nullable enable

using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class ManifestConstants
{
	public static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
	public static readonly XName AttName = AndroidNs + "name";
}
