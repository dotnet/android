using System;

using Android.Runtime;

using Java.Interop;

using Mono.Android_Test.Library;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("InvokerActivation")]
	public class InvokerActivationTests
	{
		[Test]
		public void XamarinAndroidInterfaceInvoker_ActivatesOnceAndPreservesIdentity ()
		{
			XamarinListInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var first = Java.Lang.Object.GetObject<IXamarinList> (handle, JniHandleOwnership.DoNotTransfer);
				var second = Java.Lang.Object.GetObject<IXamarinList> (handle, JniHandleOwnership.DoNotTransfer);

				Assert.IsNotNull (first);
				Assert.AreSame (first, second);
				Assert.AreEqual (typeof (XamarinListInvoker), first.GetType ());
				Assert.AreEqual (1, XamarinListInvoker.ConstructorInvocations);
				first.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		[Test]
		public void JavaInteropInterfaceInvoker_ActivatesOnceAndPreservesIdentity ()
		{
			JavaInteropCollectionInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var first = Java.Lang.Object.GetObject<IJavaInteropCollection> (handle, JniHandleOwnership.DoNotTransfer);
				var second = Java.Lang.Object.GetObject<IJavaInteropCollection> (handle, JniHandleOwnership.DoNotTransfer);

				Assert.IsNotNull (first);
				Assert.AreSame (first, second);
				Assert.AreEqual (typeof (JavaInteropCollectionInvoker), first.GetType ());
				Assert.AreEqual (1, JavaInteropCollectionInvoker.ConstructorInvocations);
				first.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		[Test]
		public void InheritedJavaInteropInterface_UsesExplicitInvoker ()
		{
			InheritedJavaInteropListInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var peer = Java.Lang.Object.GetObject<IInheritedJavaInteropList> (handle, JniHandleOwnership.DoNotTransfer);

				Assert.IsNotNull (peer);
				Assert.IsInstanceOf<IJavaInteropCollection> (peer);
				Assert.AreEqual (typeof (InheritedJavaInteropListInvoker), peer.GetType ());
				Assert.AreEqual (1, InheritedJavaInteropListInvoker.ConstructorInvocations);
				peer.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		[Test]
		public void AbstractJavaInteropType_UsesExplicitInvoker ()
		{
			JavaInteropAbstractListInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var peer = Java.Lang.Object.GetObject<JavaInteropAbstractList> (handle, JniHandleOwnership.DoNotTransfer);

				Assert.IsNotNull (peer);
				Assert.AreEqual (typeof (JavaInteropAbstractListInvoker), peer.GetType ());
				Assert.AreEqual (1, JavaInteropAbstractListInvoker.ConstructorInvocations);
				peer.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		[Test]
		public void InvokerInAnotherAssembly_ActivatesAndPreservesIdentity ()
		{
			ExternalRandomAccessInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var first = Java.Lang.Object.GetObject<IExternalRandomAccess> (handle, JniHandleOwnership.DoNotTransfer);
				var second = Java.Lang.Object.GetObject<IExternalRandomAccess> (handle, JniHandleOwnership.DoNotTransfer);

				Assert.IsNotNull (first);
				Assert.AreSame (first, second);
				Assert.AreEqual (typeof (ExternalRandomAccessInvoker), first.GetType ());
				Assert.AreEqual (1, ExternalRandomAccessInvoker.ConstructorInvocations);
				first.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		static IntPtr CreateArrayListHandle ()
		{
			using var list = new Java.Util.ArrayList ();
			return JNIEnv.NewGlobalRef (list.Handle);
		}
	}

	[Register ("java/util/List", "", "Java.InteropTests.XamarinListInvoker")]
	interface IXamarinList : IJavaPeerable, IDisposable
	{
	}

	[Register ("java/util/List", DoNotGenerateAcw = true)]
	sealed class XamarinListInvoker : Java.Lang.Object, IXamarinList
	{
		public static int ConstructorInvocations;

		public XamarinListInvoker (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
			ConstructorInvocations++;
		}
	}

	[JniTypeSignature ("java/util/Collection", GenerateJavaPeer = false, InvokerType = typeof (JavaInteropCollectionInvoker))]
	interface IJavaInteropCollection : IJavaPeerable
	{
	}

	[JniTypeSignature ("java/util/Collection", GenerateJavaPeer = false)]
	sealed class JavaInteropCollectionInvoker : JavaObject, IJavaInteropCollection
	{
		static readonly JniPeerMembers members = new JniPeerMembers ("java/util/Collection", typeof (JavaInteropCollectionInvoker));

		public static int ConstructorInvocations;

		public override JniPeerMembers JniPeerMembers => members;

		public JavaInteropCollectionInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
			ConstructorInvocations++;
		}
	}

	[JniTypeSignature ("java/util/List", GenerateJavaPeer = false, InvokerType = typeof (InheritedJavaInteropListInvoker))]
	interface IInheritedJavaInteropList : IJavaInteropCollection
	{
	}

	[JniTypeSignature ("java/util/List", GenerateJavaPeer = false)]
	sealed class InheritedJavaInteropListInvoker : JavaObject, IInheritedJavaInteropList
	{
		static readonly JniPeerMembers members = new JniPeerMembers ("java/util/List", typeof (InheritedJavaInteropListInvoker));

		public static int ConstructorInvocations;

		public override JniPeerMembers JniPeerMembers => members;

		public InheritedJavaInteropListInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
			ConstructorInvocations++;
		}
	}

	[JniTypeSignature ("java/util/AbstractList", GenerateJavaPeer = false, InvokerType = typeof (JavaInteropAbstractListInvoker))]
	abstract class JavaInteropAbstractList : JavaObject
	{
		static readonly JniPeerMembers members = new JniPeerMembers ("java/util/AbstractList", typeof (JavaInteropAbstractList));

		public override JniPeerMembers JniPeerMembers => members;

		protected JavaInteropAbstractList (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
		}
	}

	[JniTypeSignature ("java/util/AbstractList", GenerateJavaPeer = false)]
	sealed class JavaInteropAbstractListInvoker : JavaInteropAbstractList
	{
		public static int ConstructorInvocations;

		public JavaInteropAbstractListInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
			ConstructorInvocations++;
		}
	}

	[Register ("java/util/RandomAccess", DoNotGenerateAcw = true)]
	sealed class ExternalRandomAccessInvoker : Java.Lang.Object, IExternalRandomAccess
	{
		public static int ConstructorInvocations;

		public ExternalRandomAccessInvoker (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
			ConstructorInvocations++;
		}
	}
}
