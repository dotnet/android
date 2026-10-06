#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

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
	public string? CustomBundleConfigFile { get; set; }
	public bool UseAssemblyStore { get; set; }

	public override bool RunTask ()
	{
		var environment = new EnvironmentBuilder (escapeValues: false);
		environment.Read (Environments);
		var runtimeProperties = new SortedDictionary<string, string> (
			RuntimePropertiesParser.ParseConfig (ProjectRuntimeConfigFilePath, ProjectRuntimeConfigDevFilePath)
				?? new Dictionary<string, string> (), StringComparer.Ordinal);
		foreach (string name in new [] {
			"HOST_RUNTIME_CONTRACT", "RUNTIME_IDENTIFIER", "APP_CONTEXT_BASE_DIRECTORY",
			"PINVOKE_OVERRIDE", "BUNDLE_PROBE",
		}) {
			runtimeProperties.Remove (name);
		}

		if (!Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicy namingPolicy)) {
			namingPolicy = Java.Interop.Tools.TypeNameMappings.PackageNamingPolicy.LowercaseCrc64;
		}

		var libraries = new List<string> ();
		var flags = new List<byte> ();
		var seen = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var ignorePreload = JniPreloadPolicy.MakeIgnoreCollection (
			Log, NativeLibrariesAlwaysJniPreload, NativeLibrariesNoJniPreload);

		foreach (ITaskItem item in NativeLibraries ?? []) {
			string name = item.GetMetadata ("ArchiveFileName");
			if (name.IsNullOrEmpty ()) {
				name = item.ItemSpec;
			}
			name = Path.GetFileName (name);
			if (!seen.Add (name)) {
				continue;
			}

			bool isJni = ELFHelper.IsJniLibrary (Log, item.ItemSpec);
			bool preload = isJni && !JniPreloadPolicy.ShouldIgnore (Log, ignorePreload, item);
			libraries.Add (name);
			flags.Add ((byte)((isJni ? 1 : 0) | (preload ? 2 : 0)));
		}

		if (Log.HasLoggedErrors) {
			return false;
		}

		var fields = new StringBuilder ();
		AppendString (fields, "PackageName", AndroidPackageName);
		fields.Append ("\tpublic static final int PackageNamingPolicy = ").Append (((int)namingPolicy).ToString (CultureInfo.InvariantCulture)).AppendLine (";");
		fields.Append ("\tpublic static final boolean HaveAssemblyStore = ").Append (UseAssemblyStore ? "true" : "false").AppendLine (";");
		bool ignoreSplitConfigs = !CustomBundleConfigFile.IsNullOrEmpty () && BundleConfigSplitConfigsChecker.ShouldIgnoreSplitConfigs (Log, CustomBundleConfigFile);
		if (Log.HasLoggedErrors) {
			return false;
		}
		fields.Append ("\tpublic static final boolean IgnoreSplitConfigs = ").Append (ignoreSplitConfigs ? "true" : "false").AppendLine (";");
		AppendPairs (fields, "Environment", environment.EnvironmentVariables);
		AppendPairs (fields, "SystemProperties", environment.SystemProperties);
		AppendPairs (fields, "RuntimeProperties", runtimeProperties);
		AppendArray (fields, "NativeLibraries", libraries);
		fields.AppendLine ("\tpublic static final byte[] NativeLibraryFlags = new byte[] {");
		foreach (byte value in flags) {
			fields.Append ("\t\t").Append (value.ToString (CultureInfo.InvariantCulture)).AppendLine (",");
		}
		fields.AppendLine ("\t};");

		using Stream? template = typeof (GenerateJavaApplicationConfig).Assembly.GetManifestResourceStream ("AppBootstrapConfig.java");
		if (template == null) {
			throw new InvalidOperationException ("AppBootstrapConfig.java resource was not found");
		}
		using var reader = new StreamReader (template);
		string source = reader.ReadToEnd ().Replace ("@FIELDS@", fields.ToString ());
		string directory = Path.GetDirectoryName (OutputFile) ?? throw new ArgumentException ("Output file must have a directory", nameof (OutputFile));
		Directory.CreateDirectory (directory);
		Files.CopyIfStringChanged (source, OutputFile);
		return true;
	}

	static void AppendPairs (StringBuilder source, string name, IEnumerable<KeyValuePair<string, string>> pairs)
	{
		var values = new List<string> ();
		foreach (var pair in pairs) {
			values.Add (pair.Key);
			values.Add (pair.Value);
		}
		AppendArray (source, name, values);
	}

	static void AppendArray (StringBuilder source, string name, IEnumerable<string> values)
	{
		source.Append ("\tpublic static final String[] ").Append (name).AppendLine (" = new String[] {");
		foreach (string value in values) {
			source.Append ("\t\t");
			AppendJavaString (source, value);
			source.AppendLine (",");
		}
		source.AppendLine ("\t};");
	}

	static void AppendString (StringBuilder source, string name, string value)
	{
		source.Append ("\tpublic static final String ").Append (name).Append (" = ");
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
