using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Java.Interop {

	public class ExportedMemberBuilder : IExportedMemberBuilder
	{
		public ExportedMemberBuilder (JavaVM javaVM = null)
		{
			JavaVM = javaVM;
		}

		public JavaVM JavaVM {get; private set;}

		public IEnumerable<JniNativeMethodRegistration> GetExportedMemberRegistrations (Type declaringType)
		{
			if (declaringType == null)
				throw new ArgumentNullException ("declaringType");
			return CreateExportedMemberRegistrationIterator (declaringType);
		}

		IEnumerable<JniNativeMethodRegistration> CreateExportedMemberRegistrationIterator (Type declaringType)
		{
			const BindingFlags methodScope = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			foreach (var method in declaringType.GetMethods (methodScope)) {
				var exports = (ExportAttribute[]) method.GetCustomAttributes (typeof(ExportAttribute), inherit:false);
				if (exports == null || exports.Length == 0)
					continue;
				var export  = exports [0];
				yield return CreateMarshalFromJniMethodRegistration (export, declaringType, method);
			}
		}

		public JniNativeMethodRegistration CreateMarshalFromJniMethodRegistration (ExportAttribute export, Type type, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (type == null)
				throw new ArgumentNullException ("type");
			if (method == null)
				throw new ArgumentNullException ("method");

			return new JniNativeMethodRegistration () {
				Name        = GetJniMethodName (export, method),
				Signature   = GetJniMethodSignature (export, method),
				Marshaler   = CreateJniMethodMarshaler (export, type, method),
			};
		}

		protected virtual string GetJniMethodName (ExportAttribute export, MethodInfo method)
		{
			return export.Name ?? "n_" + method.Name;
		}

		protected virtual string GetJniMethodSignature (ExportAttribute export, MethodInfo method)
		{
			if (export.Signature != null)
				return export.Signature;
			throw new NotSupportedException ("parameter deduction not yet implemented.");
		}

		Delegate CreateJniMethodMarshaler (ExportAttribute export, Type type, MethodInfo method)
		{
			var e = CreateMarshalFromJniMethodExpression (export, type, method);
			return e.Compile ();
		}

		// TODO: make internal, and add [InternalsVisibleTo] for Java.Interop.Export-Tests
		public virtual LambdaExpression CreateMarshalFromJniMethodExpression (ExportAttribute export, Type type, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (type == null)
				throw new ArgumentNullException ("type");
			if (method == null)
				throw new ArgumentNullException ("method");

			var methodParameters = method.GetParameters ();

			var jnienv  = Expression.Parameter (typeof (IntPtr), "__jnienv");
			var context = Expression.Parameter (typeof (IntPtr), "__context");

			var jvm         = Expression.Variable (typeof (JavaVM), "__jvm");
			var variables   = new List<ParameterExpression> () {
				jvm,
			};

			var marshalBody = new List<Expression> () {
				CheckJnienv (jnienv),
				Expression.Assign (jvm, GetJavaVM ()),
			};

			ParameterExpression self = null;
			if (!method.IsStatic) {
				self    = Expression.Variable (type, "__this");
				variables.Add (self);
				marshalBody.Add (Expression.Assign (self, GetThis (jvm, type, context)));
			}

			var marshalParameters   = new List<ParameterExpression> (methodParameters.Length);
			var invokeParameters    = new List<ParameterExpression> (methodParameters.Length);
			for (int i = 0; i < methodParameters.Length; ++i) {
				var jni = GetMarshalFromJniParameterType (methodParameters [i].ParameterType);
				if (jni == methodParameters [i].ParameterType) {
					var p   = Expression.Parameter (jni, methodParameters [i].Name);
					marshalParameters.Add (p);
					invokeParameters.Add (p);
				}
				else {
					var np      = Expression.Parameter (jni, "native_" + methodParameters [i].Name);
					var p       = Expression.Variable (methodParameters [i].ParameterType, methodParameters [i].Name);
					var fromJni = GetMarshalFromJniExpression (jvm, p.Type, np);
					if (fromJni == null)
						throw new NotSupportedException (string.Format ("Cannot convert from '{0}' to '{1}'.", jni, methodParameters [i].ParameterType));
					variables.Add (p);
					marshalParameters.Add (np);
					invokeParameters.Add (p);
					marshalBody.Add (Expression.Assign (p, fromJni));
				}
			}

			Expression invoke = method.IsStatic
				? Expression.Call (method, invokeParameters)
				: Expression.Call (self, method, invokeParameters);
			ParameterExpression ret = null;
			if (method.ReturnType == typeof (void)) {
				marshalBody.Add (invoke);
			} else {
				var jniRType    = GetMarshalToJniReturnType (method.ReturnType);
				var exit        = Expression.Label (jniRType, "__exit");
				ret             = Expression.Variable (jniRType, "__jret");
				var mret        = Expression.Variable (method.ReturnType, "__mret");
				variables.Add (ret);
				variables.Add (mret);
				marshalBody.Add (Expression.Assign (mret, invoke));
				if (jniRType == method.ReturnType)
					marshalBody.Add (Expression.Assign (ret, mret));
				else {
					var marshalExpr = GetMarshalToJniExpression (method.ReturnType, mret);
					if (marshalExpr == null)
						throw new NotSupportedException (string.Format ("Don't know how to marshal '{0}' to '{1}'.",
								method.ReturnType, jniRType));
					marshalBody.Add (Expression.Assign (ret, marshalExpr));
				}
				marshalBody.Add (Expression.Return (exit, ret));
				marshalBody.Add (Expression.Label (exit, ret));
			}


			var funcTypeParams = new List<Type> () {
				typeof (IntPtr),
				typeof (IntPtr),
			};
			foreach (var p in marshalParameters)
				funcTypeParams.Add (p.Type);
			if (ret != null)
				funcTypeParams.Add (ret.Type);
			var marshalerType = ret == null
				? Expression.GetActionType (funcTypeParams.ToArray ())
				: Expression.GetFuncType (funcTypeParams.ToArray ());

			var bodyParams = new List<ParameterExpression> { jnienv, context };
			bodyParams.AddRange (marshalParameters);
			var body = Expression.Block (variables, marshalBody);
			return Expression.Lambda (marshalerType, body, bodyParams);
		}

		protected virtual Type GetMarshalFromJniParameterType (Type type)
		{
			if (JniBuiltinTypes.Contains (type))
				return type;
			return typeof (IntPtr);
		}

		protected virtual Type GetMarshalToJniReturnType (Type type)
		{
			if (JniBuiltinTypes.Contains (type))
				return type;
			return typeof (IntPtr);
		}

		protected virtual Expression GetMarshalFromJniExpression (Expression jvm, Type targetType, Expression jniParameter)
		{
			MarshalInfo v;
			if (Marshalers.TryGetValue (targetType, out v))
				return v.FromJni (jvm, targetType, jniParameter);
			if (typeof (IJavaObject).IsAssignableFrom (targetType))
				return Marshalers [typeof (IJavaObject)].FromJni (jvm, targetType, jniParameter);
			return null;
		}

		protected virtual Expression GetMarshalToJniExpression (Type sourceType, Expression managedParameter)
		{
			MarshalInfo v;
			if (Marshalers.TryGetValue (sourceType, out v))
				return v.ToJni (managedParameter);
			if (typeof (IJavaObject).IsAssignableFrom (sourceType))
				return Marshalers [typeof (IJavaObject)].ToJni (managedParameter);
			return null;
		}

		static readonly Dictionary<Type, MarshalInfo> Marshalers = new Dictionary<Type, MarshalInfo> () {
			{ typeof (string), new MarshalInfo {
					FromJni = (vm, t, p) => Expression.Call (F<IntPtr, string> (JniEnvironment.Strings.ToString).Method, p),
					ToJni   = p => Expression.Call (F<string, JniLocalReference> (JniEnvironment.Strings.NewString).Method, p)
			} },
			{ typeof (IJavaObject), new MarshalInfo {
					FromJni = (vm, t, p) => GetThis (vm, t, p),
					ToJni   = p => Expression.Call (F<IJavaObject, IntPtr> (JniEnvironment.Handles.NewReturnToJniRef).Method, p)
			} },
		};

		static Func<T, TRet> F<T, TRet> (Func<T, TRet> func)
		{
			return func;
		}

		static Expression CheckJnienv (ParameterExpression jnienv)
		{
			Action<IntPtr> a = JniEnvironment.CheckCurrent;
			return Expression.Call (null, a.Method, jnienv);
		}

		static Expression GetThis (Expression vm, Type targetType, Expression context)
		{
			return Expression.Call (
					vm,
					"GetObject",
					new[]{targetType},
					context);
		}

		static Expression GetJavaVM ()
		{
			var env     = typeof (JniEnvironment);
			var cenv    = Expression.Property (null, env, "Current");
			var vm      = Expression.Property (cenv, "JavaVM");
			return vm;
		}

		static readonly ISet<Type> JniBuiltinTypes = new HashSet<Type> {
			typeof (IntPtr),
			typeof (void),
			typeof (bool),
			typeof (sbyte),
			typeof (char),
			typeof (short),
			typeof (int),
			typeof (long),
			typeof (float),
			typeof (double),
		};
	}

	class MarshalInfo {

		public Func<Expression /* vm */, Type /* targetType */, Expression /* value */, Expression /* managed rep */>    FromJni;
		public Func<Expression /* managed rep */, Expression /* jni rep */>    ToJni;
	}
}

