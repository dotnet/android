using System;
using System.Collections.Generic;
using System.Threading;
using Android.OS;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class BundleTest 
	{
		[Test]
		public void TestBundleIntegerArrayList()
		{
			var b = new Bundle();
			b.PutIntegerArrayList("key", new List<Java.Lang.Integer>() { Java.Lang.Integer.ValueOf(1) });
			var list = b.GetIntegerArrayList ("key");
			Assert.NotNull (list, "'key' doesn't refer to a list of integers");
			var obj = b.Get ("key");
			Assert.NotNull (obj, "Missing 'key' in bundle");
			Assert.IsTrue (obj is global::Android.Runtime.JavaList, "`obj` should be a JavaList!");
			try {
				list = b.GetIntegerArrayList ("key");
				Assert.NotNull (list, "'key' doesn't refer to a list of integers after non-generic call");
			} catch (Exception e) {
				Assert.Fail ("Java.Lang.Object caches too aggresively");
			}
		}

		[Test]
		public void TestBundleIntegerArrayList2 ()
		{
			var b = new Bundle();
			b.PutIntegerArrayList ("key", new List<Java.Lang.Integer> () { Java.Lang.Integer.ValueOf (1) });
			var obj = b.Get ("key");
			Assert.NotNull (obj, "Missing 'key' in bundle");
			try {
				var list = b.GetIntegerArrayList ("key");
				Assert.NotNull (list, "'key' doesn't refer to a list of integers");
			} catch (Exception e) {
				Assert.Fail ("Java.Lang.Object caches too aggresively");
			}
		}
	}
}
