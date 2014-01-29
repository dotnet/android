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
				throw new ArgumentNullException ("type");
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

			var ptypes = method.GetParameters ()
				.Select (p => Expression.Parameter (GetMarshalFromJniParameterType (p.ParameterType), p.Name))
				.ToList ();
			var jnienv  = Expression.Parameter (typeof (IntPtr), "__jnienv");
			var context = Expression.Parameter (typeof (IntPtr), "__context");

			var marshalBody = new List<Expression> () {
				CheckJnienv (jnienv),
			};

			if (method.IsStatic)
				marshalBody.Add (Expression.Call (method, ptypes));
			else {
				var instance = GetThis (context, type);
				marshalBody.Add (Expression.Call (instance, method, ptypes));
			}
			var funcTypeParams = new List<Type> () {
				typeof (IntPtr),
				typeof (IntPtr),
			};
			foreach (var p in method.GetParameters ())
				funcTypeParams.Add (p.ParameterType);
			var marshalerType = (Type) null;
			if (method.ReturnType == typeof(void))
				marshalerType = Expression.GetActionType (funcTypeParams.ToArray ());
			else {
				funcTypeParams.Add (GetMarshalToJniReturnType (method.ReturnType));
				marshalerType = Expression.GetFuncType (funcTypeParams.ToArray ());
			}
			var bodyParams = new List<ParameterExpression> { jnienv, context };
			bodyParams.AddRange (ptypes);
			var body = Expression.Block (marshalBody);
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
			return typeof (JniReferenceSafeHandle);
		}

		static Expression CheckJnienv (ParameterExpression jnienv)
		{
			Action<IntPtr> a = JniEnvironment.CheckCurrent;
			return Expression.Call (null, a.Method, jnienv);
		}

		static Expression GetThis (ParameterExpression context, Type targetType)
		{
			return Expression.Call (
					GetJavaVM (),
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
}

