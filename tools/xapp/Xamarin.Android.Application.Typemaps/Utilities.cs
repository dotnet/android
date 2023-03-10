using System;
using System.Buffers;
using System.IO;

namespace Xamarin.Android.Application.Typemaps;

static class Utilities
{
	public static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;

	public static string GetOutputFileBaseName (string outputDirectory, string formatVersion, MapKind kind, MapArchitecture architecture)
	{
		string ret = $"typemap-v{formatVersion}-{kind}-{architecture}";
		if (outputDirectory.Length == 0) {
			return ret;
		}

		return Path.Combine (outputDirectory, ret);
	}

	public static string GetManagedOutputFileName (string baseFileName, string extension)
	{
		return $"{baseFileName}-managed.{extension}";
	}

	public static string GetJavaOutputFileName (string baseFileName, string extension)
	{
		return $"{baseFileName}-java.{extension}";
	}

	public static void CreateFileDirectory (string filePath)
	{
		string fileDir = Path.GetDirectoryName (filePath) ?? String.Empty;
		if (fileDir.Length > 0) {
			Directory.CreateDirectory (fileDir);
		}
	}
}
