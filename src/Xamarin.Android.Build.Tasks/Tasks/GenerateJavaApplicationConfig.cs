#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

using PackageNamingPolicyEnum = Java.Interop.Tools.TypeNameMappings.PackageNamingPolicy;

namespace Xamarin.Android.Tasks;

sealed class AppBootstrapSettings
{
	public string PackageName = "";
	public int PackageNamingPolicy;
	public bool HaveAssemblyStore;
	public IEnumerable<KeyValuePair<string, string>> Environment = [];
	public IEnumerable<KeyValuePair<string, string>> SystemProperties = [];
	public IEnumerable<KeyValuePair<string, string>> RuntimeProperties = [];
	public List<string> NativeLibraries = [];
	public List<byte> NativeLibraryFlags = [];
}

public class GenerateJavaApplicationConfig : AndroidTask
{
	public override string TaskPrefix => "GJAC";

	[Required]
	public string OutputFile { get; set; } = "";

	[Required]
	public string AndroidPackageName { get; set; } = "";

	public string? PackageNamingPolicy { get; set; }
	public ITaskItem []? Environments { get; set; }
	public ITaskItem []? NativeLibraries { get; set; }
	public ITaskItem []? NativeLibrariesNoJniPreload { get; set; }
	public ITaskItem []? NativeLibrariesAlwaysJniPreload { get; set; }
	public string ProjectRuntimeConfigFilePath { get; set; } = "";
	public string? ProjectRuntimeConfigDevFilePath { get; set; }
	public bool UseAssemblyStore { get; set; }

	public override bool RunTask ()
	{
		var envBuilder = new EnvironmentBuilder ();
		envBuilder.Read (Environments);

		var runtimeProperties = new SortedDictionary<string, string> (
			RuntimePropertiesParser.ParseConfig (ProjectRuntimeConfigFilePath, ProjectRuntimeConfigDevFilePath) ?? new Dictionary<string, string> (),
			StringComparer.Ordinal
		);
		runtimeProperties.Remove ("HOST_RUNTIME_CONTRACT");
		runtimeProperties.Remove ("RUNTIME_IDENTIFIER");
		runtimeProperties.Remove ("APP_CONTEXT_BASE_DIRECTORY");
		runtimeProperties.Remove ("PINVOKE_OVERRIDE");
		runtimeProperties.Remove ("BUNDLE_PROBE");

		if (!Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicyEnum namingPolicy)) {
			namingPolicy = PackageNamingPolicyEnum.LowercaseCrc64;
		}

		var environment = new SortedDictionary<string, string> (envBuilder.EnvironmentVariables, StringComparer.Ordinal);
		var systemProperties = new SortedDictionary<string, string> (envBuilder.SystemProperties, StringComparer.Ordinal);
		var libraryNames = new List<string> ();
		var libraryFlags = new List<byte> ();
		var seenLibraryNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var noPreload = JniPreloadPolicy.MakeIgnoreCollection (Log, NativeLibrariesAlwaysJniPreload, NativeLibrariesNoJniPreload);

		foreach (ITaskItem item in NativeLibraries ?? []) {
			string name = item.GetMetadata ("ArchiveFileName");
			if (name.IsNullOrEmpty ()) {
				name = item.ItemSpec;
			}
			name = Path.GetFileName (name);
			if (!seenLibraryNames.Add (name)) {
				continue;
			}

			bool isJni = ELFHelper.IsJniLibrary (Log, item.ItemSpec);
			bool preload = isJni && !JniPreloadPolicy.ShouldIgnore (Log, noPreload, item);
			libraryNames.Add (name);
			libraryFlags.Add ((byte)((isJni ? 1 : 0) | (preload ? 2 : 0)));
		}

		var settings = new AppBootstrapSettings {
			PackageName = AndroidPackageName,
			PackageNamingPolicy = (int)namingPolicy,
			HaveAssemblyStore = UseAssemblyStore,
			Environment = environment,
			SystemProperties = systemProperties,
			RuntimeProperties = runtimeProperties,
			NativeLibraries = libraryNames,
			NativeLibraryFlags = libraryFlags,
		};

		if (Log.HasLoggedErrors) {
			return false;
		}

		WriteSource (OutputFile, settings);
		return true;
	}

	internal static void WriteNativeAotSource (string outputDirectory, ITaskItem []? environments)
	{
		var envBuilder = new EnvironmentBuilder ();
		envBuilder.Read (environments);
		WriteSource (
			Path.Combine (outputDirectory, "src", "net", "dot", "android", "AppBootstrapConfig.java"),
			new AppBootstrapSettings {
				Environment = new SortedDictionary<string, string> (envBuilder.EnvironmentVariables, StringComparer.Ordinal),
				SystemProperties = new SortedDictionary<string, string> (envBuilder.SystemProperties, StringComparer.Ordinal),
			}
		);
	}

	static void WriteSource (string outputFile, AppBootstrapSettings settings)
	{
		// Keep field names and array strides in sync with JavaAppConfig::initialize in the native host.
		var fields = new StringBuilder ();
		AppendString (fields, "PackageName", settings.PackageName);
		fields.AppendLine ($"\tpublic static final int PackageNamingPolicy = {settings.PackageNamingPolicy};");
		fields.AppendLine ($"\tpublic static final boolean HaveAssemblyStore = {settings.HaveAssemblyStore.ToString ().ToLowerInvariant ()};");
		AppendPairs (fields, "Environment", settings.Environment);
		AppendPairs (fields, "SystemProperties", settings.SystemProperties);
		AppendPairs (fields, "RuntimeProperties", settings.RuntimeProperties);
		AppendArray (fields, "NativeLibraries", settings.NativeLibraries);
		fields.AppendLine ("\tpublic static final byte[] NativeLibraryFlags = new byte[] {");
		foreach (byte flags in settings.NativeLibraryFlags) {
			fields.AppendLine ($"\t\t{flags},");
		}
		fields.AppendLine ("\t};");

		using Stream? templateStream = typeof (GenerateJavaApplicationConfig).Assembly.GetManifestResourceStream ("AppBootstrapConfig.java");
		if (templateStream == null) {
			throw new InvalidOperationException ("AppBootstrapConfig.java resource was not found");
		}
		using var reader = new StreamReader (templateStream);
		string source = reader.ReadToEnd ().Replace ("@FIELDS@", fields.ToString ());

		string directory = Path.GetDirectoryName (outputFile) ?? throw new ArgumentException ("Output file must have a directory", nameof (outputFile));
		Directory.CreateDirectory (directory);
		Files.CopyIfStringChanged (source, outputFile);
	}

	static void AppendPairs (StringBuilder source, string name, IEnumerable<KeyValuePair<string, string>> pairs)
	{
		var entries = new List<string> ();
		foreach (var pair in pairs) {
			entries.Add (pair.Key);
			entries.Add (pair.Value);
		}
		AppendArray (source, name, entries);
	}

	static void AppendArray (StringBuilder source, string name, IEnumerable<string> values)
	{
		source.AppendLine ($"\tpublic static final String[] {name} = new String[] {{");
		foreach (string value in values) {
			source.Append ("\t\t");
			AppendJavaString (source, value);
			source.AppendLine (",");
		}
		source.AppendLine ("\t};");
	}

	static void AppendString (StringBuilder source, string name, string value)
	{
		source.Append ($"\tpublic static final String {name} = ");
		AppendJavaString (source, value);
		source.AppendLine (";");
	}

	static void AppendJavaString (StringBuilder source, string value)
	{
		source.Append ('"');
		foreach (char c in value) {
			switch (c) {
				case '\\': source.Append ("\\\\"); break;
				case '"': source.Append ("\\\""); break;
				case '\n': source.Append ("\\n"); break;
				case '\r': source.Append ("\\r"); break;
				case '\t': source.Append ("\\t"); break;
				case '\b': source.Append ("\\b"); break;
				case '\f': source.Append ("\\f"); break;
				default:
					if (c < 0x20 || c == 0x7f) {
						source.Append ('\\');
						source.Append (Convert.ToString ((int)c, 8).PadLeft (3, '0'));
					} else {
						source.Append (c);
					}
					break;
			}
		}
		source.Append ('"');
	}
}
