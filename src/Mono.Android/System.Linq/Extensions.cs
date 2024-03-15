using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

using Java.Interop;

namespace System.Linq {

	public static class Extensions {

		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		static IntPtr id_next;

		static Extensions ()
		{
			IntPtr grefIterable = JNIEnv.FindClass ("java/util/Iterator");
			id_next = JNIEnv.GetMethodID (grefIterable, "next", "()Ljava/lang/Object;");
			JNIEnv.DeleteGlobalRef (grefIterable);
		}

		public static IEnumerable ToEnumerable (this Java.Lang.IIterable source)
		{
			if (source == null)
				throw new ArgumentNullException ("source");
			using (var iterator = source.Iterator ())
				while (iterator.HasNext) {
					yield return JavaConvert.FromJniHandle (
							JNIEnv.CallObjectMethod (iterator.Handle, id_next),
							JniHandleOwnership.TransferLocalRef);
				}
		}

		internal static IEnumerator ToEnumerator_Dispose (this Java.Util.IIterator source)
		{
			using (source)
				while (source.HasNext) {
					yield return JavaConvert.FromJniHandle (
							JNIEnv.CallObjectMethod (source.Handle, id_next),
							JniHandleOwnership.TransferLocalRef);
				}
		}

		public static IEnumerable<T?> ToEnumerable<
				[DynamicallyAccessedMembers (Constructors)] 
				T
		> (this Java.Lang.IIterable source)
		{
			if (source == null)
				throw new ArgumentNullException ("source");
			using (var iterator = source.Iterator ())
				while (iterator.HasNext) {
					yield return JavaConvert.FromJniHandle<T>(
							JNIEnv.CallObjectMethod (iterator.Handle, id_next),
							JniHandleOwnership.TransferLocalRef)!;
				}
		}

		internal static IEnumerator<T> ToEnumerator_Dispose<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (this Java.Util.IIterator source)
		{
			using (source)
				while (source.HasNext) {
					yield return JavaConvert.FromJniHandle<T>(
							JNIEnv.CallObjectMethod (source.Handle, id_next),
							JniHandleOwnership.TransferLocalRef)!;
				}
		}
	}
}

