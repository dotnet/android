using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public class JavaObjectArray<T> : JavaArray<T>
	{
		public JavaObjectArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static JniLocalReference _NewArray (int length)
		{
			var info = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (typeof (T));
			if (info.JniTypeName == null)
				info.JniTypeName = "java/lang/Object";
			if (info.TypeIsKeyword && info.ArrayRank == 0) {
				if (info.JniTypeName == "I")
					info.JniTypeName = JniInteger.JniTypeName;
			}
			using (var t = new JniType (info.ToString ())) {
				return JniEnvironment.Arrays.NewObjectArray (length, t.SafeHandle, JniReferenceSafeHandle.Null);
			}
		}

		public JavaObjectArray (int length)
			: this (_NewArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaObjectArray (IList<T> value)
			: this (CheckLength (value))
		{
			for (int i = 0; i < value.Count; ++i)
				SetElementAt (i, value [i]);
		}

		public JavaObjectArray (IEnumerable<T> value)
			: this (_ToList (value))
		{
		}

		public override T this [int index] {
			get {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				return GetElementAt (index);
			}
			set {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				SetElementAt (index, value);
			}
		}

		T GetElementAt (int index)
		{
			var lref = JniEnvironment.Arrays.GetObjectArrayElement (SafeHandle, index);
			return JniMarshal.GetValue<T> (lref, JniHandleOwnership.Transfer);
		}

		void SetElementAt (int index, T value)
		{
			using (var h = JniMarshal.CreateLocalRef (value))
				JniEnvironment.Arrays.SetObjectArrayElement (SafeHandle, index, h);
		}

		public override IEnumerator<T> GetEnumerator ()
		{
			int len = Length;
			for (int i = 0; i < len; ++i) {
				yield return GetElementAt (i);
			}
		}

		public override void Clear ()
		{
			int len = Length;
			using (var v = JniMarshal.CreateLocalRef (default (T))) {
				for (int i = 0; i < len; i++) {
					JniEnvironment.Arrays.SetObjectArrayElement (SafeHandle, i, v);
				}
			}
		}

		public override int IndexOf (T item)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var at = GetElementAt (i);
				try {
					if (EqualityComparer<T>.Default.Equals (item, at) || JniMarshal.RecursiveEquals (item, at))
						return i;
				} finally {
					var j = at as IJavaObject;
					if (j != null)
						j.DisposeUnlessRegistered ();
				}
			}
			return -1;
		}

		public override void CopyTo (T[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			CheckArrayCopy (0, Length, arrayIndex, array.Length, Length);
			CopyToList (array, arrayIndex);
		}

		internal override void CopyToList (IList<T> list, int index)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var item         = GetElementAt (i);
				list [index + i] = item;
				if (forMarshalCollection) {
					var d = item as IJavaObject;
					if (d != null)
						d.DisposeUnlessRegistered ();
				}
			}
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				targetType == typeof (JavaObjectArray<T>);
		}

		internal static object GetValue (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
			return JavaArray<T>.GetValueFromJni (handle, transfer, targetType, (h, T) => new JavaObjectArray<T> (h, T) {
				forMarshalCollection    = true,
			});
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
			return JavaArray<T>.CreateLocalRef (value, list => new JavaObjectArray<T>(list));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
			return JavaArray<T>.CreateMarshalCollection (value, list => new JavaObjectArray<T> (list) {
				forMarshalCollection    = true,
			});
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
			JavaArray<T>.CleanupMarshalCollection<JavaObjectArray<T>> (marshalObject, value);
		}
	}
}

