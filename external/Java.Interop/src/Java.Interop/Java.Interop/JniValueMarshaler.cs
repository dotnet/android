using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Java.Interop {

	public struct JniValueMarshalerState : IEquatable<JniValueMarshalerState> {

		public  JniArgumentValue        JniArgumentValue    {get; private set;}
		public  JniObjectReference      ReferenceValue      {get; private set;}
		public  IJavaPeerable           PeerableValue       {get; private set;}
		public  object                  Extra               {get; private set;}

		public JniValueMarshalerState (JniArgumentValue jniArgumentValue, object extra = null)
		{
			JniArgumentValue    = jniArgumentValue;
			ReferenceValue      = default (JniObjectReference);
			PeerableValue       = null;
			Extra               = extra;
		}

		public JniValueMarshalerState (JniObjectReference referenceValue, object extra = null)
		{
			JniArgumentValue    = new JniArgumentValue (referenceValue);
			ReferenceValue      = referenceValue;
			PeerableValue       = null;
			Extra               = extra;
		}

		public JniValueMarshalerState (IJavaPeerable peerableValue, object extra = null)
		{
			PeerableValue       = peerableValue;
			ReferenceValue      = peerableValue == null ? default (JniObjectReference) : peerableValue.PeerReference;
			JniArgumentValue    = new JniArgumentValue (ReferenceValue);
			Extra               = extra;
		}

		internal JniValueMarshalerState (JniValueMarshalerState copy, object extra = null)
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

		public override bool Equals (object obj)
		{
			var o = obj as JniValueMarshalerState?;
			if (!o.HasValue)
				return false;
			return Equals (o.Value);
		}

		public bool Equals (JniValueMarshalerState value)
		{
			return JniArgumentValue.Equals (value.JniArgumentValue) &&
				ReferenceValue.Equals (value.ReferenceValue) &&
				object.ReferenceEquals (PeerableValue, value.PeerableValue) &&
				object.ReferenceEquals (Extra, value.Extra);
		}

		public override string ToString ()
		{
			return string.Format ("JniValueMarshalerState({0}, ReferenceValue={1}, PeerableValue=0x{2}, Extra={3})",
					JniArgumentValue.ToString (),
					ReferenceValue.ToString (),
					RuntimeHelpers.GetHashCode (PeerableValue).ToString ("x"),
					Extra);
		}
	}

	public abstract class JniValueMarshaler {

		public  virtual     bool                    IsJniValueType {
			get {return false;}
		}

		public  abstract    object                  CreateValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null);

		public  virtual     JniValueMarshalerState  CreateArgumentState (object value, ParameterAttributes synchronize = 0)
		{
			return CreateObjectReferenceArgumentState (value, synchronize);
		}

		public  abstract    JniValueMarshalerState  CreateObjectReferenceArgumentState (object value, ParameterAttributes synchronize = 0);
		public  abstract    void                    DestroyArgumentState (object value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0);
	}

	public abstract class JniValueMarshaler<T> : JniValueMarshaler {

		public  abstract    T                       CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null);

		public  virtual     JniValueMarshalerState  CreateGenericArgumentState (T value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericObjectReferenceArgumentState (value, synchronize);
		}

		public  abstract    JniValueMarshalerState  CreateGenericObjectReferenceArgumentState (T value, ParameterAttributes synchronize = 0);
		public  abstract    void                    DestroyGenericArgumentState (T value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0);

		public override object CreateValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null)
		{
			return CreateGenericValue (ref reference, options, targetType ?? typeof (T));
		}

		public override JniValueMarshalerState CreateArgumentState (object value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericArgumentState ((T) value, synchronize);
		}

		public override JniValueMarshalerState CreateObjectReferenceArgumentState (object value, ParameterAttributes synchronize = 0)
		{
			return CreateGenericObjectReferenceArgumentState ((T) value, synchronize);
		}

		public override void DestroyArgumentState (object value, ref JniValueMarshalerState state, ParameterAttributes synchronize = 0)
		{
			DestroyGenericArgumentState ((T) value, ref state, synchronize);
		}
	}
}
