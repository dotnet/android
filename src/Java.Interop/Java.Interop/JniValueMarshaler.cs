#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Java.Interop {

	public struct JniValueMarshalerState : IEquatable<JniValueMarshalerState> {

		public  JniArgumentValue        JniArgumentValue    {get; private set;}
		public  JniObjectReference      ReferenceValue      {get; private set;}
		public  IJavaPeerable?          PeerableValue       {get; private set;}
		public  object?                 Extra               {get; private set;}

		[SuppressMessage ("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Existing public API")]
		public JniValueMarshalerState (JniArgumentValue jniArgumentValue, object? extra = null)
		{
			JniArgumentValue    = jniArgumentValue;
			ReferenceValue      = default (JniObjectReference);
			PeerableValue       = null;
			Extra               = extra;
		}

		[SuppressMessage ("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Existing public API")]
		public JniValueMarshalerState (JniObjectReference referenceValue, object? extra = null)
		{
			JniArgumentValue    = new JniArgumentValue (referenceValue);
			ReferenceValue      = referenceValue;
			PeerableValue       = null;
			Extra               = extra;
		}

		[SuppressMessage ("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Existing public API")]
		public JniValueMarshalerState (IJavaPeerable? peerableValue, object? extra = null)
		{
			PeerableValue       = peerableValue;
			ReferenceValue      = peerableValue == null ? default (JniObjectReference) : peerableValue.PeerReference;
			JniArgumentValue    = new JniArgumentValue (ReferenceValue);
			Extra               = extra;
		}

		internal JniValueMarshalerState (JniValueMarshalerState copy, object? extra = null)
		{
			JniArgumentValue    = copy.JniArgumentValue;
			ReferenceValue      = copy.ReferenceValue;
			PeerableValue       = copy.PeerableValue;
			Extra               = extra ?? copy.Extra;
		}

		public override int GetHashCode ()
		{
			return JniArgumentValue.GetHashCode ();
		}

		public override bool Equals (object? obj)
		{
			var o = obj as JniValueMarshalerState?;
			if (!o.HasValue)
				return false;
			return Equals (o.Value);
		}

		public bool Equals (JniValueMarshalerState other)
		{
			return JniArgumentValue.Equals (other.JniArgumentValue) &&
				ReferenceValue.Equals (other.ReferenceValue) &&
				object.ReferenceEquals (PeerableValue, other.PeerableValue) &&
				object.ReferenceEquals (Extra, other.Extra);
		}

		public static bool operator == (JniValueMarshalerState a, JniValueMarshalerState b) => a.Equals (b);
		public static bool operator != (JniValueMarshalerState a, JniValueMarshalerState b) => !a.Equals (b);

		public override string ToString ()
		{
			return string.Format ("JniValueMarshalerState({0}, ReferenceValue={1}, PeerableValue=0x{2}, Extra={3})",
					JniArgumentValue.ToString (),
					ReferenceValue.ToString (),
					RuntimeHelpers.GetHashCode (PeerableValue!).ToString ("x"),
					Extra);
		}
	}

	public abstract class JniValueMarshaler {
		public  virtual     bool                    IsJniValueType {
			get {return false;}
		}

		static  readonly    Type                    IntPtr_type     = typeof(IntPtr);
		public  virtual     Type                    MarshalType {
			get {return IntPtr_type;}
		}

		public  abstract    object?                 CreateValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				Type? targetType = null);

		public  virtual     JniValueMarshalerState  CreateArgumentState (object? value, ParameterAttributes synchronize = 0)
		{
			return CreateObjectReferenceArgumentState (value, synchronize);
		}

		public  abstract    JniValueMarshalerState  CreateObjectReferenceArgumentState (object? value, ParameterAttributes synchronize = 0);
		public  abstract    void                    DestroyArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0);

		internal object? CreateValue (
				IntPtr handle,
				Type? targetType)
		{
			var r = new JniObjectReference (handle);
			return CreateValue (ref r, JniObjectReferenceOptions.Copy, targetType);
		}
	}

	public abstract class JniValueMarshaler<T> : JniValueMarshaler
	{

		[return: MaybeNull]
		public  abstract    T                       CreateGenericValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				Type? targetType = null);

		public  virtual     JniValueMarshalerState  CreateGenericArgumentState ([MaybeNull] T value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericObjectReferenceArgumentState (value, synchronize);
		}

		public  abstract    JniValueMarshalerState  CreateGenericObjectReferenceArgumentState ([MaybeNull] T value, ParameterAttributes synchronize = 0);
		public  abstract    void                    DestroyGenericArgumentState ([AllowNull] T value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0);

		public override object? CreateValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				Type? targetType = null)
		{
			return CreateGenericValue (ref reference, options, targetType ?? typeof (T));
		}

		public override JniValueMarshalerState CreateArgumentState (object? value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericArgumentState ((T) value!, synchronize);
		}

		public override JniValueMarshalerState CreateObjectReferenceArgumentState (object? value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericObjectReferenceArgumentState ((T) value!, synchronize);
		}

		public override void DestroyArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0)
		{
			DestroyGenericArgumentState ((T) value!, ref state, synchronize);
		}
	}
}
