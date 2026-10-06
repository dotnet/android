#if !__ANDROID__
using System;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[NonParallelizable]
	public class ThrowableInputCleanupTests : JavaVMFixture
	{
		[TestCase (1, JniObjectReferenceType.Global, true)]
		[TestCase (2, JniObjectReferenceType.Global, true)]
		[TestCase (3, JniObjectReferenceType.Global, true)]
		[TestCase (1, JniObjectReferenceType.Local, true)]
		[TestCase (2, JniObjectReferenceType.Local, true)]
		[TestCase (3, JniObjectReferenceType.Local, true)]
		[TestCase (1, JniObjectReferenceType.Global, false)]
		[TestCase (2, JniObjectReferenceType.Global, false)]
		[TestCase (3, JniObjectReferenceType.Global, false)]
		[TestCase (1, JniObjectReferenceType.Local, false)]
		[TestCase (2, JniObjectReferenceType.Local, false)]
		[TestCase (3, JniObjectReferenceType.Local, false)]
		[TestCase (1, JniObjectReferenceType.Invalid, true)]
		[TestCase (2, JniObjectReferenceType.Invalid, true)]
		[TestCase (3, JniObjectReferenceType.Invalid, true)]
		[TestCase (0, JniObjectReferenceType.Global, true)]
		[TestCase (0, JniObjectReferenceType.Local, true)]
		[TestCase (0, JniObjectReferenceType.Global, false)]
		[TestCase (0, JniObjectReferenceType.Local, false)]
		public unsafe void ExtractionDeletesOnlyTransferredInputOnFailure (int stage, JniObjectReferenceType type, bool transferred)
		{
			using var javaType = new JniType ("net/dot/jni/test/TransferFailureThrowable");
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (stage);
			var source = javaType.NewObject (javaType.GetConstructor ("(I)V"), args);
			var input = type == JniObjectReferenceType.Global ? source.NewGlobalRef () : source.NewLocalRef ();
			var handle = input.Handle;
			var throwable = new JniObjectReference (handle, type);
			var ownerThread = Environment.CurrentManagedThreadId;
			int deletions = 0;
			void ObserveDeletion (JniObjectReference reference)
			{
				if (reference.Handle == handle && reference.Type == input.Type &&
						(input.Type != JniObjectReferenceType.Local || Environment.CurrentManagedThreadId == ownerThread))
					Interlocked.Increment (ref deletions);
			}
			TestJVM.ReferenceDeleted += ObserveDeletion;
			try {
				if (stage == 0) {
					using var peer = new BorrowedThrowable (throwable, transferred);
					Assert.AreEqual ("transfer test", peer.Message);
					Assert.AreEqual ("transfer test stack", peer.JavaStackTrace);
				} else {
					var error = Assert.Throws<JavaException> (() => new BorrowedThrowable (throwable, transferred));
					try {
						StringAssert.Contains ("transfer-extraction-" + stage, error.Message);
					} finally {
						error?.Dispose ();
					}
				}
				Assert.AreEqual (stage != 0 && transferred && type != JniObjectReferenceType.Invalid ? 1 : 0, deletions,
					"Must delete precisely the original input, and only on failed transferred initialization.");
				if (deletions == 0) {
					Assert.IsTrue (JniEnvironment.Types.IsSameObject (source, input), "Input must remain live on success or when borrowed.");
				}
			} finally {
				TestJVM.ReferenceDeleted -= ObserveDeletion;
				if (deletions == 0) {
					JniObjectReference.Dispose (ref input);
				}
				JniObjectReference.Dispose (ref source);
			}
		}

		sealed class BorrowedThrowable : JavaException
		{
			public unsafe BorrowedThrowable (JniObjectReference throwable, bool transferred)
				: base (throwable,
					transferred ? JniObjectReferenceOptions.CopyAndDispose : JniObjectReferenceOptions.None)
			{
			}
		}
	}
}
#endif
