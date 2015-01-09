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

			var parameters  = methodParameters
				.Select (p => Expression.Parameter (p.ParameterType, p.Name))
				.ToList ();
			var envp        = Expression.Variable (typeof (JniEnvironment), "__envp");
			var variables   = new List<ParameterExpression> () {
				envp,
			};

			var jnienv  = parameters [0];
			MethodCallExpression invoke;
			if (value.Target == null) {
				invoke = Expression.Call (value.Method, parameters);
			} else {
				var delArgs = new List<Expression> () {
					Expression.Constant (value.Target),
				};
				delArgs.AddRange (parameters);
				invoke = Expression.Call (value.Method, delArgs);
			}
			var body    = new List<Expression> () {
				Expression.Assign (envp, CreateJniEnvironment (jnienv)),
			};

			if (delegateType.ReturnType == typeof (void)) {
				body.Add (Expression.TryCatchFinally (
					invoke,
					CreateDisposeJniEnvironment (envp),
					CreateMarshalException (envp, delegateType, null)));
			} else {
				var jniRType    = delegateType.ReturnType;
				var exit        = Expression.Label (jniRType, "__exit");
				body.Add (Expression.TryCatchFinally (
					Expression.Return (exit, invoke),
					CreateDisposeJniEnvironment (envp),
					CreateMarshalException (envp, delegateType, exit)));
				body.Add (Expression.Label (exit, Expression.Default (jniRType)));
			}

			var block = Expression.Block (variables, body);
			var funcT   = methodParameters.Select (p => p.ParameterType).ToList ();
			funcT.Add (delegateType.ReturnType);
			var marshalerType = Expression.GetDelegateType (funcT.ToArray ());
			return Expression.Lambda (marshalerType, block, parameters);
		}

		static Expression CreateJniEnvironment (ParameterExpression jnienv)
		{
			return Expression.New (
					typeof (JniEnvironment).GetConstructor (new []{typeof (IntPtr)}),
					jnienv);
		}

		static CatchBlock CreateMarshalException  (ParameterExpression envp, MethodInfo method, LabelTarget exit)
		{
			var spe     = typeof (JniEnvironment).GetMethod ("SetPendingException");
			var ex      = Expression.Variable (typeof (Exception), "__e");
			var body = new List<Expression> () {
				Expression.Call (envp, spe, ex),
			};
			if (exit != null) {
				body.Add (Expression.Return (exit, Expression.Default (method.ReturnType)));
			}
			return Expression.Catch (ex, Expression.Block (body));
		}

		static Expression CreateDisposeJniEnvironment (ParameterExpression envp)
		{
			return Expression.Call (envp, typeof (JniEnvironment).GetMethod ("Dispose"));
		}
	}
}
