using System.Collections.Generic;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class AndroidRidAbiHelperTests
	{
		static object [] StringValueSource = new object [] {
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
		};

		[Test]
		[TestCaseSource (nameof (StringValueSource))]
		public void StringValue (string input, string expected)
		{
			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (input));
		}

		static object [] ITaskItemValueSource = new object [] {
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
		};

		[Test]
		[TestCaseSource (nameof (ITaskItemValueSource))]
		public void ITaskItemValue (ITaskItem input, string expected)
		{
			Assert.AreEqual (expected, AndroidRidAbiHelper.GetNativeLibraryAbi (input));
		}
	}
}
