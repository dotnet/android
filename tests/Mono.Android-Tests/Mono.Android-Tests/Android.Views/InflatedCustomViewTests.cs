using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Java.Interop;
using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests;

// https://github.com/dotnet/android/issues/11101
[TestFixture]
public class InflatedCustomViewTests
{
	[Test]
	public void InflatedCustomView_HasValidPeerReference ()
	{
		var inflater = LayoutInflater.From (Application.Context)!;
		var layout = inflater.Inflate (Resource.Layout.inflated_custom_view, null, false)!;

		// Find our custom view in the inflated layout
		var customView = FindCustomView (layout);

		Assert.IsNotNull (customView, "Custom view should be found in inflated layout");

		// After inflation via Java-initiated activation, the peer should have a
		// properly managed global JNI reference, not a raw local ref with Invalid type.
		var peerRef = customView!.PeerReference;
		Assert.IsTrue (peerRef.IsValid, "PeerReference should be valid");
		Assert.AreNotEqual (
			JniObjectReferenceType.Invalid,
			peerRef.Type,
			"PeerReference.Type should not be Invalid — it should be a Global ref");

		// The peer should be registered so PeekObject can find it
		var peeked = Java.Lang.Object.PeekObject (customView.Handle);
		Assert.IsNotNull (peeked, "PeekObject should find the registered peer");
		Assert.AreSame (customView, peeked, "PeekObject should return the same instance");
	}

	[Test]
	public void InflatedCustomView_CanBeCollected ()
	{
		WeakReference? weakRef = null;

		// Create and discard the inflated view on a separate thread
		// to avoid any local variable keeping it alive
		var t = new System.Threading.Thread (() => {
			var inflater = LayoutInflater.From (Application.Context)!;
			var layout = inflater.Inflate (Resource.Layout.inflated_custom_view, null, false)!;
			var customView = FindCustomView (layout);
			Assert.IsNotNull (customView, "Custom view should be found in inflated layout");
			weakRef = new WeakReference (customView);
		});
		t.Start ();
		t.Join ();

		// Force GC + bridge processing
		GC.Collect ();
		GC.WaitForPendingFinalizers ();
		GC.Collect ();
		GC.WaitForPendingFinalizers ();

		Assert.IsNotNull (weakRef, "WeakReference should have been created");
		Assert.IsFalse (weakRef!.IsAlive,
			"Custom view should be collected after GC — if it's still alive, there is a memory leak (https://github.com/dotnet/android/issues/11101)");
	}

	// Stress test: repeated inflation + GC to trigger the race condition
	// between Activate.SetPeerReference and ConstructPeer. Under the bug,
	// each race hit leaks a JNI global ref, so gref count grows unboundedly.
	[Test]
	public void InflatedCustomView_RepeatedInflation_DoesNotLeakGlobalRefs ()
	{
		int initialGrefCount = Java.Interop.JniEnvironment.Runtime.GlobalReferenceCount;

		for (int i = 0; i < 50; i++) {
			var t = new System.Threading.Thread (() => {
				var inflater = LayoutInflater.From (Application.Context)!;
				inflater.Inflate (Resource.Layout.inflated_custom_view, null, false);
			});
			t.Start ();
			t.Join ();
		}

		GC.Collect ();
		GC.WaitForPendingFinalizers ();
		GC.Collect ();
		GC.WaitForPendingFinalizers ();

		int finalGrefCount = Java.Interop.JniEnvironment.Runtime.GlobalReferenceCount;

		// Allow some tolerance — other code may allocate/release grefs.
		// The key assertion is that gref count doesn't grow proportionally
		// to the number of inflations (under the bug, each inflation
		// leaks ~1 gref, so 50 inflations would leak ~50 grefs).
		int leaked = finalGrefCount - initialGrefCount;
		Assert.Less (leaked, 10,
			$"Global reference count grew by {leaked} after 50 inflations — " +
			$"expected near-zero growth after GC (initial={initialGrefCount}, final={finalGrefCount})");
	}

	static InflatedCustomView? FindCustomView (View root)
	{
		if (root is InflatedCustomView customView)
			return customView;

		if (root is ViewGroup viewGroup) {
			for (int i = 0; i < viewGroup.ChildCount; i++) {
				var child = viewGroup.GetChildAt (i);
				if (child is InflatedCustomView found)
					return found;
			}
		}

		return null;
	}
}

// A simple custom view that can be inflated from XML
public sealed class InflatedCustomView : View
{
	public InflatedCustomView (Context? context) : base (context) { }
	public InflatedCustomView (nint javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) { }
	public InflatedCustomView (Context? context, IAttributeSet? attrs) : base (context, attrs) { }
	public InflatedCustomView (Context? context, IAttributeSet? attrs, int defStyleAttr) : base (context, attrs, defStyleAttr) { }
	public InflatedCustomView (Context? context, IAttributeSet? attrs, int defStyleAttr, int defStyleRes) : base (context, attrs, defStyleAttr, defStyleRes) { }
}
