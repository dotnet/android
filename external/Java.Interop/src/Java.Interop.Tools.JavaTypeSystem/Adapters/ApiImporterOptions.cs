using System;
using System.Collections.ObjectModel;

namespace Java.Interop.Tools.JavaTypeSystem;

public class ApiImporterOptions
{
	public Collection<string> SupportedTypeMapAttributes { get; } = ["Android.Runtime.RegisterAttribute"];
}
