using System;
using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Android_Test.Library;

namespace Xamarin.Android.RuntimeTests
{
	[TestClass]
	public class CustomWidgetTests
	{
		public CustomWidgetTests()
		{
			// FIXME: https://github.com/dotnet/android/issues/9008
			new Foo ();
		}

		// https://bugzilla.xamarin.com/show_bug.cgi?id=23880
		[TestMethod]
		public void UpperCaseCustomWidget_ShouldNotThrowInflateException ()
		{
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService (Context.LayoutInflaterService);
				inflater.Inflate (Resource.Layout.uppercase_custom, null);
			}, "Regression test for widget with uppercase namespace (bug #23880) failed");
		}

		[TestMethod]
		public void LowerCaseCustomWidget_ShouldNotThrowInflateException ()
		{
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService(Context.LayoutInflaterService);
				inflater.Inflate(Resource.Layout.lowercase_custom, null);
			}, "Regression test for widget with lowercase namespace (bug #23880) failed.");
		}

		[TestMethod]
		public void UpperAndLowerCaseCustomWidget_FromLibrary_ShouldNotThrowInflateException ()
		{
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService (Context.LayoutInflaterService);
				inflater.Inflate (Resource.Layout.upper_lower_custom, null);
			}, "Regression test for widgets with uppercase and lowercase namespace (bug #23880) failed.");
		}

		// https://github.com/dotnet/android/issues/11101
		[TestMethod]
		[Ignore ("Currently failing/flaky due to unresolved custom view global ref leak: https://github.com/dotnet/android/issues/11201")]
		public void InflateCustomView_ShouldNotLeakGlobalRefs ()
		{
			var inflater = (LayoutInflater) Application.Context.GetSystemService (Context.LayoutInflaterService);
			Assert.IsNotNull (inflater);

			// Warm up: inflate once to populate caches and type mappings,
			// and let any background thread activity from previous tests settle.
			inflater.Inflate (Resource.Layout.lowercase_custom, null);
			CollectGarbage (times: 3);

			int grefBefore = Java.Interop.Runtime.GlobalReferenceCount;

			// Use a large number of inflations so that a real leak (3+ global refs
			// per inflate) produces a delta far above any background noise from
			// Android system services, GC bridge processing, or finalizer threads.
			const int inflateCount = 100;
			for (int i = 0; i < inflateCount; i++) {
				inflater.Inflate (Resource.Layout.lowercase_custom, null);
			}

			CollectGarbage (times: 3);

			int grefAfter = Java.Interop.Runtime.GlobalReferenceCount;
			int delta = grefAfter - grefBefore;

			// A real leak would produce delta >= 300 (3 leaked refs per inflate).
			// Use a generous threshold to tolerate background noise on real devices.
			Assert.IsTrue (delta <= 100,
				$"Global reference leak detected: {delta} extra global refs after inflating/GC'ing {inflateCount} custom views. Before={grefBefore}, After={grefAfter}");

			static void CollectGarbage (int times)
			{
				for (int i = 0; i < times; i++) {
					GC.Collect ();
					GC.WaitForPendingFinalizers ();
				}
			}
		}
	}

	public class CustomButton : Button
	{
		public CustomButton (Context context) : base (context)
		{
		}

		public CustomButton (Context context, IAttributeSet attributes) : base (context, attributes)
		{
		}
	}
}
