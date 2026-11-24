using System;

using Android.Runtime;
using Java.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	[TestCategory ("NativeAOTIgnore")]
	[TestCategory ("JniRemapping")]
	public class JniRemappingLookupTests
	{
		[TestMethod]
		public void RuntimeFeatureIsEnabledForRemappingInputs ()
		{
			Assert.IsTrue (Microsoft.Android.Runtime.RuntimeFeature.JniRemapping);
		}

		[TestMethod]
		[DataRow ("net/dot/android/remap/AsciiFirst", "net/dot/android/remap/TargetFirst")]
		[DataRow ("net/dot/android/remap/Middle", "net/dot/android/remap/TargetMiddle")]
		[DataRow ("net/dot/android/remap/Zebra", "net/dot/android/remap/TargetLast")]
		[DataRow ("net/dot/android/remap/Źródło", "net/dot/android/remap/UnicodeTarget")]
		public void ReplacementTypeLookupUsesGeneratedTable (string source, string target)
		{
			Assert.AreEqual (target, JniEnvironment.Runtime.TypeManager.GetReplacementType (source));
		}

		[TestMethod]
		[DataRow ("0/net/dot/android/remap/Before")]
		[DataRow ("net/dot/android/remap/Between")]
		[DataRow ("\ue000/net/dot/android/remap/After")]
		public void ReplacementTypeLookupReturnsNullOutsideTable (string source)
		{
			Assert.IsNull (JniEnvironment.Runtime.TypeManager.GetReplacementType (source));
		}

		[TestMethod]
		public void IncomingRenamedPeerUsesReverseTypeWithTrimmableTypeMap ()
		{
			Assert.AreEqual (
				typeof (IncomingDeclaredPeer),
				JniEnvironment.Runtime.TypeManager.GetType (new JniTypeSignature (IncomingDeclaredPeer.RuntimeJniName)));
			Assert.AreEqual (
				typeof (IncomingWrongPeer),
				JniEnvironment.Runtime.TypeManager.GetType (new JniTypeSignature (IncomingDeclaredPeer.DeclaredJniName)));

			var renamedHandle = JNIEnv.CreateInstance (IncomingDeclaredPeer.RuntimeJniName, "()V");
			var overlappingHandle = JNIEnv.CreateInstance (IncomingDeclaredPeer.DeclaredJniName, "()V");
			try {
				using var renamedPeer = Java.Lang.Object.GetObject<Java.Lang.Object> (renamedHandle, JniHandleOwnership.DoNotTransfer);
				using var overlappingPeer = Java.Lang.Object.GetObject<Java.Lang.Object> (overlappingHandle, JniHandleOwnership.DoNotTransfer);
				Assert.IsInstanceOfType<IncomingDeclaredPeer> (renamedPeer);
				Assert.IsInstanceOfType<IncomingWrongPeer> (overlappingPeer);
			} finally {
				JNIEnv.DeleteLocalRef (renamedHandle);
				JNIEnv.DeleteLocalRef (overlappingHandle);
			}
		}

		[TestMethod]
		public void ExplicitRuntimeRegistrationIsNotSupported ()
		{
#pragma warning disable CS0618 // Verify the retained obsolete API still rejects explicit registration.
			Assert.ThrowsExactly<NotSupportedException> (() => global::Java.Interop.TypeManager.RegisterType (
				ExplicitRegisteredPeer.RuntimeJniName,
				typeof (ExplicitRegisteredPeer)));
#pragma warning restore CS0618
		}

		[TestMethod]
		[DataRow ("(I)I", "exact", "(I)I")]
		[DataRow ("(I)V", "parameters", "(I)V")]
		[DataRow ("(J)I", "wildcard", "(J)I")]
		public void ReplacementMethodLookupPrefersSpecificSignature (string sourceSignature, string targetName, string targetSignature)
		{
			var info = JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (
				"net/dot/android/remap/ManagedLookup",
				"overload",
				sourceSignature);

			Assert.IsTrue (info.HasValue);
			var replacement = info.GetValueOrDefault ();
			Assert.AreEqual (
				"net/dot/android/remap/ManagedTarget",
				replacement.TargetJniType);
			Assert.AreEqual (
				targetName,
				replacement.TargetJniMethodName);
			Assert.AreEqual (
				targetSignature,
				replacement.TargetJniMethodSignature);
			Assert.AreEqual ("net/dot/android/remap/ManagedLookup", replacement.SourceJniType);
			Assert.AreEqual ("overload", replacement.SourceJniMethodName);
			Assert.AreEqual (sourceSignature, replacement.SourceJniMethodSignature);
		}

		[TestMethod]
		[DataRow ("I", "exactValue", "J")]
		[DataRow ("J", "wildcardValue", "J")]
		public void ReplacementFieldLookupPrefersSpecificSignature (string sourceSignature, string targetName, string targetSignature)
		{
			var info = JniEnvironment.Runtime.TypeManager.GetReplacementFieldInfo (
				"net/dot/android/remap/ManagedLookup",
				"value",
				sourceSignature);

			Assert.IsTrue (info.HasValue);
			var replacement = info.GetValueOrDefault ();
			Assert.AreEqual ("net/dot/android/remap/ManagedTarget", replacement.TargetJniType);
			Assert.AreEqual (targetName, replacement.TargetJniFieldName);
			Assert.AreEqual (targetSignature, replacement.TargetJniFieldSignature);
		}

	}

	[Register (DeclaredJniName, DoNotGenerateAcw = true)]
	sealed class IncomingDeclaredPeer : Java.Lang.Object
	{
		public const string DeclaredJniName = "net/dot/android/remap/IncomingDeclaredPeer";
		public const string RuntimeJniName = "net/dot/android/remap/IncomingRenamedPeer";

		public IncomingDeclaredPeer (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	[Register ("net/dot/android/remap/IncomingWrongPeer", DoNotGenerateAcw = true)]
	sealed class IncomingWrongPeer : Java.Lang.Object
	{
		public IncomingWrongPeer (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	[Register ("net/dot/android/remap/ExplicitRegisteredPeer", DoNotGenerateAcw = true)]
	sealed class ExplicitRegisteredPeer : Java.Lang.Object
	{
		public const string RuntimeJniName = "net/dot/android/remap/ExplicitRuntimePeer";

		public ExplicitRegisteredPeer (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}
