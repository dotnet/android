using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestClass]
	public class AndroidRidAbiHelperTests
	{
		public static IEnumerable<object []> StringValueSource { get; } = new object [] [] {
			new[] {
				/* input */    default (string),
				/* expected */ default (string)
			},
			new[] {
				/* input */    "",
				/* expected */ default
			},
			new[] {
				/* input */    "armeabi-v7a/libfoo.so",
				/* expected */ "armeabi-v7a"
			},
			new[] {
				/* input */    "arm64-v8a/libfoo.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    "x86/libfoo.so",
				/* expected */ "x86"
			},
			new[] {
				/* input */    "x86_64/libfoo.so",
				/* expected */ "x86_64"
			},
			new[] {
				/* input */    "android-arm/libfoo.so",
				/* expected */ "armeabi-v7a"
			},
			new[] {
				/* input */    "android-arm64/libfoo.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    "android-x86/libfoo.so",
				/* expected */ "x86"
			},
			new[] {
				/* input */    "android-x64/libfoo.so",
				/* expected */ "x86_64"
			},
			new[] {
				/* input */    "android-arm/native/libfoo.so",
				/* expected */ "armeabi-v7a"
			},
			new[] {
				/* input */    "android-arm64/native/libfoo.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    "android-x86/native/libfoo.so",
				/* expected */ "x86"
			},
			new[] {
				/* input */    "android-x64/native/libfoo.so",
				/* expected */ "x86_64"
			},
			new[] {
				/* input */    "android.21-x64/native/libfoo.so",
				/* expected */ "x86_64"
			},
			new[] {
				/* input */    "packages/sqlitepclraw.lib.e_sqlite3.android/1.1.11/runtimes/android-arm64/native/libe_sqlite3.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    "arm64-v8a\\libfoo.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    "android-arm64\\libfoo.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    @"C:\libfoo.so",
				/* expected */ default (string)
			},
			new[] {
				/* input */    "/libfoo.so",
				/* expected */ default (string)
			},
			new[] {
				/* input */    @"C:\",
				/* expected */ default (string)
			},
			new[] {
				/* input */    "/",
				/* expected */ default (string)
			},
			new[] {
				/* input */    @"C:\arm64-v8a\libfoo.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    @"C:\android-arm64\libfoo.so",
				/* expected */ "arm64-v8a"
			},
			new[] {
				/* input */    @"C:\android-x64\native\libfoo.so",
				/* expected */ "x86_64"
			},
		};

		[TestMethod]
		[DynamicData (nameof (StringValueSource))]
		public void StringValue (string input, string expected)
		{
			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (input));
		}

		public static IEnumerable<object []> ITaskItemValueSource { get; } = new object [] [] {
			new object [] {
				/* input */
				new TaskItem(""),
				/* expected */
				default (string)
			},
			new object [] {
				/* input */
				new TaskItem("armeabi-v7a/libfoo.so"),
				/* expected */
				"armeabi-v7a"
			},
			new object [] {
				/* input */
				new TaskItem("libabi.so", new Dictionary<string,string> {
					{ "Abi", "armeabi-v7a" }
				}),
				/* expected */
				"armeabi-v7a"
			},
			new object [] {
				/* input */
				new TaskItem("librid.so", new Dictionary<string,string> {
					{ "RuntimeIdentifier", "android-arm" }
				}),
				/* expected */
				"armeabi-v7a"
			},
			new object [] {
				/* input */
				new TaskItem("liblink.so", new Dictionary<string,string> {
					{ "Link", "armeabi-v7a/libfoo.so" }
				}),
				/* expected */
				"armeabi-v7a"
			},
			new object [] {
				/* input */
				new TaskItem("liblink.so", new Dictionary<string,string> {
					{ "Link", "x86/libfoo.so" }
				}),
				/* expected */
				"x86"
			},
			new object [] {
				/* input */
				new TaskItem("liblink.so", new Dictionary<string,string> {
					{ "Link", "x86_64/libfoo.so" }
				}),
				/* expected */
				"x86_64"
			},
			new object [] {
				/* input */
				new TaskItem("libridlink.so", new Dictionary<string,string> {
					{ "Link", "android-arm/libfoo.so" }
				}),
				/* expected */
				"armeabi-v7a"
			},
			new object [] {
				/* input */
				new TaskItem("liblinkwin.so", new Dictionary<string,string> {
					{ "Link", "x86_64\\libfoo.so" }
				}),
				/* expected */
				"x86_64"
			},
			new object [] {
				/* input */
				new TaskItem("liblinkwin.so", new Dictionary<string,string> {
					{ "Link", "android-arm64\\libfoo.so" },
				}),
				/* expected */
				"arm64-v8a",
			},
			new object [] {
				/* input */
				new TaskItem(@"C:\libfoo.so"),
				/* expected */
				default (string)
			},
			new object [] {
				/* input */
				new TaskItem("/libfoo.so"),
				/* expected */
				default (string)
			},
			new object [] {
				/* input */
				new TaskItem(@"C:\arm64-v8a\libfoo.so"),
				/* expected */
				"arm64-v8a"
			},
			new object [] {
				/* input */
				new TaskItem("liblink.so", new Dictionary<string,string> {
					{ "Link", @"C:\libfoo.so" }
				}),
				/* expected */
				default (string)
			},
		};

		[TestMethod]
		[DynamicData (nameof (ITaskItemValueSource))]
		public void ITaskItemValue (ITaskItem input, string expected)
		{
			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (input));
		}

		[TestMethod]
		[DataRow ("arm64-v8a", "android-x64", "x86/libfoo.so", "arm64-v8a")]
		[DataRow ("", "android-x64", "x86/libfoo.so", "x86_64")]
		[DataRow ("", "", "x86/libfoo.so", "x86")]
		[DataRow ("", "invalid", "x86/libfoo.so", "armeabi-v7a")]
		[DataRow ("", "", "unknown/libfoo.so", "armeabi-v7a")]
		[DataRow ("", "", "", "armeabi-v7a")]
		public void MetadataPrecedence (string abi, string rid, string link, string expected)
		{
			var library = new TaskItem (Path.Combine ("armeabi-v7a", "libfoo.so"));
			library.SetMetadata ("Abi", abi);
			library.SetMetadata ("RuntimeIdentifier", rid);
			library.SetMetadata ("Link", link);

			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (library));
		}

		[TestMethod]
		[DataRow ("libfoo.so", "arm64-v8a")]
		[DataRow ("./libfoo.so", "arm64-v8a")]
		[DataRow ("../x86/libfoo.so", "x86")]
		[DataRow ("../android-x64/native/libfoo.so", "x86_64")]
		[DataRow ("../interpreter-armeabi-v7a/libfoo.so", "armeabi-v7a")]
		[DataRow ("../unknown/libfoo.so", null)]
		public void ProjectRelativePath (string path, string expected)
		{
			var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (
				Path.Combine (Path.GetTempPath (), "AndroidRidAbiHelperTests", "arm64-v8a"),
				new Dictionary<string, string> ());

			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (path, environment));
			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (new TaskItem (path), environment));
			var linked = new TaskItem (Path.Combine (Path.GetTempPath (), "unknown", "libfoo.so"));
			linked.SetMetadata ("Link", path);
			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (linked, environment));
		}
	}
}
