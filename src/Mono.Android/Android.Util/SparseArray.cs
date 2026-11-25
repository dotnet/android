using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Android.Runtime;

using Java.Interop;

namespace Android.Util
{
	[Register ("android/util/SparseArray", DoNotGenerateAcw=true)]
	public partial class SparseArray<
			[DynamicallyAccessedMembers (Constructors)]
			E
	> : SparseArray
	{
		[UnsafeAccessor (UnsafeAccessorKind.StaticField, Name = "_members")]
		// Returns a typed, GC-tracked byref to the managed static field; readonly prevents replacing its reference.
		static safe extern ref readonly JniPeerMembers GetPeerMembers (SparseArray? _);

		public SparseArray ()
		{
		}
		
		public SparseArray (int capacity)
			: base (capacity)
		{
		}
		
		// https://developer.android.com/reference/android/util/SparseArray#append(int,%20E)
		[Register ("append", "(ILjava/lang/Object;)V", "")]
		public virtual void Append (int key, E value)
		{
			const string id = "append.(ILjava/lang/Object;)V";
			unsafe {
				// SAFETY: The stack arguments match id; the helpers retain this peer and the local argument through JNI.
				JavaConvert.WithLocalJniHandle (value, lref => {
					JniArgumentValue* args = stackalloc JniArgumentValue [2] {
						new JniArgumentValue (key),
						new JniArgumentValue (lref),
					};
					GetPeerMembers (null).InstanceMethods.InvokeVirtualVoidMethod (id, this, args);
					return IntPtr.Zero;
				});
			}
		}
		
		[Register ("get", "(I)Ljava/lang/Object;", "")]
		[return: MaybeNull]
		public new virtual E Get (int key)
		{
			const string id = "get.(I)Ljava/lang/Object;";
			unsafe {
				// SAFETY: The stack argument matches id; invocation retains this peer and the result local transfers to conversion.
				JniArgumentValue* args = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (key),
				};
				var result = GetPeerMembers (null).InstanceMethods.InvokeVirtualObjectMethod (id, this, args);
				return JavaConvert.FromJniHandle<E> (result.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}

		[Register ("get", "(ILjava/lang/Object;)Ljava/lang/Object;", "")]
		[return: MaybeNull]
		public virtual E Get (int key, E valueIfKeyNotFound)
		{
			const string id = "get.(ILjava/lang/Object;)Ljava/lang/Object;";
			unsafe {
				// SAFETY: The stack arguments match id; the helpers retain this peer and the local argument through JNI.
				var result = JavaConvert.WithLocalJniHandle (valueIfKeyNotFound, lref => {
					JniArgumentValue* args = stackalloc JniArgumentValue [2] {
						new JniArgumentValue (key),
						new JniArgumentValue (lref),
					};
					return GetPeerMembers (null).InstanceMethods.InvokeVirtualObjectMethod (id, this, args);
				});
				return JavaConvert.FromJniHandle<E> (result.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}

		[Register ("indexOfValue", "(Ljava/lang/Object;)I", "")]
		public virtual int IndexOfValue (E value)
		{
			const string id = "indexOfValue.(Ljava/lang/Object;)I";
			unsafe {
				// SAFETY: The stack argument matches id; the helpers retain this peer and the local argument through JNI.
				return JavaConvert.WithLocalJniHandle (value, lref => {
					JniArgumentValue* args = stackalloc JniArgumentValue [1] {
						new JniArgumentValue (lref),
					};
					return GetPeerMembers (null).InstanceMethods.InvokeVirtualInt32Method (id, this, args);
				});
			}
		}

		[Register ("put", "(ILjava/lang/Object;)V", "")]
		public virtual void Put (int key, E value)
		{
			const string id = "put.(ILjava/lang/Object;)V";
			unsafe {
				// SAFETY: The stack arguments match id; the helpers retain this peer and the local argument through JNI.
				JavaConvert.WithLocalJniHandle (value, lref => {
					JniArgumentValue* args = stackalloc JniArgumentValue [2] {
						new JniArgumentValue (key),
						new JniArgumentValue (lref),
					};
					GetPeerMembers (null).InstanceMethods.InvokeVirtualVoidMethod (id, this, args);
					return IntPtr.Zero;
				});
			}
		}

		[Register ("setValueAt", "(ILjava/lang/Object;)V", "")]
		public virtual void SetValueAt (int index, E value)
		{
			const string id = "setValueAt.(ILjava/lang/Object;)V";
			unsafe {
				// SAFETY: The stack arguments match id; the helpers retain this peer and the local argument through JNI.
				JavaConvert.WithLocalJniHandle (value, lref => {
					JniArgumentValue* args = stackalloc JniArgumentValue [2] {
						new JniArgumentValue (index),
						new JniArgumentValue (lref),
					};
					GetPeerMembers (null).InstanceMethods.InvokeVirtualVoidMethod (id, this, args);
					return IntPtr.Zero;
				});
			}
		}
		
		[Register ("valueAt", "(I)Ljava/lang/Object;", "")]
		[return: MaybeNull]
		public new virtual E ValueAt (int index)
		{
			const string id = "valueAt.(I)Ljava/lang/Object;";
			unsafe {
				// SAFETY: The stack argument matches id; invocation retains this peer and the result local transfers to conversion.
				JniArgumentValue* args = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (index),
				};
				var result = GetPeerMembers (null).InstanceMethods.InvokeVirtualObjectMethod (id, this, args);
				return JavaConvert.FromJniHandle<E> (result.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}
	}
}
