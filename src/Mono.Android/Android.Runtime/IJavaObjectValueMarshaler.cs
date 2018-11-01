using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Java.Interop;
using Java.Interop.Expressions;

namespace Android.Runtime
{
	sealed class IJavaObjectValueMarshaler : JniValueMarshaler<IJavaObject> {

		internal    static  IJavaObjectValueMarshaler              Instance    = new IJavaObjectValueMarshaler ();

		public override IJavaObject CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			throw new NotImplementedException ();
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (IJavaObject value, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}

		public override void DestroyGenericArgumentState (IJavaObject value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}

		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			return Expression.Call (
				typeof (JNIEnv),
				"ToLocalJniHandle",
				null,
				sourceValue);
		}

		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type targetType)
		{
			var r   = Expression.Variable (targetType, sourceValue.Name + "_val");
			context.LocalVariables.Add (r);
			context.CreationStatements.Add (
					Expression.Assign (r,
						Expression.Call (
					                typeof (JavaConvert),
					                "FromJniHandle",
							new[]{targetType},
					                sourceValue,
					                Expression.Field (null, typeof (JniHandleOwnership), "DoNotTransfer"))));
			return r;
		}
	}
}
