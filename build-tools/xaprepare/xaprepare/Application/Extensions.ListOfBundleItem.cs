using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	static class BundleItem_List_Extensions
	{
		public static void Add (this List<BundleItem> list, string sourcePath)
		{
			list.Add (sourcePath, archivePath: null, shouldInclude: null);
		}

		public static void Add (this List<BundleItem> list, string sourcePath, string archivePath)
		{
			list.Add (sourcePath, archivePath, shouldInclude: null);
		}

		public static void Add (this List<BundleItem> list, string sourcePath, Func<Context, bool> shouldInclude)
		{
			list.Add (sourcePath, archivePath: null, shouldInclude: shouldInclude);
		}

		public static void Add (this List<BundleItem> list, string sourcePath, string archivePath, Func<Context, bool> shouldInclude)
		{
			if (list == null)
				throw new ArgumentNullException (nameof (list));

			list.Add (new BundleItem (sourcePath, archivePath, shouldInclude));
		}

		public static void AddRange (this List<BundleItem> list, List<BclFile> bclFiles)
		{
			if (list == null)
				throw new ArgumentNullException (nameof (list));

			if (bclFiles == null)
				throw new ArgumentNullException (nameof (bclFiles));

			foreach (BclFile bf in bclFiles) {
				if (bf == null)
					continue;

				// BCL file's *destination* location is our *source* path
				(string destFilePath, string debugSymbolsDestPath) = MonoRuntimesHelpers.GetDestinationPaths (bf);
				list.Add (destFilePath);
				if (bf.ExcludeDebugSymbols || !File.Exists (debugSymbolsDestPath))
					continue;
				list.Add (debugSymbolsDestPath);
			}
		}

		public static void AddRange (this List<BundleItem> list, List<MonoUtilityFile> muFiles)
		{
			if (list == null)
				throw new ArgumentNullException (nameof (list));

			if (muFiles == null)
				throw new ArgumentNullException (nameof (muFiles));

			foreach (MonoUtilityFile muf in muFiles) {
				if (muf == null)
					continue;

				// MU file's *destination* location is our *source* path
				(string destFilePath, string debugSymbolsDestPath) = MonoRuntimesHelpers.GetDestinationPaths (muf);
				list.Add (destFilePath);
				if (muf.IgnoreDebugInfo || String.IsNullOrEmpty (debugSymbolsDestPath))
					continue;
				list.Add (debugSymbolsDestPath);
			}
		}

		public static void AddRange (this List<BundleItem> list, List<RuntimeFile> runtimeFiles)
		{
			if (list == null)
				throw new ArgumentNullException (nameof (list));

			if (runtimeFiles == null)
				throw new ArgumentNullException (nameof (runtimeFiles));

			var sharedFiles = new HashSet <string> (StringComparer.Ordinal);
			foreach (Runtime runtime in MonoRuntimesHelpers.GetEnabledRuntimes (new Runtimes (), false)) {
				foreach (RuntimeFile rtf in runtimeFiles) {
					if (rtf == null)
						continue;

					// Runtime file's *destination* location is our *source* path
					(bool skipFile, string _, string destFilePath) = MonoRuntimesHelpers.GetRuntimeFilePaths (runtime, rtf);
					if (rtf.Shared && sharedFiles.Contains (destFilePath))
						continue;

					if (skipFile)
						continue;
					list.Add (destFilePath);

					if (rtf.Shared)
						sharedFiles.Add (destFilePath);
				}
			}
		}

		public static void AddRange (this List<BundleItem> list, List<TestAssembly> testAssemblies)
		{
			if (list == null)
				throw new ArgumentNullException (nameof (list));

			if (testAssemblies == null)
				throw new ArgumentNullException (nameof (testAssemblies));

			foreach (TestAssembly tasm in testAssemblies) {
				if (tasm == null)
					continue;

				// Test assembly's *destination* location is our *source* path
				(string destFilePath, string debugSymbolsDestPath) = MonoRuntimesHelpers.GetDestinationPaths (tasm);
				list.Add (destFilePath);
				if (String.IsNullOrEmpty (debugSymbolsDestPath))
					continue;
				list.Add (debugSymbolsDestPath);
			}
		}
	}
}
