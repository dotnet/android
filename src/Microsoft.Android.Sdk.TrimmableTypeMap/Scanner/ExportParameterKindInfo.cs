namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Identifies a special [ExportParameter] marshalling kind applied to
/// a parameter or return value of an [Export] method.
/// </summary>
public enum ExportParameterKindInfo
{
	Unspecified = 0,
	InputStream = 1,
	OutputStream = 2,
	XmlPullParser = 3,
	XmlResourceParser = 4,
}
