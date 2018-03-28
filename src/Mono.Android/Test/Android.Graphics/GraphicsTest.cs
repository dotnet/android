using System;

using Android.Content;

using NUnit.Framework;

namespace Android.GraphicsTests
{
	[TestFixture]
	public class GraphicTest
	{
		[Test]
		public void NinePatch ()
		{
			var d = Android.App.Application.Context.Resources.GetDrawable (Xamarin.Android.RuntimeTests.Resource.Drawable.image);
			Assert.IsNotNull (d, "An image should have been retrieved.");
			Assert.IsNotNull (d as Android.Graphics.Drawables.NinePatchDrawable, "The image should be a NinePatchDrawable.");
		}
	}
}
