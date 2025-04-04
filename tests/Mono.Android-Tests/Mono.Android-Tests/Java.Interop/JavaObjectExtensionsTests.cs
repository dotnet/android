﻿using System;
using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content;
using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JavaObjectExtensionsTests {

		[Test]
		public void JavaCast_BaseToGenericWrapper ()
		{
			using (var list = new JavaList (new[]{ 1, 2, 3 }))
			using (var generic = JavaObjectExtensions.JavaCast<JavaList<int>> (list)) {
				// Yay, no exceptions!
				Assert.AreEqual (1, generic [0]);
			}
		}

		[Test]
		public void JavaCast_InterfaceCast ()
		{
			IntPtr g;
			using (var vp = global::Net.Dot.Android.Test.Example.ValueProvider) {
				g = JNIEnv.NewGlobalRef (vp.Handle);
			}
			// We want a Java.Lang.Object so that we create an IIValueProviderInvoker
			// instead of just getting back the original instance.
			using (var o = Java.Lang.Object.GetObject<Java.Lang.Object> (g, JniHandleOwnership.TransferGlobalRef)) {
				var c = JavaObjectExtensions.JavaCast<global::Net.Dot.Android.Test.IValueProvider> (o);
				Assert.AreEqual (42, c.Value);
				c.Dispose ();
			}
		}

		[Test]
		public void JavaCast_BadInterfaceCast ()
		{
			using var n = new Java.Lang.Integer (42);
			var e = Assert.Catch<Exception>(() => JavaObjectExtensions.JavaCast<Java.Lang.IAppendable> (n));
			if (e is System.Reflection.TargetInvocationException tie) {
				// .NET 8 behavior
				Assert.IsTrue (tie.InnerException is InvalidCastException);
			} else if (e is InvalidCastException ice) {
				// Yay
			} else {
				Assert.Fail ($"Unexpected exception type: {e.GetType ()}: {e.ToString ()}");
			}
		}

		[Test]
		public void JavaCast_ObtainOriginalInstance ()
		{
			using var list = new Java.Util.ArrayList ();
			using var copy = new Java.Lang.Object (list.Handle, JniHandleOwnership.DoNotTransfer);
			using var al   = JavaObjectExtensions.JavaCast<Java.Util.AbstractList> (copy);

			// Semantic change: in .NET 8, `al` is a new `AbstractListInvoker` instance, not `list`
			Assert.AreSame (list, al);
		}

		[Test]
		public void JavaCast_InvalidTypeCastThrows ()
		{
			using (var s = new Java.Lang.String ("value")) {
				Assert.Throws<InvalidCastException> (() => JavaObjectExtensions.JavaCast<Java.Lang.Integer> (s));
			}
		}

		[Test]
		public void JavaCast_CheckForManagedSubclasses ()
		{
			using (var o = CreateObject ()) {
				Assert.Throws<InvalidCastException> (() => JavaObjectExtensions.JavaCast<MyObject> (o));
			}
		}

		[Test]
		public void JavaAs ()
		{
			using var v     = new Java.InteropTests.MyJavaInterfaceImpl ();
			using var c     = v.JavaAs<Java.Lang.ICloneable>();
			Assert.IsNotNull (c);
		}

		static Java.Lang.Object CreateObject ()
		{
			var ctor    = JNIEnv.GetMethodID (Java.Lang.Class.Object, "<init>", "()V");
			var value   = JNIEnv.NewObject (Java.Lang.Class.Object, ctor);
			return new Java.Lang.Object (value, JniHandleOwnership.TransferLocalRef);
		}
	}

	class MyObject : Java.Lang.Object, Java.Lang.ICloneable {

		public MyObject ()
		{
		}

		public MyObject (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace Net.Dot.Android.Test {
	partial class IValueProviderInvoker {
	}
}
