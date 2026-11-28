using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	public class AidlTest : BaseTest
	{
		void TestAidl (string testName, string aidl, AndroidRuntime runtime)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidInterfaceDescription, "Test.aidl") { TextContent = () => aidl });
			using (var p = CreateApkBuilder (Path.Combine ("temp", testName))) {
				Assert.IsTrue (p.Build (proj), "Build should have succeeded.");
			}
		}

		[TestMethod]
		[CombinatorialData]
		public void ListAndMap ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			string aidl = @"
import android.os;
import android.view;
import android.animation;

interface Test {
	void arrayList(inout List list);
	void map(inout Map m);
}
";
			TestAidl (TestName, aidl, runtime);
		}

		[TestMethod]
		[CombinatorialData]
		public void NamespaceResolution ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			string aidl = @"
import android.os;
import android.view;
import android.animation;

interface Test {
}
";
			TestAidl (TestName, aidl, runtime);
		}

		[TestMethod]
		[CombinatorialData]
		public void PrimitiveTypes ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			string aidl = @"
package com.xamarin.test;
interface Test {
	void test0();
	int test1 ();
	// now those test methods are explicitly added 'inout' because otherwise Android SDK aidl rejects it...
	int test2 (inout int [] args);
	boolean test3 (inout boolean [] args);
	byte test4 (inout byte [] args);
	char test5 (inout char [] args);
	long test6 (inout long [] args);
	float test7 (inout float [] args);
	double test8 (inout double [] args);
	String test9 (inout String [] args);
	// thought that it's missing 'short' ? It's not supported - http://stackoverflow.com/questions/6742167/android-aidl-support-short-type

	void test10 (in byte [] args);
	void test11 (out byte [] args);
	void test12 (inout byte [] args);

	int [] test20 ();
	boolean [] test21 ();
	byte [] test22 ();
	String [] test23 ();
}
";
			TestAidl (TestName, aidl, runtime);
		}
	}
}
