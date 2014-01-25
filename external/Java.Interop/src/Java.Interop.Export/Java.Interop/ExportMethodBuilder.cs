using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Java.Interop {

	public static class ExportMethodBuilder
	{
		public static void AddExportMethods (Type type, ICollection<JniNativeMethodRegistration> methods)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			if (methods == null)
				throw new ArgumentNullException ("methods");

			const BindingFlags methodScope = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			foreach (var method in type.GetMethods (methodScope)) {
				var exports = (ExportAttribute[]) method.GetCustomAttributes (typeof(ExportAttribute), inherit:false);
				if (exports == null || exports.Length == 0)
					continue;
				var export  = exports [0];
				methods.Add (CreateNativeMethodRegistration (export, type, method));
			}
		}

		public static JniNativeMethodRegistration CreateNativeMethodRegistration (ExportAttribute export, Type type, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (type == null)
				throw new ArgumentNullException ("type");
			if (method == null)
				throw new ArgumentNullException ("method");

			return new JniNativeMethodRegistration () {
				Name        = CreateName (export, method),
				Signature   = CreateSignature (export, method),
				Marshaler   = CreateMarshaler (export, type, method),
			};
		}

		static string CreateName (ExportAttribute export, MethodInfo method)
		{
			return export.Name ?? "n_" + method.Name;
		}

		static string CreateSignature (ExportAttribute export, MethodInfo method)
		{
			if (export.Signature != null)
				return export.Signature;
			throw new NotSupportedException ("parameter deduction not yet implemented.");
		}

		static Delegate CreateMarshaler (ExportAttribute export, Type type, MethodInfo method)
		{
			var e = CreateInvocationExpression (export, type, method);
			return e.Compile ();
		}

		// TODO: make internal, and add [InternalsVisibleTo] for Java.Interop.Export-Tests
		public static LambdaExpression CreateInvocationExpression (ExportAttribute export, Type type, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (type == null)
				throw new ArgumentNullException ("type");
			if (method == null)
				throw new ArgumentNullException ("method");

			var ptypes = method.GetParameters ()
				.Select (p => Expression.Parameter (p.ParameterType, p.Name))
				.ToList ();
			var jnienv  = Expression.Parameter (typeof (IntPtr), "__jnienv");
			var context = Expression.Parameter (typeof (IntPtr), "__context");
			var invoke = (MethodCallExpression) null;
			if (method.IsStatic)
				invoke = Expression.Call (method, ptypes);
			else {
				var instance = GetThis (context, type);
				invoke = Expression.Call (instance, method, ptypes);
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
				funcTypeParams.Add (method.ReturnType);
				marshalerType = Expression.GetFuncType (funcTypeParams.ToArray ());
			}
			var bodyParams = new List<ParameterExpression> { jnienv, context };
			bodyParams.AddRange (ptypes);
			return Expression.Lambda (marshalerType, invoke, bodyParams);
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
	}
}

