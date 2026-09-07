using System;
using System.Diagnostics.CodeAnalysis;

using Android.Runtime;

using Java.Interop;

using Microsoft.Android.Runtime;

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
			IXamarinListInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var first = Java.Lang.Object.GetObject<IXamarinList> (handle, JniHandleOwnership.DoNotTransfer);
				var second = Java.Lang.Object.GetObject<IXamarinList> (handle, JniHandleOwnership.DoNotTransfer);

				Assert.IsNotNull (first);
				Assert.AreSame (first, second);
				Assert.AreEqual (typeof (IXamarinListInvoker), first.GetType ());
				Assert.AreEqual (1, IXamarinListInvoker.ConstructorInvocations);
				first.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		[Test]
		public void JavaInteropInterfaceInvoker_ActivatesOnceAndPreservesIdentity ()
		{
			AssumeTrimmableTypeMapEnabled ();
			JavaInteropCollectionInvoker.ConstructorInvocations = 0;

			var handle = CreateHashSetHandle ();
			try {
				var first = GetValue<IJavaInteropCollection> (handle);
				var second = GetValue<IJavaInteropCollection> (handle);

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
		public void CompatibleInterfaceThenConcrete_PreservesIdentity ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var handle = CreateArrayListHandle ();
			try {
				var asInterface = GetValue<Java.Util.IList> (handle);
				var asConcrete = GetValue<Java.Util.ArrayList> (handle);

				Assert.IsNotNull (asInterface);
				Assert.AreSame (asInterface, asConcrete);
				Assert.AreEqual (typeof (Java.Util.ArrayList), asInterface.GetType ());
				asInterface.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		[Test]
		public void InheritedJavaInteropInterface_UsesExplicitInvoker ()
		{
			AssumeTrimmableTypeMapEnabled ();
			InheritedJavaInteropListInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var peer = GetValue<IInheritedJavaInteropList> (handle);

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
			AssumeTrimmableTypeMapEnabled ();
			JavaInteropAbstractListInvoker.ConstructorInvocations = 0;

			var handle = CreateArrayListHandle ();
			try {
				var peer = GetValue<JavaInteropAbstractList> (handle);

				Assert.IsNotNull (peer);
				Assert.AreEqual (typeof (JavaInteropAbstractListInvoker), peer.GetType ());
				Assert.AreEqual (1, JavaInteropAbstractListInvoker.ConstructorInvocations);
				peer.Dispose ();
			} finally {
				JNIEnv.DeleteGlobalRef (handle);
			}
		}

		static void AssumeTrimmableTypeMapEnabled ()
		{
			if (!RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}
		}

		static IntPtr CreateArrayListHandle ()
		{
			using var list = new Java.Util.ArrayList ();
			return JNIEnv.NewGlobalRef (list.Handle);
		}

		static IntPtr CreateHashSetHandle ()
		{
			using var set = new Java.Util.HashSet ();
			return JNIEnv.NewGlobalRef (set.Handle);
		}

		static T GetValue<[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T> (IntPtr handle)
		{
			var reference = new JniObjectReference (handle, JniObjectReferenceType.Global);
			return JniEnvironment.Runtime.ValueManager.GetValue<T> (ref reference, JniObjectReferenceOptions.Copy);
		}
	}

	[Register ("java/util/List", "", "Java.InteropTests.IXamarinListInvoker", DoNotGenerateAcw = true)]
	interface IXamarinList : IJavaObject, IJavaPeerable, IDisposable
	{
	}

	[Register ("java/util/List", DoNotGenerateAcw = true)]
	sealed class IXamarinListInvoker : Java.Lang.Object, IXamarinList
	{
		public static int ConstructorInvocations;

		public IXamarinListInvoker (IntPtr handle, JniHandleOwnership transfer)
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
	sealed class JavaInteropCollectionInvoker : global::Java.Interop.JavaObject, IJavaInteropCollection
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
	sealed class InheritedJavaInteropListInvoker : global::Java.Interop.JavaObject, IInheritedJavaInteropList
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
	abstract class JavaInteropAbstractList : global::Java.Interop.JavaObject
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

}
