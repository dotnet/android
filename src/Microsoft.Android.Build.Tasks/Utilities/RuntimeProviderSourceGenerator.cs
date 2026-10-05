using System;
using System.IO;
using System.Text;

namespace Microsoft.Android.Build.Tasks;

internal static class RuntimeProviderSourceGenerator
{
	internal static string ReadResource (string resource)
	{
		using var stream = typeof (RuntimeProviderSourceGenerator).Assembly.GetManifestResourceStream (resource)
			?? throw new InvalidOperationException ($"Java bootstrap resource '{resource}' was not found.");
		using var reader = new StreamReader (stream);
		return reader.ReadToEnd ();
	}

	internal static string [] WriteAdditionalRuntimeProviderSources (
		string outputDirectory, bool isCoreCLR, string [] additionalProviderSources, bool preserveTimestamp = true)
	{
		if (additionalProviderSources.Length == 0) {
			return [];
		}

		string providerName = isCoreCLR ? "MonoRuntimeProvider" : "NativeAotRuntimeProvider";
		string template = ReadResource (isCoreCLR ? "MonoRuntimeProvider.Bundled.java" : "NativeAotRuntimeProvider.java");
		var files = new string [additionalProviderSources.Length];
		for (int i = 0; i < additionalProviderSources.Length; i++) {
			string provider = additionalProviderSources [i];
			string contents = template.Replace (providerName, provider);
			string path = isCoreCLR ?
				Path.Combine (outputDirectory, "src", "mono", provider + ".java") :
				Path.Combine (outputDirectory, "src", "net", "dot", "jni", "nativeaot", provider + ".java");
			if (preserveTimestamp) {
				Files.CopyIfStringChanged (contents, path);
			} else {
				Directory.CreateDirectory (Path.GetDirectoryName (path)
					?? throw new InvalidOperationException ($"No directory for Java source '{path}'."));
				File.WriteAllText (path, contents, new UTF8Encoding (false));
			}
			files [i] = path;
		}
		return files;
	}
}
