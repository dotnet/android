using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.AppTools.Assemblies;
using Xamarin.Android.Tools;

namespace Microsoft.Android.AppTools;

/// <summary>
/// Main application information class.  Gathers information about the application, or components thereof,
/// regardless of whether the data is gathered from an application archive or just a single file (e.g.
/// the assembly store) that makes part of the application.
/// </summary>
public class ApplicationInfo
{
	readonly ILogger log;

	/// <summary>
	/// If information was obtained from an application archive (`.apk`, `.aab` or `.zip`), this
	/// property will be set accordingly to a value different than <see cref="ArchiveKind.None" />
	/// </summary>
	public ArchiveKind ArchiveKind { get; private set; } = ArchiveKind.None;

	public ApplicationRuntime RuntimeKind { get; private set; } = ApplicationRuntime.Unknown;

	/// <summary>
	/// If assembly stores were read, either from the archive or directly from a file on the filesystem,
	/// this property will contain instances of the <see cref="AssemblyStore" /> class describing the
	/// stores in detail. If the collection isn't `null`, it is also guaranteed not to be empty.
	/// </summary>
	public ICollection<AssemblyStore>? AssemblyStores { get; private set; }

	/// <summary>
	/// If application info was obtained from an application archive (`.apk`, `.aab` or `.zip`) or
	/// from `AndroidManifest.xml`, this property will contain the application's package name (if
	/// found in the manifest).
	/// </summary>
	public string? PackageName { get; private set; }

	/// <summary>
	/// If application info was obtained from an application archive (`.apk`, `.aab` or `.zip`) or
	/// by directly reading a `.so` shared library, this collection will contain information about
	/// all the shared libraries for all the target architectures supported by the application.
	/// </summary>
	public ICollection<SharedLibrary>? SharedLibraries { get; private set; }

	/// <summary>
	/// If application info was obtained from an application archive (`.apk`, `.aab` or `.zip`) or
	/// by directly reading a `.so` shared library, this collection will contain all the architectures
	/// targeted by the application.
	/// </summary>
	public ICollection<AndroidTargetArch>? TargetArchitectures { get; private set; }

	public ApplicationInfo (ILogger log)
	{
		this.log = log;
	}

	/// <summary>
	/// Reads application information from the file passed in the `inputFilePath` parameter. The file
	/// doesn't have to be application .apk or .aab archive, it can be any file that is (or is not)
	/// part of the application. If the file is unrecognized, unsupported etc, the constructor will
	/// make a note of it and initialize the class accordingly. All the issues will be reported via
	/// calls to members of the `ILogger` interface, passed in the `log` parameter to the class
	/// constructor. Returns `true` if there was any valid information read, `false` otherwise.
	/// </summary>
	public bool Read (string inputFilePath)
	{
		(ApplicationRuntime runtimeKind, FileFormat format, FileInfo? info) = Utils.DetectFileFormat (log, inputFilePath);
		if (info == null || format == FileFormat.Unknown) {
			return false;
		}

		RuntimeKind = runtimeKind;
		ArchiveKind = format switch {
			FileFormat.Aab => ArchiveKind.AAB,
			FileFormat.Apk => ArchiveKind.APK,
			FileFormat.Zip => ArchiveKind.ZIP,
			_ => ArchiveKind.None
		};

		IList<AssemblyStoreExplorer>? explorers = AssemblyStoreExplorer.Open (log, inputFilePath, format, info);
		if (explorers != null) {
			var stores = new List<AssemblyStore> ();
			foreach (AssemblyStoreExplorer exp in explorers) {
				stores.Add (new AssemblyStore (log, exp));
			}
			AssemblyStores = stores;
		}

		return true;
	}
}
