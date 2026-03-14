using System;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace ApplicationUtility;

class Utilities
{
	public static void DeleteFile (string path, bool quiet = true)
	{
		try {
			File.Delete (path);
		} catch (Exception ex) {
			Log.Debug ($"Failed to delete file '{path}'.", ex);
			if (!quiet) {
				throw;
			}
		}
	}

	public static void CloseAndDeleteFile (FileStream stream, bool quiet = true)
	{
		string path = stream.Name;
		try {
			stream.Close ();
		} catch (Exception ex) {
			Log.Debug ($"Failed to close file stream.", ex);
			if (!quiet) {
				throw;
			}
		}

		DeleteFile (path);
	}

	// TODO: review all the call sites, now that `rewindStream` default is different
	public static BinaryReader GetReaderAndRewindStream (Stream stream, bool rewindStream = true)
	{
		if (rewindStream) {
			stream.Seek (0, SeekOrigin.Begin);
		}

		return new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
	}

	public static BasicAspectState GetFailureAspectState (string message)
	{
		Log.Debug (message);
		return new BasicAspectState (false);
	}

	public static string ToStringOrNull<T> (T? reference) => reference == null ? "<NULL>" : reference.ToString () ?? "[unknown]";

	public static string GetZipEntryFileName (string entryName)
	{
		int idx = entryName.LastIndexOf ('/');
		if (idx <= 0) {
			return entryName;
		}

		if (idx == entryName.Length - 1) {
			return String.Empty;
		}

		return entryName.Substring (idx + 1);
	}

	public static string SizeToString (ulong val)
	{
		// TODO: return both bytes and a "human readable" value (kb, mb etc)
		// TODO: format the byte size according to the current culture
		return val.ToString ();
	}

	// TODO: consider replacing all uses of `AndroidTargetArch` with `NativeArchitecture`
	public static NativeArchitecture TargetArchToNative (AndroidTargetArch arch)
	{
		return arch switch {
			AndroidTargetArch.Arm    => NativeArchitecture.Arm,
			AndroidTargetArch.Arm64  => NativeArchitecture.Arm64,
			AndroidTargetArch.X86    => NativeArchitecture.X86,
			AndroidTargetArch.X86_64 => NativeArchitecture.X64,
			_                        => throw new NotSupportedException ($"Unsupported Android target architecture '{arch}'")
		};
	}

	public static NativeArchitecture AssemblyStoreAbiToNative (AssemblyStoreABI abi)
	{
		return abi switch {
			AssemblyStoreABI.Arm   => NativeArchitecture.Arm,
			AssemblyStoreABI.Arm64 => NativeArchitecture.Arm64,
			AssemblyStoreABI.X86   => NativeArchitecture.X86,
			AssemblyStoreABI.X64   => NativeArchitecture.X64,
			_                      => throw new NotSupportedException ($"Unsupported assembly store ABI '{abi}'")
		};
	}

	public static string ArchNameForPath (NativeArchitecture arch)
	{
		return arch switch {
			NativeArchitecture.Arm     => "arm",
			NativeArchitecture.Arm64   => "arm64",
			NativeArchitecture.X86     => "x86",
			NativeArchitecture.X64     => "x86-64",
			NativeArchitecture.Unknown => "unknown-abi",
			_                          => throw new NotSupportedException ($"Unsupported native architecture '{arch}'")
		};
	}

	public static string DemangleSharedAssemblyLibraryName (string sharedLibraryName) => DemangleSharedLibraryName (sharedLibraryName, ".dll.so");
	public static string DemangleSharedPdbLibraryName (string sharedLibraryName) => DemangleSharedLibraryName (sharedLibraryName, ".pdb.so");

	public static string DemangleSharedLibraryName (string sharedLibraryName, string removeExtensionIfEndsWith)
	{
		string? cultureName = null;
		if (sharedLibraryName.StartsWith ("lib_", StringComparison.Ordinal)) {
			sharedLibraryName = sharedLibraryName.Substring (4);
		} else if (sharedLibraryName.StartsWith ("lib-", StringComparison.Ordinal)) {
			int cultureEnd = sharedLibraryName.IndexOf ('_');
			if (cultureEnd == -1 || cultureEnd == 4) {
				// No culture, odd
				sharedLibraryName = sharedLibraryName.Substring (4);
			} else {
				cultureName = sharedLibraryName.Substring (4, cultureEnd - 4);
				sharedLibraryName = sharedLibraryName.Substring (cultureEnd + 1);
			}
		}

		if (sharedLibraryName.EndsWith (removeExtensionIfEndsWith, StringComparison.Ordinal)) {
			sharedLibraryName = Path.GetFileNameWithoutExtension (sharedLibraryName);
		}

		if (!String.IsNullOrEmpty (cultureName)) {
			sharedLibraryName = $"{cultureName}/{sharedLibraryName}";
		}

		Log.Debug ($"Demangled library name: '{sharedLibraryName}'");
		return sharedLibraryName;
	}
}
