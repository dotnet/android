using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests
{
	[TestFixture]
	public class CustomWidgetTests
	{
		public CustomWidgetTests()
		{
			// FIXME: https://github.com/xamarin/xamarin-android/issues/9008
			new Mono.Android_Test.Library.Foo ();
		}

		// https://bugzilla.xamarin.com/show_bug.cgi?id=23880
		[Test]
		public void UpperCaseCustomWidget_ShouldNotThrowInflateException ()
		{
			Assert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService (Context.LayoutInflaterService);
				inflater.Inflate (Resource.Layout.uppercase_custom, null);
			}, "Regression test for widget with uppercase namespace (bug #23880) failed");
		}

		[Test]
		public void LowerCaseCustomWidget_ShouldNotThrowInflateException ()
		{
			Assert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService(Context.LayoutInflaterService);
				inflater.Inflate(Resource.Layout.lowercase_custom, null);
			}, "Regression test for widget with lowercase namespace (bug #23880) failed.");
		}

		[Test]
		public void UpperAndLowerCaseCustomWidget_FromLibrary_ShouldNotThrowInflateException ()
		{
			Assert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService (Context.LayoutInflaterService);
				inflater.Inflate (Resource.Layout.upper_lower_custom, null);
			}, "Regression test for widgets with uppercase and lowercase namespace (bug #23880) failed.");
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
