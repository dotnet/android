#nullable enable

using System;

using Android.Animation;
using Android.App;
using Android.Graphics;
using Android.Text;
using Android.Widget;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.RuntimeTests;

[TestClass]
[TestCategory ("HandwrittenJniBindings")]
public class HandwrittenBindingSmokeTests
{
	[TestMethod]
	public void ValueAnimatorEventDispatcherReusesSingleListener ()
	{
		EventHandler onStart = (_, _) => { };
		EventHandler onEnd = (_, _) => { };

		using var animator = ValueAnimator.OfInt (0, 1);
		Assert.IsNotNull (animator);

		animator.AnimationStart += onStart;
		animator.AnimationEnd += onEnd;
		Assert.IsNotNull (animator.Listeners);
		Assert.AreEqual (1, animator.Listeners.Count);

		animator.AnimationStart -= onStart;
		animator.AnimationEnd -= onEnd;
	}

	[TestMethod]
	public void BitmapInfoLockAndUnlockPixelsSucceed ()
	{
		var config = Bitmap.Config.Argb8888;
		Assert.IsNotNull (config);

		using var bitmap = Bitmap.CreateBitmap (4, 4, config);
		Assert.IsNotNull (bitmap);

		var info = bitmap.GetBitmapInfo ();
		Assert.AreEqual (4u, info.Width);
		Assert.AreEqual (4u, info.Height);

		IntPtr pixels = bitmap.LockPixels ();
		Assert.AreNotEqual (IntPtr.Zero, pixels);
		bitmap.UnlockPixels ();
	}

	[TestMethod]
	public void DirectByteBufferAddressIsNonZero ()
	{
		using var direct = Java.Nio.ByteBuffer.AllocateDirect (16);
		Assert.IsNotNull (direct);
		Assert.AreNotEqual (IntPtr.Zero, direct.GetDirectBufferAddress ());

		using var heap = Java.Nio.ByteBuffer.Allocate (16);
		Assert.IsNotNull (heap);
		Assert.AreEqual (IntPtr.Zero, heap.GetDirectBufferAddress ());
	}

	[TestMethod]
	public void TextViewTextEventsShareWatcherAndFire ()
	{
		int beforeCount = 0;
		int changedCount = 0;
		int afterCount = 0;

		EventHandler<TextChangedEventArgs> before = (_, _) => beforeCount++;
		EventHandler<TextChangedEventArgs> changed = (_, _) => changedCount++;
		EventHandler<AfterTextChangedEventArgs> after = (_, _) => afterCount++;

		using var view = new TextView (Application.Context);
		view.BeforeTextChanged += before;
		view.TextChanged += changed;
		view.AfterTextChanged += after;

		view.Text = "hello";
		Assert.AreEqual (1, beforeCount);
		Assert.AreEqual (1, changedCount);
		Assert.AreEqual (1, afterCount);

		view.BeforeTextChanged -= before;
		view.TextChanged -= changed;
		view.AfterTextChanged -= after;

		view.Text = "world";
		Assert.AreEqual (1, beforeCount);
		Assert.AreEqual (1, changedCount);
		Assert.AreEqual (1, afterCount);
	}
}
