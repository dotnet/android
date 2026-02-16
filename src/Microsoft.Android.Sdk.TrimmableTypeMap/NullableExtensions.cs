using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class NullableExtensions
{
	// The static methods in System.String are not NRT annotated in netstandard2.0,
	// so we need to add our own extension methods to make them nullable aware.
	public static bool IsNullOrEmpty ([NotNullWhen (false)] this string? str)
	{
		return string.IsNullOrEmpty (str);
	}

	public static bool IsNullOrWhiteSpace ([NotNullWhen (false)] this string? str)
	{
		return string.IsNullOrWhiteSpace (str);
	}
}
