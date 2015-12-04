using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Java.Interop {

	class JniStringValueMarshaler : JniValueMarshaler<string> {

		internal    static  readonly    JniStringValueMarshaler     Instance    = new JniStringValueMarshaler ();

		public override string CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			return JniEnvironment.Strings.ToString (ref reference, options, targetType ?? typeof (string));
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (string value, ParameterAttributes synchronize)
		{
			var r   = JniEnvironment.Strings.NewString (value);
			return new JniValueMarshalerState (r);
		}

		public override void DestroyGenericArgumentState (string value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			var r   = state.ReferenceValue;
			JniObjectReference.Dispose (ref r);
			state   = new JniValueMarshalerState ();
		}
	}
}
