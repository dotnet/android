using System;
using System.Buffers;
using System.IO;

namespace Xamarin.Android.Application.Utilities;

static class Util
{
	public static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;

	public static void CreateFileDirectory (string filePath)
	{
		string fileDir = Path.GetDirectoryName (filePath) ?? String.Empty;
		if (fileDir.Length > 0) {
			Directory.CreateDirectory (fileDir);
		}
	}
}
