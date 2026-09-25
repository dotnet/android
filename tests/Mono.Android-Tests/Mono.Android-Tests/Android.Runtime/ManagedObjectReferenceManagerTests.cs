using System;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class ManagedObjectReferenceManagerTests {
		sealed class TrackingWriter : StringWriter {
			public int DisposeCount { get; private set; }

			protected override void Dispose (bool disposing)
			{
				DisposeCount++;
				base.Dispose (disposing);
			}
		}

		static global::Android.Runtime.ManagedObjectReferenceManager CreateManager (TextWriter? grefLog = null, TextWriter? lrefLog = null)
		{
			return new global::Android.Runtime.ManagedObjectReferenceManager (grefLog, lrefLog, false, false);
		}

		static global::Android.Runtime.ReferenceLogEvent GetEvent (string name)
		{
			return name switch {
				"GlobalCreated"      => global::Android.Runtime.ReferenceLogEvent.GlobalCreated,
				"GlobalDeleted"      => global::Android.Runtime.ReferenceLogEvent.GlobalDeleted,
				"WeakGlobalCreated"  => global::Android.Runtime.ReferenceLogEvent.WeakGlobalCreated,
				"WeakGlobalDeleted"  => global::Android.Runtime.ReferenceLogEvent.WeakGlobalDeleted,
				_                    => throw new ArgumentOutOfRangeException (nameof (name)),
			};
		}

		static void AssertReferenceLogFileMode (string path)
		{
			if (OperatingSystem.IsWindows ()) {
				return;
			}

			Assert.AreEqual (
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead,
				File.GetUnixFileMode (path));
		}

		[TestCase ("GlobalCreated", "+g+ grefc 2 gwrefc 3 obj-handle 0x1234/L -> new-handle 0x5678/G from thread 'worker'(42)")]
		[TestCase ("GlobalDeleted", "-g- grefc 2 gwrefc 3 handle 0x1234/G from thread 'worker'(42)")]
		[TestCase ("WeakGlobalCreated", "+w+ grefc 2 gwrefc 3 obj-handle 0x1234/G -> new-handle 0x5678/W from thread 'worker'(42)")]
		[TestCase ("WeakGlobalDeleted", "-w- grefc 2 gwrefc 3 handle 0x1234/W from thread 'worker'(42)")]
		public void FormatsReferenceMessages (string eventName, string expected)
		{
			byte currentType = eventName == "GlobalCreated" ? (byte) 'L' :
				eventName == "WeakGlobalCreated" ? (byte) 'G' :
				eventName == "WeakGlobalDeleted" ? (byte) 'W' :
				(byte) 'G';
			byte newType = eventName == "WeakGlobalCreated" ? (byte) 'W' : (byte) 'G';

			string result = global::Android.Runtime.ManagedObjectReferenceManager.FormatReferenceMessage (
				GetEvent (eventName),
				2,
				3,
				new IntPtr (0x1234),
				currentType,
				new IntPtr (0x5678),
				newType,
				"worker",
				42);

			Assert.AreEqual (expected, result);
		}

		[Test]
		public void FormatsUnnamedThreadWithNullMarker ()
		{
			string result = global::Android.Runtime.ManagedObjectReferenceManager.FormatReferenceMessage (
				global::Android.Runtime.ReferenceLogEvent.GlobalDeleted,
				2,
				3,
				new IntPtr (0x1234),
				(byte) 'G',
				IntPtr.Zero,
				(byte) 'I',
				null,
				42);

			Assert.AreEqual ("-g- grefc 2 gwrefc 3 handle 0x1234/G from thread '<null>'(42)", result);
		}

		[Test]
		public void CountsGlobalAndWeakReferencesIndependently ()
		{
			var manager = CreateManager ();

			manager.LogReference (global::Android.Runtime.ReferenceLogEvent.GlobalCreated, IntPtr.Zero, (byte) 'L', new IntPtr (1), (byte) 'G', "", 1, null);
			manager.LogReference (global::Android.Runtime.ReferenceLogEvent.WeakGlobalCreated, new IntPtr (1), (byte) 'G', new IntPtr (2), (byte) 'W', "", 1, null);
			manager.LogReference (global::Android.Runtime.ReferenceLogEvent.GlobalCreated, IntPtr.Zero, (byte) 'L', new IntPtr (3), (byte) 'G', "", 1, null);
			manager.LogReference (global::Android.Runtime.ReferenceLogEvent.GlobalDeleted, new IntPtr (1), (byte) 'G', IntPtr.Zero, (byte) 'I', "", 1, null);

			Assert.AreEqual (1, manager.GlobalReferenceCount);
			Assert.AreEqual (1, manager.WeakGlobalReferenceCount);
		}

		[Test]
		public void UpdatesCountsWithoutFormattingMetadata ()
		{
			var manager = CreateManager ();
			int globalCount = manager.UpdateReferenceCount (global::Android.Runtime.ReferenceLogEvent.GlobalCreated, out int weakCount);

			Assert.AreEqual (1, globalCount);
			Assert.AreEqual (0, weakCount);
		}

		[Test]
		public void InitializesMaxGrefCountsFromArguments ()
		{
			var args = new global::Android.Runtime.JNIEnvInit.JnienvInitializeArgs {
				grefGcThreshold = 1800,
				maxGrefCount = 2000,
			};
			int previousThreshold = global::Android.Runtime.JNIEnvInit.gref_gc_threshold;
			int previousMaximum = global::Android.Runtime.JNIEnvInit.max_gref_count;

			try {
				global::Android.Runtime.JNIEnvInit.InitializeMaxGrefCounts (args);

				Assert.AreEqual (1800, global::Android.Runtime.JNIEnvInit.gref_gc_threshold);
				Assert.AreEqual (2000, global::Android.Runtime.JNIEnvInit.max_gref_count);
			} finally {
				global::Android.Runtime.JNIEnvInit.gref_gc_threshold = previousThreshold;
				global::Android.Runtime.JNIEnvInit.max_gref_count = previousMaximum;
			}
		}

		[Test]
		public void CreatesExplicitAndDefaultLogFiles ()
		{
			string directory = Path.Combine (Path.GetTempPath (), "xa-reference-log-" + Guid.NewGuid ().ToString ("N"));
			string explicitPath = Path.Combine (directory, "custom.txt");
			Directory.CreateDirectory (directory);

			try {
				var explicitWriter = global::Android.Runtime.ManagedObjectReferenceManager.CreateLogWriter (true, false, explicitPath, directory, "fallback.txt", "test");
				Assert.IsNotNull (explicitWriter);
				explicitWriter?.WriteLine ("explicit");
				explicitWriter?.Dispose ();
				Assert.AreEqual ("explicit" + Environment.NewLine, File.ReadAllText (explicitPath));
				AssertReferenceLogFileMode (explicitPath);

				var defaultWriter = global::Android.Runtime.ManagedObjectReferenceManager.CreateLogWriter (true, false, null, directory, "fallback.txt", "test");
				Assert.IsNotNull (defaultWriter);
				defaultWriter?.WriteLine ("default");
				defaultWriter?.Dispose ();
				string fallbackPath = Path.Combine (directory, "fallback.txt");
				Assert.AreEqual ("default" + Environment.NewLine, File.ReadAllText (fallbackPath));
				AssertReferenceLogFileMode (fallbackPath);

				Assert.IsNull (global::Android.Runtime.ManagedObjectReferenceManager.CreateLogWriter (true, true, explicitPath, directory, "fallback.txt", "test"));
			} finally {
				if (File.Exists (explicitPath)) {
					File.Delete (explicitPath);
				}
				string fallbackPath = Path.Combine (directory, "fallback.txt");
				if (File.Exists (fallbackPath)) {
					File.Delete (fallbackPath);
				}
				if (Directory.Exists (directory)) {
					Directory.Delete (directory);
				}
			}
		}

		[Test]
		public void DisposesDistinctAndSharedLogWriters ()
		{
			var grefWriter = new TrackingWriter ();
			var lrefWriter = new TrackingWriter ();
			var manager = (IDisposable) CreateManager (grefWriter, lrefWriter);

			manager.Dispose ();

			Assert.AreEqual (1, grefWriter.DisposeCount);
			Assert.AreEqual (1, lrefWriter.DisposeCount);

			var sharedWriter = new TrackingWriter ();
			manager = (IDisposable) CreateManager (sharedWriter, sharedWriter);

			manager.Dispose ();

			Assert.AreEqual (1, sharedWriter.DisposeCount);
		}
	}
}
