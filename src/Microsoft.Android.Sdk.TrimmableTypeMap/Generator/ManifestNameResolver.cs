using System;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class ManifestNameResolver
{
	/// <summary>
	/// Resolves an <c>android:name</c> value to a fully-qualified class name.
	/// Names starting with '.' are relative to the package. Names with no '.' at all
	/// are also treated as relative (Android tooling convention).
	/// </summary>
	public static string Resolve (string name, string packageName)
	{
		return name switch {
			_ when name.StartsWith (".", StringComparison.Ordinal) => packageName + name,
			_ when name.IndexOf ('.') < 0 && !packageName.IsNullOrEmpty () => packageName + "." + name,
			_ => name,
		};
	}
}
