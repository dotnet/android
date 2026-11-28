using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.Android.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Android_Test.Library;

namespace Xamarin.Android.RuntimeTests
{
	[TestClass]
	public class CustomWidgetTests
	{
		readonly TestContext testContext;

		public CustomWidgetTests (TestContext testContext)
		{
			this.testContext = testContext;
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
		[Timeout (60_000, CooperativeCancellation = true)]
		public async Task InflateCustomView_ShouldNotLeakGlobalRefs ()
		{
			var inflater = (LayoutInflater) Application.Context.GetSystemService (Context.LayoutInflaterService);
			Assert.IsNotNull (inflater);

			// Warm up: inflate once to populate caches and type mappings,
			// and let any background thread activity from previous tests settle.
			inflater.Inflate (Resource.Layout.lowercase_custom, null);
			await Java.InteropTests.GCBridgeTestHelpers.CollectUnreachablePeersAsync (testContext.CancellationToken);

			// Background threads may create and delete unrelated global references while this
			// test runs, so only follow references created by this thread during inflation.
			using var references = new InflatedGlobalReferences ();
			const int inflateCount = 100;
			references.StartRecording ();
			for (int i = 0; i < inflateCount; i++) {
				inflater.Inflate (Resource.Layout.lowercase_custom, null);
			}
			references.StopRecording ();

			// A bridge round keeps a peer whose Java object has not been collected yet, so
			// concurrent activity can require more rounds before every inflated view is freed.
			// A real leak would leave >= 300 references (3 leaked refs per inflate).
			const int maxLeaked = 100;
			const int maxCollections = 10;
			int leaked = int.MaxValue;
			for (int i = 0; i < maxCollections && leaked > maxLeaked; i++) {
				await Java.InteropTests.GCBridgeTestHelpers.CollectUnreachablePeersAsync (testContext.CancellationToken);
				leaked = references.OutstandingReferenceCount;
			}
			Assert.IsTrue (leaked <= maxLeaked,
				$"Global reference leak detected: {leaked} tracked JNI references created while inflating {inflateCount} custom views are still outstanding after GC.");
		}

		// Tracks global references created on the recording thread, following the
		// global/weak handle replacements made by GC bridge processing on other threads.
		sealed class InflatedGlobalReferences : EventListener
		{
			readonly Lock gate = new ();
			readonly HashSet<ulong> outstanding = new ();
			int recordingThreadId = -1;

			public InflatedGlobalReferences ()
			{
				RuntimeEventSource.Initialize ();
				foreach (var source in EventSource.GetSources ()) {
					if (source.Name == RuntimeEventSource.ProviderName)
						EnableEvents (source, EventLevel.Verbose, RuntimeEventSource.GlobalReferenceKeyword);
				}
			}

			public int OutstandingReferenceCount {
				get {
					lock (gate)
						return outstanding.Count;
				}
			}

			public void StartRecording () => Volatile.Write (ref recordingThreadId, System.Environment.CurrentManagedThreadId);

			public void StopRecording () => Volatile.Write (ref recordingThreadId, -1);

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.Payload is not { Count: > 6 } payload || payload [0] is not ulong source)
					return;
				switch (eventData.EventId) {
				case RuntimeEventSource.GlobalReferenceCreatedEventId:
				case RuntimeEventSource.WeakGlobalReferenceCreatedEventId:
					if (payload [1] is not ulong handle)
						return;
					bool recorded = payload [6] is int threadId && threadId == Volatile.Read (ref recordingThreadId);
					lock (gate) {
						if (recorded || outstanding.Contains (source))
							outstanding.Add (handle);
					}
					break;
				case RuntimeEventSource.GlobalReferenceDeletedEventId:
				case RuntimeEventSource.WeakGlobalReferenceDeletedEventId:
					lock (gate)
						outstanding.Remove (source);
					break;
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
