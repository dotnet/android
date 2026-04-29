// User-type test fixture assembly that references REAL Mono.Android.
// Exercises edge cases that MCW binding assemblies don't have:
// - User types extending Java peers without [Register]
// - Component attributes ([Activity], [Service], etc.)
// - [Export] methods
// - Nested user types
// - Generic user types

using System;
using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Runtime;
using Java.Interop;

[assembly: SupportedOSPlatform ("android21.0")]

namespace UserApp
{
	[Activity (Name = "com.example.userapp.MainActivity", MainLauncher = true, Label = "User App")]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Android.OS.Bundle? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
		}
	}

	// Activity WITHOUT explicit Name — should get CRC64-based JNI name
	[Activity (Label = "Settings")]
	public class SettingsActivity : Activity
	{
	}

	// Simple Activity subclass — no attributes at all, just extends a Java peer
	public class PlainActivity : Activity
	{
	}
}

namespace UserApp.Services
{
	[Service (Name = "com.example.userapp.MyBackgroundService")]
	public class MyBackgroundService : Android.App.Service
	{
		public override Android.OS.IBinder? OnBind (Android.Content.Intent? intent) => null;
	}

	// Service without explicit Name
	[Service]
	public class UnnamedService : Android.App.Service
	{
		public override Android.OS.IBinder? OnBind (Android.Content.Intent? intent) => null;
	}
}

namespace UserApp.Receivers
{
	[BroadcastReceiver (Name = "com.example.userapp.BootReceiver", Exported = false)]
	public class BootReceiver : BroadcastReceiver
	{
		public override void OnReceive (Context? context, Intent? intent)
		{
		}
	}
}

namespace UserApp
{
	public class MyBackupAgent : Android.App.Backup.BackupAgent
	{
		public override void OnBackup (Android.OS.ParcelFileDescriptor? oldState,
			Android.App.Backup.BackupDataOutput? data,
			Android.OS.ParcelFileDescriptor? newState)
		{
		}

		public override void OnRestore (Android.App.Backup.BackupDataInput? data,
			int appVersionCode,
			Android.OS.ParcelFileDescriptor? newState)
		{
		}
	}

	[Application (Name = "com.example.userapp.MyApp", BackupAgent = typeof (MyBackupAgent))]
	public class MyApp : Application
	{
		public MyApp (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace UserApp.Nested
{
	[Register ("com/example/userapp/OuterClass")]
	public class OuterClass : Java.Lang.Object
	{
		// Nested class inheriting from Java peer — no [Register]
		public class InnerHelper : Java.Lang.Object
		{
		}

		// Deeply nested
		public class MiddleClass : Java.Lang.Object
		{
			public class DeepHelper : Java.Lang.Object
			{
			}
		}
	}
}

namespace UserApp.Models
{
	// These should all get CRC64-based JNI names
	public class UserModel : Java.Lang.Object
	{
	}

	public class DataManager : Java.Lang.Object
	{
	}
}

namespace UserApp
{
	[Register ("com/example/userapp/CustomView")]
	public class CustomView : Android.Views.View
	{
		protected CustomView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace UserApp.Listeners
{
	public class MyClickListener : Java.Lang.Object, Android.Views.View.IOnClickListener
	{
		public void OnClick (Android.Views.View? v)
		{
		}
	}
}

namespace UserApp
{
	public class ExportedMethodHolder : Java.Lang.Object
	{
		[Export ("doWork")]
		public void DoWork ()
		{
		}
	}

	// [Export] shapes that the legacy JCW emitter (CecilImporter.GetJniSignature)
	// cannot encode but that the trimmable scanner is expected to handle. These
	// types are excluded from legacy↔new comparison in ScannerComparisonTests
	// and validated by ScannerExportShapesTests via the new scanner only.
	public enum ExportSampleEnum { Zero, One, Two }
	public enum ExportSampleByteEnum : byte { Red, Green, Blue }
	public enum ExportSampleLongEnum : long { Zero = 0L, Big = long.MaxValue }

	public class ExportEnumShapes : Java.Lang.Object
	{
		[Export ("echoEnum")]
		public ExportSampleEnum EchoEnum (ExportSampleEnum value) => value;

		[Export ("echoByteEnum")]
		public ExportSampleByteEnum EchoByteEnum (ExportSampleByteEnum value) => value;

		[Export ("echoLongEnum")]
		public ExportSampleLongEnum EchoLongEnum (ExportSampleLongEnum value) => value;
	}

	public class ExportCharSequenceShapes : Java.Lang.Object
	{
		[Export ("echoCharSequence")]
		public Java.Lang.ICharSequence? EchoCharSequence (Java.Lang.ICharSequence? value) => value;
	}

	public class ExportCollectionShapes : Java.Lang.Object
	{
		[Export ("echoList")]
		public System.Collections.IList? EchoList (System.Collections.IList? value) => value;

		[Export ("echoMap")]
		public System.Collections.IDictionary? EchoMap (System.Collections.IDictionary? value) => value;

		[Export ("echoCollection")]
		public System.Collections.ICollection? EchoCollection (System.Collections.ICollection? value) => value;
	}

	// [ExportField] generates a Java field whose value is produced by a getter
	// method. The scanner must surface the method-level registration so the UCO
	// can dispatch to the getter.
	public class ExportFieldShapes : Java.Lang.Object
	{
		protected ExportFieldShapes (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[ExportField ("STATIC_INSTANCE")]
		public static ExportFieldShapes? GetInstance () => null;

		[ExportField ("VALUE")]
		public string GetValue () => "";

		[ExportField ("COUNT")]
		public int GetCount () => 0;
	}

	// [ExportParameter] overrides a Stream / XmlReader's Java type without
	// relying on auto-resolution. Each kind must map to its specific JNI
	// descriptor (java/io/InputStream, OutputStream, org/xmlpull/v1/XmlPullParser,
	// android/content/res/XmlResourceParser).
	public class ExportParameterShapes : Java.Lang.Object
	{
		[Export ("openStream")]
		public int OpenStream ([ExportParameter (ExportParameterKind.InputStream)] System.IO.Stream? stream)
			=> stream is null ? 0 : 1;

		[return: ExportParameter (ExportParameterKind.OutputStream)]
		[Export ("wrapStream")]
		public System.IO.Stream? WrapStream ([ExportParameter (ExportParameterKind.OutputStream)] System.IO.Stream? stream)
			=> stream;

		[return: ExportParameter (ExportParameterKind.XmlPullParser)]
		[Export ("readXml")]
		public System.Xml.XmlReader? ReadXml ([ExportParameter (ExportParameterKind.XmlPullParser)] System.Xml.XmlReader? reader)
			=> reader;

		[return: ExportParameter (ExportParameterKind.XmlResourceParser)]
		[Export ("readResourceXml")]
		public System.Xml.XmlReader? ReadResourceXml ([ExportParameter (ExportParameterKind.XmlResourceParser)] System.Xml.XmlReader? reader)
			=> reader;
	}

	// === Phase A: dispatch & declaration shapes ===

	// A.1: static [Export] method — different dispatch path (no `this`).
	public class StaticExportShapes : Java.Lang.Object
	{
		[Export ("compute")]
		public static int Compute (int x) => x;

		[Export ("hello")]
		public static string Hello () => "hi";
	}

	// A.2: [Export(Throws = ...)] — declared exceptions in JNI signature.
	public class ExportThrowsShapes : Java.Lang.Object
	{
		[Export ("ioCall", Throws = new [] { typeof (Java.IO.IOException) })]
		public void IoCall () { }

		[Export ("multiThrow", Throws = new [] { typeof (Java.IO.IOException), typeof (Java.Lang.IllegalStateException) })]
		public int MultiThrow () => 0;
	}

	// A.3: Mixed [Register] overrides + new [Export] methods on the same type.
	[Register ("my/app/MixedRegisterAndExport")]
	public class MixedRegisterAndExport : Activity
	{
		protected override void OnCreate (Android.OS.Bundle? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
		}

		[Export ("doWork")]
		public void DoWork () { }

		[Export ("compute")]
		public int Compute (int x) => x;
	}

	// A.4: [Export] on a virtual method, derived class re-declaring without [Export].
	public class VirtualExportBase : Java.Lang.Object
	{
		[Export ("ping")]
		public virtual int Ping () => 0;
	}

	public class VirtualExportDerived : VirtualExportBase
	{
		public override int Ping () => 1;
	}

	// A.5: [Export] with explicit JNI name differing from C# method name.
	public class ExportRenameShapes : Java.Lang.Object
	{
		[Export ("javaSideName")]
		public void CSharpSideName () { }
	}

	// === Phase B: edge marshalling ===

	// B.1: [Export] returning Java.Lang.Object explicitly (intentional unwrapped path).
	public class ExportObjectShapes : Java.Lang.Object
	{
		[Export ("any")]
		public Java.Lang.Object? Any (Java.Lang.Object? v) => v;
	}

	// B.2: array of user-peer type — exercise [] recursion through the user-peer
	// JNI resolver fix from a prior commit.
	public class UserPeerForArray : Java.Lang.Object
	{
		protected UserPeerForArray (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	public class ExportUserPeerArrayShapes : Java.Lang.Object
	{
		[Export ("echoArr")]
		public UserPeerForArray []? EchoArr (UserPeerForArray []? a) => a;
	}

	// B.3: protected/private [Export] methods — visibility shouldn't gate registration.
	public class ExportVisibilityShapes : Java.Lang.Object
	{
		[Export ("doProtected")]
		protected void DoProtected () { }

		[Export ("doPrivate")]
		void DoPrivate () { }
	}

	// B.4: [ExportField] returning a primitive — focused single-shape assertion.
	public class ExportFieldPrimitiveShapes : Java.Lang.Object
	{
		protected ExportFieldPrimitiveShapes (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[ExportField ("MAX_VALUE")]
		public static int GetMaxValue () => 42;
	}

	// B.5: [Export] overloads with same Java name, different signatures — no dedup.
	public class ExportOverloadShapes : Java.Lang.Object
	{
		[Export ("call")]
		public void Call (int x) { }

		[Export ("call")]
		public void Call (string s) { }
	}

	// === Phase C: robustness ===
	// C.1 (property) is gated by [AttributeUsage(Method|Constructor)] — skip.

	// C.2: generic method with [Export] — scanner shouldn't crash on T.
	public class ExportGenericShapes : Java.Lang.Object
	{
		[Export ("g")]
		public T Identity<T> (T x) => x;
	}

	// C.3: override of a [Register]'d base method also marked [Export].
	// Legacy: [Register]-driven dispatch wins (with connector); [Export] is a no-op.
	public class ExportOverridingRegisterShape : Activity
	{
		[Export ("onCreateExport")]
		protected override void OnCreate (Android.OS.Bundle? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
		}
	}
}
