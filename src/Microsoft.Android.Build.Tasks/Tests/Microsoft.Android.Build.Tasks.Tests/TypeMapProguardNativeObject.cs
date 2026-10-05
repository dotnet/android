using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests;

static class TypeMapProguardNativeObject
{
	public static string LlvmReadObjPath =>
		typeof (TypeMapProguardNativeObject).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (attribute => attribute.Key == "NativeAotLlvmReadObjPath").Value ?? "";

	public static string LlvmObjDumpPath => ToolPath ("llvm-objdump");

	static string ToolPath (string name) => Path.Combine (Path.GetDirectoryName (LlvmReadObjPath) ?? "",
		OperatingSystem.IsWindows () ? name + ".exe" : name);

	public static string WriteObject (string directory, string name, params string [] keys)
	{
		string clang = ToolPath ("clang");
		if (!File.Exists (LlvmReadObjPath) || !File.Exists (LlvmObjDumpPath) || !File.Exists (clang)) {
			Assert.Ignore ("Set _NativeAotLlvmReadObjPath to the NDK llvm-readobj executable with adjacent llvm-objdump and clang to run native-object integration tests.");
		}

		string objectPath = Path.ChangeExtension (Path.Combine (directory, name), ".o");
		string sourcePath = Path.ChangeExtension (objectPath, ".s");
		byte [] blob = CreateTable ([CreateGroup (keys)]);
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
		source.Append ("""
			.size __external_type_map__, . - __external_type_map__
			.balign 4
			.globl __external_CommonFixupsTable_references
			.type __external_CommonFixupsTable_references,%object
			__external_CommonFixupsTable_references:
			.long _ZTV29Mono_Android_Java_Lang_Object - .
			.size __external_CommonFixupsTable_references, . - __external_CommonFixupsTable_references
			.section .data,"aw",%progbits
			.globl _ZTV29Mono_Android_Java_Lang_Object
			.type _ZTV29Mono_Android_Java_Lang_Object,%object
			_ZTV29Mono_Android_Java_Lang_Object:
			.byte 0
			.size _ZTV29Mono_Android_Java_Lang_Object, 1

			""");
		File.WriteAllText (sourcePath, source.ToString (), new UTF8Encoding (false));

		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		var startInfo = ProcessUtils.CreateProcessStartInfo (clang,
			"--target=aarch64-linux-android", "-c", "-x", "assembler", sourcePath, "-o", objectPath);
		int exitCode = ProcessUtils.StartProcess (startInfo, stdout, stderr, CancellationToken.None).GetAwaiter ().GetResult ();
		if (exitCode != 0) {
			throw new InvalidOperationException ($"clang exited with code {exitCode}: {stderr}{stdout}");
		}
		return objectPath;
	}

	static byte [] CreateGroup (string [] keys) =>
		[0, 2, .. CreateTable (keys.Select (CreateKey).ToArray ())];

	static byte [] CreateKey (string key)
	{
		byte [] utf8 = new UTF8Encoding (false, true).GetBytes (key);
		return [.. EncodeUnsigned ((uint) utf8.Length), .. utf8, 0];
	}

	static byte [] CreateTable (byte [] [] payloads)
	{
		var bytes = new List<byte> { 0, 2, 0 };
		var pointers = new List<int> ();
		for (int i = 0; i < payloads.Length; i++) {
			bytes.Add ((byte) i);
			pointers.Add (bytes.Count);
			bytes.AddRange (new byte [5]);
		}
		bytes [2] = checked ((byte) (bytes.Count - 1));
		for (int i = 0; i < payloads.Length; i++) {
			// NativeFormat offsets are relative to the integer's address, not its end.
			uint relative = checked ((uint) (bytes.Count - pointers [i]));
			bytes [pointers [i]] = 15;
			for (int b = 0; b < 4; b++) {
				bytes [pointers [i] + b + 1] = (byte) (relative >> (8 * b));
			}
			bytes.AddRange (payloads [i]);
		}
		return bytes.ToArray ();
	}

	static byte [] EncodeUnsigned (uint value)
	{
		int width = value < 1u << 7 ? 1 : value < 1u << 14 ? 2 : value < 1u << 21 ? 3 : value < 1u << 28 ? 4 : 5;
		var result = new byte [width];
		if (width == 5) {
			result [0] = 15;
			for (int i = 0; i < 4; i++) {
				result [i + 1] = (byte) (value >> (8 * i));
			}
		} else {
			uint encoded = (value << width) | ((1u << (width - 1)) - 1);
			for (int i = 0; i < width; i++) {
				result [i] = (byte) (encoded >> (8 * i));
			}
		}
		return result;
	}
}
