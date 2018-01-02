using System;
using System.Collections.Generic;
using System.Reflection;

namespace Java.Interop
{
	public class JavaObjectArray<T> : JavaArray<T>
	{
		public JavaObjectArray (ref JniObjectReference handle, JniObjectReferenceOptions transfer)
			: base (ref handle, transfer)
		{
		}

		static JniObjectReference NewArray (int length)
		{
			var info = JniEnvironment.Runtime.TypeManager.GetTypeSignature (typeof (T));
			if (info.SimpleReference == null)
				info = new JniTypeSignature ("java/lang/Object", info.ArrayRank);
			if (info.IsKeyword && info.ArrayRank == 0) {
				info = info.GetPrimitivePeerTypeSignature ();
			}
			using (var t = new JniType (info.Name)) {
				return JniEnvironment.Arrays.NewObjectArray (length, t.PeerReference, new JniObjectReference ());
			}
		}

		public unsafe JavaObjectArray (int length)
			: this (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = NewArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaObjectArray (IList<T> value)
			: this (CheckLength (value))
		{
			for (int i = 0; i < value.Count; ++i)
				SetElementAt (i, value [i]);
		}

		public JavaObjectArray (IEnumerable<T> value)
			: this (ToList (value))
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
			var lref = JniEnvironment.Arrays.GetObjectArrayElement (PeerReference, index);
			return JniEnvironment.Runtime.ValueManager.GetValue<T> (ref lref, JniObjectReferenceOptions.CopyAndDispose);
		}

		void SetElementAt (int index, T value)
		{
			var vm  = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var s   = vm.CreateGenericObjectReferenceArgumentState (value);
			JniEnvironment.Arrays.SetObjectArrayElement (PeerReference, index, s.ReferenceValue);
			vm.DestroyGenericArgumentState (value, ref s, 0);
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
			var vm  = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var s   = vm.CreateArgumentState (default (T));
			for (int i = 0; i < len; i++) {
				JniEnvironment.Arrays.SetObjectArrayElement (PeerReference, i, s.ReferenceValue);
			}
			vm.DestroyGenericArgumentState (default (T), ref s, 0);
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
					var j = at as IJavaPeerable;
					if (j != null)
						j.DisposeUnlessReferenced ();
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
					var d = item as IJavaPeerable;
					if (d != null)
						d.DisposeUnlessReferenced ();
				}
			}
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				targetType == typeof (JavaObjectArray<T>);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<T>> {

			public override IList<T> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
			{
				return JavaArray<T>.CreateValue (ref reference, options, targetType, (ref JniObjectReference h, JniObjectReferenceOptions t) => new JavaObjectArray<T> (ref h, t) {
					forMarshalCollection    = true,
				});
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (IList<T> value, ParameterAttributes synchronize)
			{
				return JavaArray<T>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaObjectArray<T> (list)
						: new JavaObjectArray<T> (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState (IList<T> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<T>.DestroyArgumentState<JavaObjectArray<T>> (value, ref state, synchronize);
			}
		}
	}
}

