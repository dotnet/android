using System;
using System.Diagnostics.CodeAnalysis;
using Android.App;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Runtime;
using NUnit.Framework;

namespace Android.GraphicsTests
{
	// https://bugzilla.xamarin.com/show_bug.cgi?id=23823
	[TestFixture]
	public class NinePatchTests
	{
		object[] NinePatchDrawables = {
			new object[] {
				Xamarin.Android.RuntimeTests.Resource.Drawable.image,
				"image" // Drawable from App project.
			},
			new object[] {
				Mono.Android_Test.Library.Resource.Drawable.tile,
				"tile" // Drawable from Library project.
			}
		};

		[Test, TestCaseSource (nameof (NinePatchDrawables))]
		public void DrawableFromRes_ShouldBeTypeNinePatchDrawable (int resId, string name)
		{
			var d = Application.Context.Resources.GetDrawable (resId);
			Assert.IsNotNull (d, $"An image should have been retrieved from resource `{name}`.");
			Assert.IsNotNull (d as NinePatchDrawable, $"The drawable created from resource `{name}` should be a NinePatchDrawable.");
		}

		[Test, TestCaseSource (nameof (NinePatchDrawables))]
		public void DrawableFromResStream_ShouldBeTypeNinePatchDrawable (int resId, string name)
		{
			var value = new Android.Util.TypedValue ();
			InputStreamInvoker si = GetResourceStream (resId, value);
			var d = Drawable.CreateFromResourceStream (Application.Context.Resources, value, si, value.String.ToString (), null);
			Assert.IsNotNull (d, $"An image should have been retrieved from resource `{name}`.");
			Assert.IsNotNull (d as NinePatchDrawable, $"The drawable created from resource `{name}` should be a NinePatchDrawable.");
		}

		[Test, TestCaseSource (nameof (NinePatchDrawables))]
		public void BitmapFromDecodeRes_ShouldContainNinePatchChunk (int resId, string name)
		{
			Bitmap bm = BitmapFactory.DecodeResource (Application.Context.Resources, resId);
			byte[] chunk = bm.GetNinePatchChunk ();
			Assert.IsTrue (NinePatch.IsNinePatchChunk (chunk),
				$"Bitmap decoded from resource `{name}` did not contain a valid NinePatch chunk.");
		}

		[Test, TestCaseSource (nameof (NinePatchDrawables))]
		public void BitmapFromDecodeResStream_ShouldContainNinePatchChunk (int resId, string name)
		{
			var value = new Android.Util.TypedValue();
			InputStreamInvoker si = GetResourceStream (resId, value);
			Bitmap bm =  BitmapFactory.DecodeResourceStream (Application.Context.Resources, value, si, null, null);
			byte[] chunk = bm.GetNinePatchChunk ();
			Assert.IsTrue(NinePatch.IsNinePatchChunk (chunk),
				$"Bitmap decoded from resource stream with id `{name}` did not contain a valid NinePatch chunk.");
		}

		InputStreamInvoker GetResourceStream (int resId, Android.Util.TypedValue outValue)
		{
			Application.Context.Resources.GetValue (resId, outValue, true);
			IntPtr sp = OpenNonAsset(Application.Context.Resources.Assets, outValue.AssetCookie, outValue.String.ToString (), 2 /* AssetManager.ACCESS_STREAMING */);
			Java.IO.InputStream s = Java.Lang.Object.GetObject<Java.IO.InputStream> (sp, JniHandleOwnership.TransferLocalRef);
			return new InputStreamInvoker (s);
		}

		IntPtr AssetManager_openNonAsset;
		IntPtr OpenNonAsset (AssetManager manager, int cookie, string fileName, int accessMode)
		{
			if (AssetManager_openNonAsset == IntPtr.Zero)
				AssetManager_openNonAsset = JNIEnv.GetMethodID (manager.Class.Handle, "openNonAsset", "(ILjava/lang/String;I)Ljava/io/InputStream;");

			using (var f = new Java.Lang.String (fileName)) {
				return JNIEnv.CallObjectMethod (manager.Handle, AssetManager_openNonAsset, new JValue (cookie), new JValue (f), new JValue (accessMode));
			}
		}
	}
}
