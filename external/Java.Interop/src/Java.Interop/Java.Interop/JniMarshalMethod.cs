using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public static class JniMarshalMethod {

		internal static Delegate Wrap (Delegate value)
		{
			return CreateMarshalMethodExpression (value)
				.Compile ();
		}

		public static LambdaExpression CreateMarshalMethodExpression (Delegate value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");

			var invoke = value.GetType ().GetMethod ("Invoke", BindingFlags.Public | BindingFlags.Instance);
			if (invoke == null)
				throw new NotSupportedException ("Cannot find Invoke() method on type: " + value.GetType ());

			return CreateMarshalMethodExpression (invoke, value);
		}

		static LambdaExpression CreateMarshalMethodExpression (MethodInfo delegateType, Delegate value)
		{
			if (delegateType == null)
				throw new ArgumentNullException ("method");

			var methodParameters = delegateType.GetParameters ();

			// sanity; needed?
			if (methodParameters.Length < 2)
				throw new NotSupportedException ("What kind of JNI marshal method is this where it has < 2 parameters?! (jnienv, and jclass/jobject are required).");
			if (methodParameters [0].ParameterType != typeof(IntPtr))
				throw new NotSupportedException ("What kind of JNI marshal method is this where the first parameter isn't an IntPtr?! Is: " + methodParameters [0].ParameterType);

			var variables   = methodParameters
				.Select (p => Expression.Parameter (p.ParameterType, p.Name))
				.ToList ();

			var jnienv  = variables [0];
			MethodCallExpression invoke;
			if (value.Target == null) {
				invoke = Expression.Call (value.Method, variables);
			} else {
				var delArgs = new List<Expression> () {
					Expression.Constant (value.Target),
				};
				delArgs.AddRange (variables);
				invoke = Expression.Call (value.Method, delArgs);
			}
			var body    = new List<Expression> () {
				CheckJnienv (jnienv),
			};

			if (delegateType.ReturnType == typeof (void)) {
				body.Add (Expression.TryCatch (
					invoke,
					CreateMarshalException (delegateType, null)));
			} else {
				var jniRType    = delegateType.ReturnType;
				var exit        = Expression.Label (jniRType, "__exit");
				body.Add (Expression.TryCatch (
					Expression.Return (exit, invoke),
					CreateMarshalException (delegateType, exit)));
				body.Add (Expression.Label (exit, Expression.Default (jniRType)));
			}

			var block = Expression.Block (body);
			var funcT   = methodParameters.Select (p => p.ParameterType).ToList ();
			funcT.Add (delegateType.ReturnType);
			var marshalerType = Expression.GetDelegateType (funcT.ToArray ());
			return Expression.Lambda (marshalerType, block, variables);
		}

		static Expression CheckJnienv (ParameterExpression jnienv)
		{
			Action<IntPtr> a = JniEnvironment.CheckCurrent;
			return Expression.Call (null, a.Method, jnienv);
		}

		static CatchBlock CreateMarshalException  (MethodInfo method, LabelTarget exit)
		{
			Action<Exception>   a = JniEnvironment.Errors.Throw;
			var ex      = Expression.Variable (typeof (Exception), "__e");
			var body = new List<Expression> () {
				Expression.Call (a.Method, ex),
			};
			if (exit != null) {
				body.Add (Expression.Return (exit, Expression.Default (method.ReturnType)));
			}
			return Expression.Catch (ex, Expression.Block (body));
		}
	}
}
