#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Java.Interop
{
	public class JavaObjectArray<T> : JavaArray<T>
	{
		internal    static  readonly    ValueMarshaler   Instance           = new ValueMarshaler ();

		public JavaObjectArray (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
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

		public override void DisposeUnlessReferenced ()
		{
			if (forMarshalCollection) {
				Dispose ();
				return;
			}
			base.DisposeUnlessReferenced ();
		}

		[MaybeNull]
		public override T this [int index] {
			get {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException (nameof (index), "index < 0 || index >= Length");
				return GetElementAt (index);
			}
			set {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException (nameof (index), "index < 0 || index >= Length");
				SetElementAt (index, value);
			}
		}

		[return: MaybeNull]
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
#pragma warning disable CS8603 // Possible null reference return.
				yield return GetElementAt (i);
#pragma warning restore CS8603 // Possible null reference return.
			}
		}

		public override void Clear ()
		{
			int len = Length;
			var vm  = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
#pragma warning disable 8653
			var s   = vm.CreateArgumentState (default (T));
			for (int i = 0; i < len; i++) {
				JniEnvironment.Arrays.SetObjectArrayElement (PeerReference, i, s.ReferenceValue);
			}
			vm.DestroyGenericArgumentState (default (T), ref s, 0);
#pragma warning restore 8653
		}

		public override int IndexOf (T item)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var at = GetElementAt (i);
				try {
#pragma warning disable CS8604 // Possible null reference argument.
					if (EqualityComparer<T>.Default.Equals (item, at) || JniMarshal.RecursiveEquals (item, at))
#pragma warning restore CS8604 // Possible null reference argument.
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
				throw new ArgumentNullException (nameof (array));
			CheckArrayCopy (0, Length, arrayIndex, array.Length, Length);
			CopyToList (array, arrayIndex);
		}

		internal override void CopyToList (IList<T> list, int index)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var item         = GetElementAt (i);
#pragma warning disable CS8601 // Possible null reference assignment.
				list [index + i] = item;
#pragma warning restore CS8601 // Possible null reference assignment.
				if (forMarshalCollection) {
					var d = item as IJavaPeerable;
					if (d != null)
						d.DisposeUnlessReferenced ();
				}
			}
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				targetType == typeof (JavaObjectArray<T>);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<T>> {

			public override IList<T> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<T>.CreateValue (ref reference, options, targetType, (ref JniObjectReference h, JniObjectReferenceOptions t) => new JavaObjectArray<T> (ref h, t) {
					forMarshalCollection    = true,
				});
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull]IList<T> value, ParameterAttributes synchronize)
			{
				return JavaArray<T>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaObjectArray<T> (list)
						: new JavaObjectArray<T> (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull]IList<T> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<T>.DestroyArgumentState<JavaObjectArray<T>> (value, ref state, synchronize);
			}
		}
	}

	partial class JniEnvironment {

		[SuppressMessage ("Design", "CA1034", Justification = "https://github.com/xamarin/Java.Interop/commit/bb7ca5d02aa3fc2b447ad57af1256e74e5f954fa")]
		partial class Arrays {

			public static JavaObjectArray<T>? CreateMarshalObjectArray<T> (IEnumerable<T>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaObjectArray<T> c) {
					return c;
				}
				return new JavaObjectArray<T> (value) {
					forMarshalCollection = true,
				};
			}
		}
	}
}

