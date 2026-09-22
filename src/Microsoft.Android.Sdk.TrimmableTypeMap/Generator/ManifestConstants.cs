using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class ManifestConstants
{
	public static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
	public static readonly XName AttName = AndroidNs + "name";
	public static readonly HashSet<string> ComponentElementNames = new (StringComparer.Ordinal) {
		"application",
		"activity",
		"activity-alias",
		"instrumentation",
		"service",
		"receiver",
		"provider",
	};
}
