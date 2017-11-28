using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

using NUnit.Framework.Api;
using NUnit.Framework.Internal.Filters;

namespace Xamarin.Android.BclTests
{
	static partial class App
	{
		internal static string[] ExcludeAssemblyNames = {
			// Not needed; distributed only for "sanity"/consistency
			"nunitlite.dll",
		};

		internal static IEnumerable<Assembly> GetTestAssemblies ()
		{
			yield return typeof (App).Assembly;

			var names   = TestAssemblyNames.Except (ExcludeAssemblyNames);
			foreach (var name in names) {
				var a = Assembly.Load (name);
				if (a == null) {
					Console.WriteLine ($"# WARNING: Unable to load assembly: {name}");
					continue;
				}
				yield return a;
			}
		}

		internal static IEnumerable<string> GetExcludedCategories ()
		{
			var excluded = new List<string> {
				"AndroidNotWorking",
				"CAS",
				"InetAccess",
				"MobileNotWorking",
				"NotWorking",
			};

			if (!System.Environment.Is64BitOperatingSystem) {
				excluded.Add ("LargeFileSupport");
			}

			return excluded;
		}

		internal static void ExtractBclTestFiles ()
		{
			var cachePath   = global::Android.App.Application.Context.CacheDir.AbsolutePath;
			if (Directory.Exists (Path.Combine (cachePath, "Test")))
				return;

			using (var files    = typeof (App).Assembly.GetManifestResourceStream ("bcl-tests.zip"))
			using (var zip      = new ZipArchive (files, ZipArchiveMode.Read)) {
				zip.ExtractToDirectory (cachePath);
			}
		}

		static string[] ExcludeTestNames = new string[]{
		};

		internal static ITestFilter UpdateFilter (ITestFilter filter)
		{
			if (ExcludeTestNames == null || ExcludeTestNames.Length == 0)
				return filter;
			var excludeTestNamesFilter  = new SimpleNameFilter (ExcludeTestNames);
			return new AndFilter (filter, new NotFilter (excludeTestNamesFilter));
		}
	}
}
