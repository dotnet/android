using System;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	// Device-level coverage for [Export] / [ExportField] marshalling.
	//
	// These tests drive the Java side of an [Export]-bearing peer via JNIEnv,
	// then assert what C# observed (and vice versa). They run under both the
	// legacy llvm-ir typemap (which is the contract) and the trimmable typemap
	// (which must match it). See export-comparison.md for the gap analysis.
	//
	// Naming: each test is named Export_<Group>_<Feature>_<Behaviour> so the
	// runner output is greppable.
	[TestFixture]
	public class ExportTests
	{
		// ---------------------------------------------------------------
		// Group A — parameter / return marshalling
		// ---------------------------------------------------------------

		[Test, Category ("Export")]
		public void Export_Method_Primitive_RoundTrip ()
		{
			using var e = new ExportPrimitives ();
			var m = JNIEnv.GetMethodID (e.Class.Handle, "EchoInt", "(I)I");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for EchoInt not found");
			int r = JNIEnv.CallIntMethod (e.Handle, m, new JValue (21));
			Assert.AreEqual (43, r, "EchoInt(21) should be 43 (= 21*2 + 1)");
		}

		[Test, Category ("Export")]
		public void Export_Method_Bool_RoundTrip ()
		{
			using var e = new ExportPrimitives ();
			var m = JNIEnv.GetMethodID (e.Class.Handle, "EchoBool", "(Z)Z");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for EchoBool not found");
			Assert.IsFalse (JNIEnv.CallBooleanMethod (e.Handle, m, new JValue (true)),  "EchoBool(true) should return false");
			Assert.IsTrue  (JNIEnv.CallBooleanMethod (e.Handle, m, new JValue (false)), "EchoBool(false) should return true");
		}

		[Test, Category ("Export")]
		public void Export_Method_String_RoundTrip ()
		{
			using var e = new ExportPrimitives ();
			var m = JNIEnv.GetMethodID (e.Class.Handle, "EchoString", "(Ljava/lang/String;)Ljava/lang/String;");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for EchoString not found");
			IntPtr argHandle = JNIEnv.NewString ("world");
			try {
				IntPtr resultHandle = JNIEnv.CallObjectMethod (e.Handle, m, new JValue (argHandle));
				try {
					string result = JNIEnv.GetString (resultHandle, JniHandleOwnership.DoNotTransfer);
					Assert.AreEqual ("<world>", result);
				} finally {
					JNIEnv.DeleteLocalRef (resultHandle);
				}
			} finally {
				JNIEnv.DeleteLocalRef (argHandle);
			}
		}

		[Test, Category ("Export")]
		public void Export_Method_PeerArg_RoundTrip ()
		{
			using var e = new ExportPrimitives ();
			using var arg = new Java.Lang.Integer (42);
			var m = JNIEnv.GetMethodID (e.Class.Handle, "GetClassName", "(Ljava/lang/Object;)Ljava/lang/String;");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for GetClassName not found");
			IntPtr resultHandle = JNIEnv.CallObjectMethod (e.Handle, m, new JValue (arg.Handle));
			try {
				string result = JNIEnv.GetString (resultHandle, JniHandleOwnership.DoNotTransfer);
				Assert.AreEqual ("java.lang.Integer", result);
			} finally {
				JNIEnv.DeleteLocalRef (resultHandle);
			}
		}

		[Test, Category ("Export")]
		public void Export_Method_PeerArg_NullArg_HandledGracefully ()
		{
			using var e = new ExportPrimitives ();
			var m = JNIEnv.GetMethodID (e.Class.Handle, "GetClassName", "(Ljava/lang/Object;)Ljava/lang/String;");
			IntPtr resultHandle = JNIEnv.CallObjectMethod (e.Handle, m, new JValue (IntPtr.Zero));
			try {
				string result = JNIEnv.GetString (resultHandle, JniHandleOwnership.DoNotTransfer);
				Assert.AreEqual ("<null>", result);
			} finally {
				JNIEnv.DeleteLocalRef (resultHandle);
			}
		}

		[Test, Category ("Export")]
		public void Export_Method_IntArray_RoundTrip_AndCopyBack ()
		{
			using var e = new ExportPrimitives ();
			var m = JNIEnv.GetMethodID (e.Class.Handle, "DoubleArray", "([I)[I");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for DoubleArray not found");

			var input = new int [] { 1, 2, 3 };
			IntPtr argHandle = JNIEnv.NewArray (input);
			try {
				IntPtr resultHandle = JNIEnv.CallObjectMethod (e.Handle, m, new JValue (argHandle));
				try {
					var output = (int []) JNIEnv.GetArray (resultHandle, JniHandleOwnership.DoNotTransfer, typeof (int));
					Assert.AreEqual (new [] { 2, 4, 6 }, output, "return array should have doubled values");

					// Copy-back: the input handle should also reflect the doubled values
					var roundTrippedInput = (int []) JNIEnv.GetArray (argHandle, JniHandleOwnership.DoNotTransfer, typeof (int));
					Assert.AreEqual (new [] { 2, 4, 6 }, roundTrippedInput, "input array mutations should propagate back to JNI handle");
				} finally {
					JNIEnv.DeleteLocalRef (resultHandle);
				}
			} finally {
				JNIEnv.DeleteLocalRef (argHandle);
			}
		}

		// NOTE: A5/A6/A7 (enum, ICharSequence return, IList return) are
		// deferred. The legacy Java callable wrapper emitter
		// (CecilImporter.GetJniSignature) returns null for managed enum,
		// non-bound IList, and certain ICharSequence shapes — the build
		// fails before the runtime path can be exercised. Those tests
		// belong with the codegen fix that teaches the JCW emitter to
		// widen these types (mirrors §2 / §7 of export-comparison.md).

		[Test, Category ("Export")]
		public void Export_Method_PeerArray_RoundTrip ()
		{
			using var e = new ExportPrimitives ();
			using var a = new Java.Lang.Integer (1);
			using var b = new Java.Lang.Integer (2);
			using var c = new Java.Lang.Integer (3);

			var m = JNIEnv.GetMethodID (e.Class.Handle, "Tail", "([Ljava/lang/Object;)[Ljava/lang/Object;");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for Tail not found");

			IntPtr argHandle = JNIEnv.NewObjectArray<Java.Lang.Object> (a, b, c);
			try {
				IntPtr resultHandle = JNIEnv.CallObjectMethod (e.Handle, m, new JValue (argHandle));
				try {
					var result = (Java.Lang.Object []) JNIEnv.GetArray (resultHandle, JniHandleOwnership.DoNotTransfer, typeof (Java.Lang.Object));
					Assert.AreEqual (2, result.Length);
					Assert.AreEqual ("2", result [0].ToString ());
					Assert.AreEqual ("3", result [1].ToString ());
				} finally {
					JNIEnv.DeleteLocalRef (resultHandle);
				}
			} finally {
				JNIEnv.DeleteLocalRef (argHandle);
			}
		}

		// ---------------------------------------------------------------
		// Group B — exception routing
		// ---------------------------------------------------------------
		// The trimmable [Export] UCO wraps the dispatch in BeginMarshalMethod /
		// OnUserUnhandledException / EndMarshalMethod so unhandled managed
		// exceptions are stored as a pending exception on the JniTransition
		// (matching the JavaInterop contract used by UCO ctors) instead of
		// aborting the process. When the JNI call returns to managed code on
		// the same thread, RaisePendingException re-raises the original
		// exception — which can be either the underlying managed exception
		// or a Java.Lang.Throwable depending on the runtime path. The
		// invariant we assert here is "process did not abort and an exception
		// surfaces with a recognizable message". See
		// ExportMethodDispatchEmitter.EmitWrappedExportMethodDispatch.

		[Test, Category ("Export")]
		public void Export_Method_Throws_PrimitiveReturn_SurfacesAsManagedException ()
		{
			using var e = new ExportThrowing ();
			var m = JNIEnv.GetMethodID (e.Class.Handle, "Throwing", "()I");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for Throwing not found");

			// The managed body throws InvalidOperationException("boom"). The wrapper
			// must catch it and route it through OnUserUnhandledException so the
			// process survives; the exception then re-surfaces on the calling
			// thread when the JNI call returns to managed code.
			var ex = Assert.Catch (() => JNIEnv.CallIntMethod (e.Handle, m));
			Assert.That (ex, Is.Not.Null, "expected an exception, got null");
			Assert.That (ex.Message, Contains.Substring ("boom"), "exception message should preserve 'boom'");
		}

		[Test, Category ("Export")]
		public void Export_Method_Throws_ObjectReturn_SurfacesAsManagedException ()
		{
			using var e = new ExportThrowing ();
			var m = JNIEnv.GetMethodID (e.Class.Handle, "ThrowingString", "()Ljava/lang/String;");
			Assert.AreNotEqual (IntPtr.Zero, m, "JNI method id for ThrowingString not found");
			var ex = Assert.Catch (() => JNIEnv.CallObjectMethod (e.Handle, m));
			Assert.That (ex, Is.Not.Null, "expected an exception, got null");
		}

		// ---------------------------------------------------------------
		// Group D — [ExportField] runtime visibility from Java
		// ---------------------------------------------------------------
		// NOTE: device-level [ExportField] tests are deferred. The JCW
		// generator (legacy and trimmable) currently emits a static field
		// initializer that calls the [ExportField] method as a non-static
		// member (`public static int FOO = InitialFoo();`), which fails
		// javac when the C# method is `static`, and is unreachable at
		// runtime when the C# method is an instance member because there
		// is no peer instance during class init. Add runtime [ExportField]
		// coverage once the JCW emitter handles both shapes correctly.
	}

	// ---------------------------------------------------------------
	// Test fixtures (peer types) used by the tests above.
	//
	// Each fixture is a small Java.Lang.Object subclass with [Export] members
	// designed to exercise one corner of the marshalling matrix.
	// ---------------------------------------------------------------

	class ExportPrimitives : Java.Lang.Object
	{
		[Export]
		public int EchoInt (int x) => x * 2 + 1;

		[Export]
		public bool EchoBool (bool x) => !x;

		[Export]
		public string EchoString (string x) => "<" + x + ">";

		[Export]
		public string GetClassName (Java.Lang.Object o) => o?.Class?.Name ?? "<null>";

		[Export]
		public int [] DoubleArray (int [] xs)
		{
			for (int i = 0; i < xs.Length; i++) {
				xs [i] *= 2;
			}
			return xs;
		}

		[Export]
		public Java.Lang.Object [] Tail (Java.Lang.Object [] xs)
		{
			if (xs.Length <= 1) {
				return Array.Empty<Java.Lang.Object> ();
			}
			var result = new Java.Lang.Object [xs.Length - 1];
			Array.Copy (xs, 1, result, 0, result.Length);
			return result;
		}
	}

	class ExportThrowing : Java.Lang.Object
	{
		[Export]
		public int Throwing () => throw new InvalidOperationException ("boom");

		[Export]
		public string ThrowingString () => throw new InvalidOperationException ("boom-string");
	}
}
