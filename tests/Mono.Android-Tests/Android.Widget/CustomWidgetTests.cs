using System.Diagnostics.CodeAnalysis;
using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using NUnit.Framework;
using Mono.Android_Test.Library;

namespace Xamarin.Android.RuntimeTests
{
	[TestFixture]
	public class CustomWidgetTests
	{
		// https://bugzilla.xamarin.com/show_bug.cgi?id=23880
		[Test]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, typeof (CustomTextView))]
		public void UpperCaseCustomWidget_ShouldNotThrowInflateException ()
		{
			Assert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService (Context.LayoutInflaterService);
				inflater.Inflate (Resource.Layout.uppercase_custom, null);
			}, "Regression test for widget with uppercase namespace (bug #23880) failed");
		}

		[Test]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, typeof (CustomTextView))]
		public void LowerCaseCustomWidget_ShouldNotThrowInflateException ()
		{
			Assert.DoesNotThrow (() => {
				var inflater = (LayoutInflater)Application.Context.GetSystemService(Context.LayoutInflaterService);
				inflater.Inflate(Resource.Layout.lowercase_custom, null);
			}, "Regression test for widget with lowercase namespace (bug #23880) failed.");
		}

		[Test]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, typeof (CustomTextView))]
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
