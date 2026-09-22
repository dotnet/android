using System;
using System.Runtime.InteropServices;

using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("NativeAOTIgnore")]
	public class JniRemappingLookupTests
	{
		[TestCase ("net/dot/android/remap/AsciiFirst", "net/dot/android/remap/TargetFirst")]
		[TestCase ("net/dot/android/remap/Middle", "net/dot/android/remap/TargetMiddle")]
		[TestCase ("net/dot/android/remap/Zebra", "net/dot/android/remap/TargetLast")]
		[TestCase ("net/dot/android/remap/Źródło", "net/dot/android/remap/UnicodeTarget")]
		public void ReplacementTypeLookupUsesGeneratedTable (string source, string target)
		{
			Assert.AreEqual (target, JniEnvironment.Runtime.TypeManager.GetReplacementType (source));
		}

		[TestCase ("0/net/dot/android/remap/Before")]
		[TestCase ("\uffff/net/dot/android/remap/After")]
		public void ReplacementTypeLookupReturnsNullOutsideTable (string source)
		{
			Assert.IsNull (JniEnvironment.Runtime.TypeManager.GetReplacementType (source));
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
				GetString (replacement.TargetJniType, replacement.TargetJniTypeUtf8));
			Assert.AreEqual (
				targetName,
				GetString (replacement.TargetJniMethodName, replacement.TargetJniMethodNameUtf8));
			Assert.AreEqual (
				targetSignature,
				GetString (replacement.TargetJniMethodSignature, replacement.TargetJniMethodSignatureUtf8) ?? sourceSignature);
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

		static string GetString (string value, IntPtr utf8)
			=> utf8 == IntPtr.Zero ? value : Marshal.PtrToStringUTF8 (utf8);
	}
}
