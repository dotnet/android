using System;
using System.Globalization;
using System.IO;
using System.Reflection;

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

		static Type ManagerType =>
			typeof (global::Android.Runtime.AndroidEnvironment).Assembly.GetType ("Android.Runtime.ManagedObjectReferenceManager", throwOnError: true)
				?? throw new InvalidOperationException ("ManagedObjectReferenceManager type was not found.");

		static Type EventType =>
			typeof (global::Android.Runtime.AndroidEnvironment).Assembly.GetType ("Android.Runtime.ReferenceLogEvent", throwOnError: true)
				?? throw new InvalidOperationException ("ReferenceLogEvent type was not found.");

		static MethodInfo GetMethod (string name)
		{
			return ManagerType.GetMethod (name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new InvalidOperationException ($"{name} method was not found.");
		}

		static object CreateManager (TextWriter? grefLog = null, TextWriter? lrefLog = null)
		{
			return Activator.CreateInstance (
				ManagerType,
				BindingFlags.Instance | BindingFlags.NonPublic,
				binder: null,
				args: [grefLog, lrefLog, false, false],
				culture: CultureInfo.InvariantCulture)
				?? throw new InvalidOperationException ("ManagedObjectReferenceManager could not be created.");
		}

		static object GetEvent (string name)
		{
			return Enum.Parse (EventType, name);
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

			object? result = GetMethod ("FormatReferenceMessage").Invoke (
				obj: null,
				parameters: [
					GetEvent (eventName),
					2,
					3,
					new IntPtr (0x1234),
					currentType,
					new IntPtr (0x5678),
					newType,
					"worker",
					42,
				]);

			Assert.AreEqual (expected, result);
		}

		[Test]
		public void FormatsUnnamedThreadWithNullMarker ()
		{
			object? result = GetMethod ("FormatReferenceMessage").Invoke (
				obj: null,
				parameters: [
					GetEvent ("GlobalDeleted"),
					2,
					3,
					new IntPtr (0x1234),
					(byte) 'G',
					IntPtr.Zero,
					(byte) 'I',
					null,
					42,
				]);

			Assert.AreEqual ("-g- grefc 2 gwrefc 3 handle 0x1234/G from thread '<null>'(42)", result);
		}

		[Test]
		public void CountsGlobalAndWeakReferencesIndependently ()
		{
			object manager = CreateManager ();
			MethodInfo logReference = GetMethod ("LogReference");

			logReference.Invoke (manager, [GetEvent ("GlobalCreated"), IntPtr.Zero, (byte) 'L', new IntPtr (1), (byte) 'G', "", 1, null]);
			logReference.Invoke (manager, [GetEvent ("WeakGlobalCreated"), new IntPtr (1), (byte) 'G', new IntPtr (2), (byte) 'W', "", 1, null]);
			logReference.Invoke (manager, [GetEvent ("GlobalCreated"), IntPtr.Zero, (byte) 'L', new IntPtr (3), (byte) 'G', "", 1, null]);
			logReference.Invoke (manager, [GetEvent ("GlobalDeleted"), new IntPtr (1), (byte) 'G', IntPtr.Zero, (byte) 'I', "", 1, null]);

			Assert.AreEqual (1, ManagerType.GetProperty ("GlobalReferenceCount")?.GetValue (manager));
			Assert.AreEqual (1, ManagerType.GetProperty ("WeakGlobalReferenceCount")?.GetValue (manager));
		}

		[Test]
		public void UpdatesCountsWithoutFormattingMetadata ()
		{
			object manager = CreateManager ();
			MethodInfo updateReferenceCount = GetMethod ("UpdateReferenceCount");
			object?[] arguments = [GetEvent ("GlobalCreated"), 0];

			object? globalCount = updateReferenceCount.Invoke (manager, arguments);

			Assert.AreEqual (1, globalCount);
			Assert.AreEqual (0, arguments [1]);
		}

		[Test]
		public void CreatesExplicitAndDefaultLogFiles ()
		{
			string directory = Path.Combine (Path.GetTempPath (), "xa-reference-log-" + Guid.NewGuid ().ToString ("N"));
			string explicitPath = Path.Combine (directory, "custom.txt");
			Directory.CreateDirectory (directory);

			try {
				MethodInfo createLogWriter = GetMethod ("CreateLogWriter");
				var explicitWriter = createLogWriter.Invoke (null, [true, false, explicitPath, directory, "fallback.txt", "test"]) as TextWriter;
				Assert.IsNotNull (explicitWriter);
				explicitWriter?.WriteLine ("explicit");
				explicitWriter?.Dispose ();
				Assert.AreEqual ("explicit" + Environment.NewLine, File.ReadAllText (explicitPath));
				AssertReferenceLogFileMode (explicitPath);

				var defaultWriter = createLogWriter.Invoke (null, [true, false, null, directory, "fallback.txt", "test"]) as TextWriter;
				Assert.IsNotNull (defaultWriter);
				defaultWriter?.WriteLine ("default");
				defaultWriter?.Dispose ();
				string fallbackPath = Path.Combine (directory, "fallback.txt");
				Assert.AreEqual ("default" + Environment.NewLine, File.ReadAllText (fallbackPath));
				AssertReferenceLogFileMode (fallbackPath);

				Assert.IsNull (createLogWriter.Invoke (null, [true, true, explicitPath, directory, "fallback.txt", "test"]));
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
