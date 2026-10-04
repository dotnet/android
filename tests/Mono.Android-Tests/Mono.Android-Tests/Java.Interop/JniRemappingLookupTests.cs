using System;

using Android.Runtime;
using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("NativeAOTIgnore")]
	public class JniRemappingLookupTests
	{
		[Test]
		public void RuntimeFeatureIsEnabledForRemappingInputs ()
		{
			Assert.IsTrue (Microsoft.Android.Runtime.RuntimeFeature.JniRemapping);
		}

		[TestCase ("net/dot/android/remap/AsciiFirst", "net/dot/android/remap/TargetFirst")]
		[TestCase ("net/dot/android/remap/Middle", "net/dot/android/remap/TargetMiddle")]
		[TestCase ("net/dot/android/remap/Zebra", "net/dot/android/remap/TargetLast")]
		[TestCase ("net/dot/android/remap/Źródło", "net/dot/android/remap/UnicodeTarget")]
		public void ReplacementTypeLookupUsesGeneratedTable (string source, string target)
		{
			Assert.AreEqual (target, JniEnvironment.Runtime.TypeManager.GetReplacementType (source));
		}

		[TestCase ("0/net/dot/android/remap/Before")]
		[TestCase ("net/dot/android/remap/Between")]
		[TestCase ("\ue000/net/dot/android/remap/After")]
		public void ReplacementTypeLookupReturnsNullOutsideTable (string source)
		{
			Assert.IsNull (JniEnvironment.Runtime.TypeManager.GetReplacementType (source));
		}

		[Test]
		public void IncomingRenamedPeerUsesReverseTypeWithLlvmIrTypeMap ()
		{
			if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap)
				Assert.Ignore ("This test validates the nontrimmable LLVM-IR typemap path.");

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
				Assert.IsInstanceOf<IncomingDeclaredPeer> (renamedPeer);
				Assert.IsInstanceOf<IncomingWrongPeer> (overlappingPeer);
			} finally {
				JNIEnv.DeleteLocalRef (renamedHandle);
				JNIEnv.DeleteLocalRef (overlappingHandle);
			}
		}

		[Test]
		[NonParallelizable]
		public void ExplicitRuntimeRegistrationPrecedesReverseTypeWithLlvmIrTypeMap ()
		{
			if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap)
				Assert.Ignore ("This test validates the nontrimmable LLVM-IR typemap path.");

			global::Java.Interop.TypeManager.RegisterType (
				ExplicitRegisteredPeer.RuntimeJniName,
				typeof (ExplicitRegisteredPeer));

			Assert.AreEqual (
				typeof (ExplicitRegisteredPeer),
				JniEnvironment.Runtime.TypeManager.GetType (new JniTypeSignature (ExplicitRegisteredPeer.RuntimeJniName)));

			var handle = JNIEnv.CreateInstance (ExplicitRegisteredPeer.RuntimeJniName, "()V");
			try {
				using var peer = Java.Lang.Object.GetObject<Java.Lang.Object> (handle, JniHandleOwnership.DoNotTransfer);
				Assert.IsInstanceOf<ExplicitRegisteredPeer> (peer);
			} finally {
				JNIEnv.DeleteLocalRef (handle);
			}
		}

		[TestCase ("(I)I", "exact", "(I)I")]
		[TestCase ("(I)V", "parameters", "(I)V")]
		[TestCase ("(J)I", "wildcard", "(J)I")]
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

		[TestCase ("I", "exactValue", "J")]
		[TestCase ("J", "wildcardValue", "J")]
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
