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
		fields.Append ("\tpublic static final int PackageNamingPolicy = ").Append (((int)namingPolicy).ToString (CultureInfo.InvariantCulture)).AppendLine (";");
		fields.Append ("\tpublic static final boolean HaveAssemblyStore = ").Append (UseAssemblyStore ? "true" : "false").AppendLine (";");
		bool ignoreSplitConfigs = !CustomBundleConfigFile.IsNullOrEmpty () && BundleConfigSplitConfigsChecker.ShouldIgnoreSplitConfigs (Log, CustomBundleConfigFile);
		if (Log.HasLoggedErrors) {
			return false;
		}
		fields.Append ("\tpublic static final boolean IgnoreSplitConfigs = ").Append (ignoreSplitConfigs ? "true" : "false").AppendLine (";");
		AppendNativeConfig (fields, environment.EnvironmentVariables, environment.SystemProperties, runtimeProperties, libraries);
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

	void AppendNativeConfig (StringBuilder source, IDictionary<string, string> environment, IDictionary<string, string> systemProperties,
		IDictionary<string, string> runtimeProperties, List<string> libraries)
	{
		using var data = new MemoryStream ();
		var strings = new Dictionary<int, string> ();
		// The fixed JNI reader uses these counts followed by offsets into the NUL-terminated UTF-8 blob.
		var layout = new List<int> { environment.Count, systemProperties.Count, runtimeProperties.Count, libraries.Count };
		AddString (AndroidPackageName);
		AddPairs (environment);
		AddPairs (systemProperties);
		AddPairs (runtimeProperties);
		foreach (string library in libraries) {
			AddString (library);
		}

		source.AppendLine ("\tpublic static final int[] NativeConfigLayout = new int[] {");
		foreach (int value in layout) {
			source.Append ("\t\t").Append (value.ToString (CultureInfo.InvariantCulture)).AppendLine (",");
		}
		source.AppendLine ("\t};");
		AppendBytes (source, data.ToArray (), strings);

		void AddPairs (IDictionary<string, string> pairs)
		{
			foreach (var pair in pairs) {
				AddString (pair.Key);
				AddString (pair.Value);
			}
		}

		void AddString (string value)
		{
			if (value.IndexOf ('\0') >= 0) {
				throw new ArgumentException ("Application bootstrap strings must not contain NUL characters");
			}
			int offset = checked ((int)data.Position);
			layout.Add (offset);
			strings.Add (offset, value);
			byte [] bytes = Encoding.UTF8.GetBytes (value);
			data.Write (bytes, 0, bytes.Length);
			data.WriteByte (0);
		}
	}

	static void AppendBytes (StringBuilder source, byte [] data, Dictionary<int, string> strings)
	{
		const int chunkSize = 4096;
		if (data.Length <= chunkSize) {
			source.AppendLine ("\tpublic static final byte[] NativeConfig = new byte[] {");
			AppendByteValues (source, data, strings, 0, data.Length);
			source.AppendLine ("\t};");
			return;
		}
		source.AppendLine ("\tpublic static final byte[] NativeConfig = createNativeConfig ();");
		source.AppendLine ("\tprivate static byte[] createNativeConfig ()");
		source.AppendLine ("\t{");
		source.Append ("\t\tbyte[] data = new byte[").Append (data.Length.ToString (CultureInfo.InvariantCulture)).AppendLine ("];");
		for (int offset = 0, chunk = 0; offset < data.Length; offset += chunkSize, chunk++) {
			int length = Math.Min (chunkSize, data.Length - offset);
			source.Append ("\t\tSystem.arraycopy (nativeConfigChunk").Append (chunk.ToString (CultureInfo.InvariantCulture))
				.Append (" (), 0, data, ").Append (offset.ToString (CultureInfo.InvariantCulture))
				.Append (", ").Append (length.ToString (CultureInfo.InvariantCulture)).AppendLine (");");
		}
		source.AppendLine ("\t\treturn data;");
		source.AppendLine ("\t}");

		// Literal arrays become DEX fill-array-data payloads; chunking avoids Java's per-method bytecode limit.
		for (int offset = 0, chunk = 0; offset < data.Length; offset += chunkSize, chunk++) {
			source.Append ("\tprivate static byte[] nativeConfigChunk").Append (chunk.ToString (CultureInfo.InvariantCulture)).AppendLine (" ()");
			source.AppendLine ("\t{");
			source.AppendLine ("\t\treturn new byte[] {");
			AppendByteValues (source, data, strings, offset, Math.Min (offset + chunkSize, data.Length));
			source.AppendLine ("\t\t};");
			source.AppendLine ("\t}");
		}
	}

	static void AppendByteValues (StringBuilder source, byte [] data, Dictionary<int, string> strings, int offset, int end)
	{
		if (offset > 0 && !strings.ContainsKey (offset)) {
			source.AppendLine ("\t\t\t// Continuation of the preceding UTF-8 string.");
		}
		int column = 0;
		for (int i = offset; i < end; i++) {
			if (strings.TryGetValue (i, out string? value)) {
				if (column > 0) {
					source.AppendLine ();
					column = 0;
				}
				source.Append ("\t\t\t// ");
				AppendJavaString (source, value);
				source.AppendLine ();
			}
			if (column == 0) {
				source.Append ("\t\t\t");
			} else {
				source.Append (' ');
			}
			source.Append (unchecked ((sbyte)data [i]).ToString (CultureInfo.InvariantCulture)).Append (",");
			if (++column == 16 || i + 1 == end) {
				source.AppendLine ();
				column = 0;
			}
		}
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
