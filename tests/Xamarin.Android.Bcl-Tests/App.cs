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
                        // https://jenkins.mono-project.com/job/xamarin-android-pr-builder/1720/testReport/(root)/AssemblyTest/GetReferencedAssemblies/
                        // https://bugzilla.xamarin.com/show_bug.cgi?id=59908
                        // AssemblyName.Flags == AssemblyNameFlags.PublicKey; expected AssemblyNameFlags.None
			"MonoTests.System.Reflection.AssemblyTest.GetReferencedAssemblies",
                        // https://jenkins.mono-project.com/job/xamarin-android-pr-builder/1720/testReport/(root)/WebInvokeAttributeTest/RejectTwoParametersWhenNotWrapped/
                        // https://bugzilla.xamarin.com/show_bug.cgi?id=59909
                        // InvalidOperationException wasn't thrown when it was expected
			"MonoTests.System.ServiceModel.Description.WebInvokeAttributeTest.RejectTwoParametersWhenNotWrapped",
		};

		internal static ITestFilter UpdateFilter (ITestFilter filter)
		{
			if (ExcludeTestNames?.Length == 0)
				return filter;
			var excludeTestNamesFilter  = new SimpleNameFilter (ExcludeTestNames);
			return new AndFilter (filter, new NotFilter (excludeTestNamesFilter));
		}
	}
}
