#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using Java.Interop.Expressions;

namespace Java.Interop {

	sealed class JniStringValueMarshaler : JniValueMarshaler<string?> {

		internal    static  readonly    JniStringValueMarshaler     Instance    = new JniStringValueMarshaler ();

		public override string? CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
		{
			return JniEnvironment.Strings.ToString (ref reference, options, targetType ?? typeof (string));
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull]string? value, ParameterAttributes synchronize)
		{
			var r   = JniEnvironment.Strings.NewString (value);
			return new JniValueMarshalerState (r);
		}

		public override void DestroyGenericArgumentState (string? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			var r   = state.ReferenceValue;
			JniObjectReference.Dispose (ref r);
			state   = new JniValueMarshalerState ();
		}

		public override Expression CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			Func<string, JniObjectReference>    m   = JniEnvironment.Strings.NewString;

			var obj = Expression.Variable (typeof (JniObjectReference), sourceValue.Name + "_ref");
			var hdl = Expression.Variable (MarshalType, sourceValue.Name + "_handle");
			context.LocalVariables.Add (obj);
			context.LocalVariables.Add (hdl);
			context.CreationStatements.Add (Expression.Assign (obj, Expression.Call (m.GetMethodInfo (), sourceValue)));
			context.CreationStatements.Add (Expression.Assign (hdl, Expression.Property (obj, "Handle")));
			context.CleanupStatements.Add (DisposeObjectReference (obj));
			return hdl;
		}

		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			Func<string, JniObjectReference>    m   = JniEnvironment.Strings.NewString;

			var obj = Expression.Variable (typeof (JniObjectReference), sourceValue.Name + "_ref");
			context.LocalVariables.Add (obj);
			context.CreationStatements.Add (Expression.Assign (obj, Expression.Call (m.GetMethodInfo (), sourceValue)));
			context.CleanupStatements.Add (DisposeObjectReference (obj));
			return ReturnObjectReferenceToJni (context, sourceValue.Name, obj);
		}

		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type? targetType)
		{
			Func<IntPtr, string?>   m   = JniEnvironment.Strings.ToString;

			targetType ??= typeof (object);

			var value = Expression.Variable (targetType, sourceValue.Name + "_val");
			context.LocalVariables.Add (value);
			context.CreationStatements.Add (Expression.Assign (value, Expression.Call (m.GetMethodInfo (), sourceValue)));

			return value;
		}
	}
}
