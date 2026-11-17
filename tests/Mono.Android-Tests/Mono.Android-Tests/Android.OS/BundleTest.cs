using System;
using System.Collections.Generic;
using System.Threading;
using Android.OS;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.RuntimeTests {

	[TestClass]
	public class BundleTest 
	{
		[TestMethod]
		public void TestBundleIntegerArrayList()
		{
			var b = new Bundle();
			b.PutIntegerArrayList("key", new List<Java.Lang.Integer>() { Java.Lang.Integer.ValueOf(1) });
			var list = b.GetIntegerArrayList ("key");
			Assert.IsNotNull (list, "'key' doesn't refer to a list of integers");
			var obj = b.Get ("key");
			Assert.IsNotNull (obj, "Missing 'key' in bundle");
			Assert.IsTrue (obj is global::Android.Runtime.JavaList, "`obj` should be a JavaList!");
			try {
				list = b.GetIntegerArrayList ("key");
				Assert.IsNotNull (list, "'key' doesn't refer to a list of integers after non-generic call");
			} catch (Exception) {
				Assert.Fail ("Java.Lang.Object caches too aggresively");
			}
		}

		[TestMethod]
		public void TestBundleIntegerArrayList2 ()
		{
			var b = new Bundle();
			b.PutIntegerArrayList ("key", new List<Java.Lang.Integer> () { Java.Lang.Integer.ValueOf (1) });
			var obj = b.Get ("key");
			Assert.IsNotNull (obj, "Missing 'key' in bundle");
			try {
				var list = b.GetIntegerArrayList ("key");
				Assert.IsNotNull (list, "'key' doesn't refer to a list of integers");
			} catch (Exception) {
				Assert.Fail ("Java.Lang.Object caches too aggresively");
			}
		}
	}
}
