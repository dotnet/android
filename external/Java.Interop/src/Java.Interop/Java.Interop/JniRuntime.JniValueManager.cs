#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Java.Interop
{
	public class JniSurfacedPeerInfo {

		public  int                             JniIdentityHashCode     {get; private set;}
		public  WeakReference<IJavaPeerable>    SurfacedPeer            {get; private set;}

		public JniSurfacedPeerInfo (int jniIdentityHashCode, WeakReference<IJavaPeerable> surfacedPeer)
		{
			JniIdentityHashCode     = jniIdentityHashCode;
			SurfacedPeer            = surfacedPeer;
		}
	}

	partial class JniRuntime
	{
		partial class CreationOptions {
			public  JniValueManager?        ValueManager                {get; set;}
		}

		internal    JniValueManager?        valueManager;
		public  JniValueManager             ValueManager                {
			get => valueManager ?? throw new NotSupportedException ();
		}

		partial void SetValueManager (CreationOptions options)
		{
			var manager     = options.ValueManager;
			if (manager == null)
				throw new ArgumentException (
						"No JniValueManager specified in JniRuntime.CreationOptions.ValueManager.",
						nameof (options));
			valueManager    = SetRuntime (manager);
		}

		public abstract partial class JniValueManager : ISetRuntime, IDisposable {
			internal const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

			JniRuntime?             runtime;
			bool                    disposed;
			public      JniRuntime  Runtime {
				get => runtime ?? throw new NotSupportedException ();
			}

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				if (disposed)
					throw new ObjectDisposedException (GetType ().Name);

				this.runtime = runtime;
			}

			public void Dispose ()
			{
				Dispose (true);
				GC.SuppressFinalize (this);
			}

			protected virtual void Dispose (bool disposing)
			{
				disposed = true;
			}

			protected void EnsureNotDisposed ()
			{
				if (disposed)
					throw new ObjectDisposedException (GetType ().Name);
			}

			public abstract void WaitForGCBridgeProcessing ();
			public abstract void CollectPeers ();
			public abstract void AddPeer (IJavaPeerable value);
			public abstract void RemovePeer (IJavaPeerable value);
			public abstract void FinalizePeer (IJavaPeerable value);
			public abstract IJavaPeerable? PeekPeer (JniObjectReference reference);
			public abstract List<JniSurfacedPeerInfo> GetSurfacedPeers ();
			public abstract void ActivatePeer (
				JniObjectReference reference,
				Type type,
				ConstructorInfo cinfo,
				object?[]? argumentValues);

			public void ConstructPeer (IJavaPeerable peer, ref JniObjectReference reference, JniObjectReferenceOptions options)
			{
				ConstructPeerCore (peer, ref reference, options);
			}

			protected abstract void ConstructPeerCore (IJavaPeerable peer, ref JniObjectReference reference, JniObjectReferenceOptions options);

			public int GetJniIdentityHashCode (JniObjectReference reference)
			{
				return JniSystem.IdentityHashCode (reference);
			}

			public virtual void DisposePeer (IJavaPeerable value)
			{
				EnsureNotDisposed ();

				if (value == null)
					throw new ArgumentNullException (nameof (value));

				if (!value.PeerReference.IsValid)
					return;

				value.Disposed ();
				RemovePeer (value);

				var h = value.PeerReference;
				if (!h.IsValid)
					return;

				DisposePeer (h, value);
			}

			void DisposePeer (JniObjectReference h, IJavaPeerable value)
			{
				EnsureNotDisposed ();

				var o = Runtime.ObjectReferenceManager;
				if (o.LogGlobalReferenceMessages) {
					o.WriteGlobalReferenceLine ("Disposing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3} Java.Type={4}",
							h.ToString (),
							value.JniIdentityHashCode.ToString ("x"),
							RuntimeHelpers.GetHashCode (value).ToString ("x"),
							value.GetType ().ToString (),
							JniEnvironment.Types.GetJniTypeNameFromInstance (h));
				}
				JniObjectReference.Dispose (ref h);
				value.SetPeerReference (new JniObjectReference ());
				GC.SuppressFinalize (value);
			}

			public virtual void DisposePeerUnlessReferenced (IJavaPeerable value)
			{
				EnsureNotDisposed ();

				if (value == null)
					throw new ArgumentNullException (nameof (value));

				var h = value.PeerReference;
				if (!h.IsValid)
					return;

				var o = PeekPeer (h);
				if (o != null && object.ReferenceEquals (o, value))
					return;

				DisposePeer (h, value);
			}

			public object? PeekValue (JniObjectReference reference)
			{
				EnsureNotDisposed ();

				if (!reference.IsValid)
					return null;

				var t   = PeekPeer (reference);
				if (t == null)
					return t;

				object? r;
				return TryUnboxPeerObject (t, out r)
					? r
					: t;
			}

			protected virtual bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)] out object? result)
			{
				result  = null;
				var p   = value as JavaProxyObject;
				if (p != null) {
					result  = p.Value;
					return true;
				}
				var x   = value as JavaProxyThrowable;
				if (x != null) {
					result  = x.Exception;
					return true;
				}
				return false;
			}

			public IJavaPeerable? GetPeer (
				JniObjectReference reference,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
			{
				EnsureNotDisposed ();

				if (!reference.IsValid) {
					return null;
				}

				var peeked  = PeekPeer (reference);
				if (peeked != null &&
						(targetType == null ||
							targetType.IsAssignableFrom (peeked.GetType ()))) {
					return peeked;
				}
				return CreatePeer (ref reference, JniObjectReferenceOptions.Copy, targetType);
			}

			public abstract IJavaPeerable? CreatePeer (
				ref JniObjectReference reference,
				JniObjectReferenceOptions transfer,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType);

			public object? CreateValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
			{
				EnsureNotDisposed ();

				return CreateValueCore (ref reference, options, targetType);
			}

			[return: MaybeNull]
			public T CreateValue<[DynamicallyAccessedMembers (Constructors)] T> (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
			{
				EnsureNotDisposed ();

				return CreateValueCore<T> (ref reference, options, targetType);
			}

			[return: MaybeNull]
			protected abstract T CreateValueCore<[DynamicallyAccessedMembers (Constructors)] T> (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null);

			protected abstract object? CreateValueCore (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null);

			public object? GetValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
			{
				EnsureNotDisposed ();

				return GetValueCore (ref reference, options, targetType);
			}

			[return: MaybeNull]
			public T GetValue<[DynamicallyAccessedMembers (Constructors)] T> (IntPtr handle)
			{
				var r   = new JniObjectReference (handle);
				return GetValue<T> (ref r, JniObjectReferenceOptions.Copy);
			}

			[return: MaybeNull]
			public T GetValue<[DynamicallyAccessedMembers (Constructors)] T> (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
			{
				EnsureNotDisposed ();

				return GetValueCore<T> (ref reference, options, targetType);
			}

			[return: MaybeNull]
			protected abstract T GetValueCore<[DynamicallyAccessedMembers (Constructors)] T> (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null);

			protected abstract object? GetValueCore (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null);

			internal Type? GetRuntimeType (JniObjectReference reference)
			{
				if (!reference.IsValid)
					return null;
				JniTypeSignature signature;
				if (!JniTypeSignature.TryParse (JniEnvironment.Types.GetJniTypeNameFromInstance (reference)!, out signature))
					return null;
				return Runtime.TypeManager.GetType (signature);
			}

			public JniValueMarshaler GetValueMarshaler (Type type) => GetValueMarshalerCore (type);
			protected abstract JniValueMarshaler GetValueMarshalerCore (Type type);

			public JniValueMarshaler<T> GetValueMarshaler<T> () => GetValueMarshalerCore<T> ();
			protected abstract JniValueMarshaler<T> GetValueMarshalerCore<T> ();

			internal JniObjectReference CreateLocalObjectReferenceArgument (Type type, object? value)
			{
				EnsureNotDisposed ();

				if (type == null)
					throw new ArgumentNullException (nameof (type));
				return CreateLocalObjectReferenceArgumentCore (type, value);
			}

			protected abstract JniObjectReference CreateLocalObjectReferenceArgumentCore (Type type, object? value);
		}
	}
}
