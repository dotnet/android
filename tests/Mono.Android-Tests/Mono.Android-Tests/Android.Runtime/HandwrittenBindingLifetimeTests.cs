#nullable enable

using System;
using System.Threading;

using Android.Animation;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Widget;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.RuntimeTests;

[TestClass]
[TestCategory ("HandwrittenJniBindings")]
public class HandwrittenBindingLifetimeTests
{
	static void RunUnderConcurrentGC (int iterations, Action<int> body)
	{
		using var done = new CancellationTokenSource ();
		var collector = new Thread (() => {
			while (!done.IsCancellationRequested) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				Thread.Yield ();
			}
		});
		collector.Start ();

		try {
			for (int i = 0; i < iterations; i++) {
				body (i);
			}
		} finally {
			done.Cancel ();
			collector.Join ();
		}
	}

	[TestMethod]
	public void BitmapPixelAccessSurvivesConcurrentCollection ()
	{
		var config = Bitmap.Config.Argb8888;
		Assert.IsNotNull (config);

		RunUnderConcurrentGC (200, _ => {
			Assert.AreEqual (4u, CreateBitmap (config).GetBitmapInfo ().Width);

			var bitmap = CreateBitmap (config);
			IntPtr pixels = bitmap.LockPixels ();
			Assert.AreNotEqual (IntPtr.Zero, pixels);
			bitmap.UnlockPixels ();
		});
	}

	static Bitmap CreateBitmap (Bitmap.Config config)
	{
		return Bitmap.CreateBitmap (4, 4, config) ?? throw new AssertFailedException ("Bitmap.CreateBitmap returned null.");
	}

	[TestMethod]
	public void JavaLangStringToStringSurvivesConcurrentCollection ()
	{
		RunUnderConcurrentGC (500, i => {
			string expected = $"value-{i}";
			string actual = new Java.Lang.String (expected).ToString ();
			Assert.AreEqual (expected, actual);
		});
	}

	[TestMethod]
	public void ContentValuesPutAndGetSurviveConcurrentCollection ()
	{
		RunUnderConcurrentGC (200, i => {
			var values = new ContentValues ();
			values.Put ("bool", (i & 1) == 0);
			values.Put ("byte", (sbyte) (i & 0x7f));
			values.Put ("short", (short) i);
			values.Put ("int", i);
			values.Put ("long", (long) i);
			values.Put ("float", (float) i);
			values.Put ("double", (double) i);

			Assert.AreEqual ((i & 1) == 0, values.GetAsBoolean ("bool"));
			Assert.AreEqual ((sbyte) (i & 0x7f), values.GetAsByte ("byte"));
			Assert.AreEqual ((short) i, values.GetAsShort ("short"));
			Assert.AreEqual (i, values.GetAsInteger ("int"));
			Assert.AreEqual ((long) i, values.GetAsLong ("long"));
			Assert.AreEqual ((float) i, values.GetAsFloat ("float"));
			Assert.AreEqual ((double) i, values.GetAsDouble ("double"));
		});
	}

	[TestMethod]
	public void SparseArrayMethodsSurviveConcurrentCollection ()
	{
		RunUnderConcurrentGC (200, i => {
			var array = new SparseArray<Java.Lang.String> ();
			using var first = new Java.Lang.String ($"first-{i}");
			using var second = new Java.Lang.String ($"second-{i}");
			using var fallback = new Java.Lang.String ("fallback");
			using var updated = new Java.Lang.String ($"updated-{i}");

			array.Append (1, first);
			array.Put (2, second);
			Assert.AreEqual (first.ToString (), array.Get (1)?.ToString ());
			Assert.AreEqual (fallback.ToString (), array.Get (99, fallback)?.ToString ());
			Assert.AreEqual (1, array.IndexOfValue (second));
			array.SetValueAt (1, updated);
			Assert.AreEqual (updated.ToString (), array.ValueAt (1)?.ToString ());
		});
	}

	[TestMethod]
	public void DirectBufferAddressSurvivesConcurrentCollection ()
	{
		RunUnderConcurrentGC (500, _ => {
			var buffer = Java.Nio.ByteBuffer.AllocateDirect (16);
			Assert.IsNotNull (buffer);
			IntPtr address = buffer.GetDirectBufferAddress ();
			Assert.AreNotEqual (IntPtr.Zero, address);
		});
	}

	[TestMethod]
	public void NotificationVibrateSurvivesConcurrentCollection ()
	{
		long [] pattern = [0, 50, 100];
		RunUnderConcurrentGC (200, _ => {
			new Notification ().Vibrate = pattern;
			var notification = new Notification ();
			notification.Vibrate = pattern;
			CollectionAssert.AreEqual (pattern, notification.Vibrate);
		});
	}

	[TestMethod]
	public void AnimatorEventDispatcherSurvivesConcurrentCollection ()
	{
		EventHandler handler = (_, _) => { };
		RunUnderConcurrentGC (200, _ => {
			var animator = ValueAnimator.OfInt (0, 1);
			Assert.IsNotNull (animator);
			animator.AnimationStart += handler;
			animator.AnimationEnd += handler;
			animator.AnimationCancel += handler;
			animator.AnimationRepeat += handler;
			animator.AnimationStart -= handler;
		});
	}

	[TestMethod]
	public void ArrayAdapterMethodsSurviveConcurrentCollection ()
	{
		RunUnderConcurrentGC (100, i => {
			var adapter = new ArrayAdapter<string> (Application.Context, Android.Resource.Layout.SimpleListItem1);
			string first = $"first-{i}";
			string second = $"second-{i}";
			adapter.Add (first);
			adapter.Insert (second, 0);
			Assert.AreEqual (second, adapter.GetItem (0));
			Assert.AreEqual (1, adapter.GetPosition (first));
			adapter.Remove (first);
		});
	}

#pragma warning disable CA1422 // AsyncTask is obsoleted on API 30+, intentionally tested here.
	[TestMethod]
	public void AsyncTaskExecuteAndGetResultSurviveConcurrentCollection ()
	{
		RunUnderConcurrentGC (50, i => {
			string expected = $"task-{i}";
			var executed = new EchoAsyncTask ().Execute (expected);
			Assert.IsNotNull (executed);
			Assert.AreEqual (expected, executed.GetResult ());
		});
	}

	sealed class EchoAsyncTask : AsyncTask<string, string, string>
	{
		protected override string RunInBackground (params string [] @params)
		{
			return @params.Length > 0 ? @params [0] : "";
		}
	}
#pragma warning restore CA1422
}
