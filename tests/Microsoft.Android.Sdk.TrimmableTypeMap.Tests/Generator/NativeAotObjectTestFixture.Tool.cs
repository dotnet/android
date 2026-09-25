using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xamarin.Android.Tools;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

static partial class NativeAotObjectTestFixture
{
	public static string WriteObject (string directory, string name, string llvmReadObjPath, string targetTriple, params string [] keys)
		=> WriteObjectGroups (directory, name, llvmReadObjPath, targetTriple, ("_ZTV29Mono_Android_Java_Lang_Object", keys));

	public static string WriteObjectGroups (
		string directory, string name, string llvmReadObjPath, string targetTriple, params (string Symbol, string [] Keys) [] groups)
	{
		string? toolDirectory = Path.GetDirectoryName (llvmReadObjPath);
		if (string.IsNullOrEmpty (toolDirectory)) {
			throw new ArgumentException ("Expected a full path to llvm-readobj.", nameof (llvmReadObjPath));
		}
		string clang = Path.Combine (toolDirectory, OperatingSystem.IsWindows () ? "clang.exe" : "clang");
		Directory.CreateDirectory (directory);
		string objectPath = Path.ChangeExtension (Path.Combine (directory, name), ".o");
		string sourcePath = Path.ChangeExtension (objectPath, ".s");
		byte [] blob = CreateGroups (groups.Select (group => group.Keys).ToArray ());
		var source = new StringBuilder ("""
			.section .rodata,"a",%progbits
			.globl __external_type_map__
			.hidden __external_type_map__
			.type __external_type_map__,%object
			__external_type_map__:

			""");
		for (int i = 0; i < blob.Length; i += 32) {
			source.Append (".byte ");
			source.AppendJoin (",", blob.Skip (i).Take (32).Select (b => "0x" + b.ToString ("x2", CultureInfo.InvariantCulture)));
			source.Append ('\n');
		}
		source.Append (".size __external_type_map__, . - __external_type_map__\n");
		source.Append ("""
			.balign 4
			.globl __external_CommonFixupsTable_references
			.type __external_CommonFixupsTable_references,%object
			__external_CommonFixupsTable_references:

			""");
		foreach (var group in groups) {
			source.Append (".long ").Append (group.Symbol).Append (" - .\n");
		}
		source.Append ("""
			.size __external_CommonFixupsTable_references, . - __external_CommonFixupsTable_references
			.section .data,"aw",%progbits

			""");
		foreach (string symbol in groups.Select (group => group.Symbol).Distinct (StringComparer.Ordinal)) {
			source.Append (".globl ").Append (symbol).Append ('\n');
			source.Append (".type ").Append (symbol).Append (",%object\n");
			source.Append (symbol).Append (":\n.byte 0\n");
			source.Append (".size ").Append (symbol).Append (", 1\n");
		}
		File.WriteAllText (sourcePath, source.ToString (), new UTF8Encoding (false));

		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		var startInfo = ProcessUtils.CreateProcessStartInfo (clang,
			"--target=" + targetTriple, "-c", "-x", "assembler", sourcePath, "-o", objectPath);
		int exitCode = ProcessUtils.StartProcess (startInfo, stdout, stderr, CancellationToken.None).GetAwaiter ().GetResult ();
		if (exitCode != 0) {
			throw new InvalidOperationException ($"clang exited with code {exitCode}: {stderr}{stdout}");
		}
		return objectPath;
	}
}
