using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaVMTest
	{
		[Test]
		public void CreateJavaVM ()
		{
			Assert.AreSame (JVM.Current, JavaVM.Current);
			Assert.IsNotNull (JVM.Current.SafeHandle);
			Assert.IsNotNull (JniEnvironment.Current);
		}

		[Test]
		public void JDK_OnlySupportsOneVM ()
		{
			#pragma warning disable 0219
			var first = JVM.Current;
			#pragma warning restore 0219
			try {
				var second = new JreVMBuilder ().CreateJreVM ();
				// If we reach here, we're in a JVM that supports > 1 VM
				second.Dispose ();
				Assert.Ignore ();
			} catch (NotSupportedException) {
			} catch (Exception e){
				Assert.Fail ("Expected NotSupportedException; got: {0}", e);
			}
		}

		[Test, ExpectedException (typeof (ArgumentNullException))]
		public void CreateJavaVMWithNullBuilder ()
		{
			new JavaVMWithNullBuilder ();
		}

		class JavaVMWithNullBuilder : JavaVM {
			public JavaVMWithNullBuilder ()
				: base ((JavaVMOptions) null)
			{
			}
		}

		[Test]
		public void GetRegisteredJavaVM_ExistingInstance ()
		{
			Assert.AreEqual (JavaVM.Current, JavaVM.GetRegisteredJavaVM (JavaVM.Current.SafeHandle));
		}

		[Test]
		public void GetObject_ReturnsNullWithNullHandle ()
		{
			var o = JVM.Current.GetObject (IntPtr.Zero);
			Assert.IsNull (o);
		}

		[Test]
		public void GetObject_ReturnsRegisteredInstance ()
		{
			JniLocalReference lref;
			using (var o = new JavaObject ()) {
				lref = o.SafeHandle.NewLocalRef ();
				Assert.AreSame (o, JVM.Current.GetObject (lref.DangerousGetHandle ()));
			}
			// At this point, the Java-side object is kept alive by `lref`,
			// but the wrapper instance has been disposed, and thus should
			// be unregistered, and thus unfindable.
			Assert.IsNull (JVM.Current.GetObject (lref, JniHandleOwnership.Transfer));
			Assert.IsTrue (lref.IsInvalid);
		}

		[Test]
		public void GetObject_ReturnsNullWithInvalidSafeHandle ()
		{
			var invalid = JniReferenceSafeHandle.Null;
			Assert.IsNull (JVM.Current.GetObject (invalid, JniHandleOwnership.Transfer));
		}
	}
}

