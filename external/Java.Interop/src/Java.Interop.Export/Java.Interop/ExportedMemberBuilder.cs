using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Java.Interop {

	public class ExportedMemberBuilder : IExportedMemberBuilder
	{
		public ExportedMemberBuilder (JniRuntime runtime)
		{
			if (runtime == null)
				throw new ArgumentNullException ("javaVM");
			Runtime = runtime;
		}

		public      JniRuntime      Runtime     {get; private set;}

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
				var exports = (JavaCallableAttribute[]) method.GetCustomAttributes (typeof(JavaCallableAttribute), inherit:false);
				if (exports == null || exports.Length == 0)
					continue;
				var export  = exports [0];
				yield return CreateMarshalFromJniMethodRegistration (export, declaringType, method);
			}
		}

		public JniNativeMethodRegistration CreateMarshalFromJniMethodRegistration (JavaCallableAttribute export, Type type, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (type == null)
				throw new ArgumentNullException ("type");
			if (method == null)
				throw new ArgumentNullException ("method");

			string signature = GetJniMethodSignature (export, method);
			return new JniNativeMethodRegistration () {
				Name        = GetJniMethodName (export, method),
				Signature   = signature,
				Marshaler   = CreateJniMethodMarshaler (export, type, method),
			};
		}

		protected virtual string GetJniMethodName (JavaCallableAttribute export, MethodInfo method)
		{
			return export.Name ?? "n_" + method.Name;
		}

		public virtual string GetJniMethodSignature (JavaCallableAttribute export, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (method == null)
				throw new ArgumentNullException ("method");

			if (export.Signature != null)
				return export.Signature;

			var signature = new StringBuilder ().Append ("(");
			foreach (var p in method.GetParameters ()) {
				var info = Runtime.TypeManager.GetTypeSignature (p.ParameterType);
				if (info.SimpleReference == null)
					throw new NotSupportedException ("Don't know how to determine JNI signature for parameter type: " + p.ParameterType.FullName + ".");
				signature.Append (info.QualifiedReference);
			}
			signature.Append (")");
			var ret = Runtime.TypeManager.GetTypeSignature (method.ReturnType);
			if (ret.SimpleReference == null)
				throw new NotSupportedException ("Don't know how to determine JNI signature for return type: " + method.ReturnType.FullName + ".");
			signature.Append (ret.QualifiedReference);
			return export.Signature = signature.ToString ();
		}

		Delegate CreateJniMethodMarshaler (JavaCallableAttribute export, Type type, MethodInfo method)
		{
			var e = CreateMarshalFromJniMethodExpression (export, type, method);
			return e.Compile ();
		}

		// TODO: make internal, and add [InternalsVisibleTo] for Java.Interop.Export-Tests
		public virtual LambdaExpression CreateMarshalFromJniMethodExpression (JavaCallableAttribute export, Type type, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (type == null)
				throw new ArgumentNullException ("type");
			if (method == null)
				throw new ArgumentNullException ("method");

			var methodParameters = method.GetParameters ();

			CheckMarshalTypesMatch (method, export.Signature, methodParameters);

			var jnienv  = Expression.Parameter (typeof (IntPtr), "__jnienv");
			var context = Expression.Parameter (typeof (IntPtr), "__context");

			var envp        = Expression.Variable (typeof (JniTransition), "__envp");
			var envpVars    = new List<ParameterExpression> () {
				envp,
			};

			var envpBody    = new List<Expression> () {
				Expression.Assign (envp, CreateJniTransition (jnienv)),
			};

			var jvm         = Expression.Variable (typeof (JniRuntime), "__jvm");
			var variables   = new List<ParameterExpression> () {
				jvm,
			};

			var marshalBody = new List<Expression> () {
				Expression.Assign (jvm, GetRuntime ()),
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
				envpBody.Add (
						Expression.TryCatchFinally (
							Expression.Block (variables, marshalBody),
							CreateDisposeJniEnvironment (envp),
							CreateMarshalException (envp, null)));
			} else {
				var jniRType    = GetMarshalToJniReturnType (method.ReturnType);
				var exit        = Expression.Label (jniRType, "__exit");
				ret             = Expression.Variable (jniRType, "__jret");
				var mret        = Expression.Variable (method.ReturnType, "__mret");
				envpVars.Add (ret);
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

				envpBody.Add (
						Expression.TryCatchFinally (
						Expression.Block (variables, marshalBody),
							CreateDisposeJniEnvironment (envp),
							CreateMarshalException (envp, exit)));

				envpBody.Add (Expression.Label (exit, Expression.Default (jniRType)));
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
			var body = Expression.Block (envpVars, envpBody);
			return Expression.Lambda (marshalerType, body, bodyParams);
		}

		void CheckMarshalTypesMatch (MethodInfo method, string signature, ParameterInfo[] methodParameters)
		{
			if (signature == null)
				return;

			var mptypes = JniSignature.GetMarshalParameterTypes (signature).ToList ();
			int len     = Math.Min (methodParameters.Length, mptypes.Count);
			for (int i = 0; i < len; ++i) {
				var jni = GetMarshalFromJniParameterType (methodParameters [i].ParameterType);
				if (mptypes [i] != jni)
					throw new ArgumentException (
							string.Format ("JNI parameter type mismatch. Type '{0}' != '{1}.", jni, mptypes [i]),
							"signature");
			}

			if (mptypes.Count != methodParameters.Length)
				throw new ArgumentException (
						string.Format ("JNI parametr count mismatch: signature contains {0} parameters, method contains {1}.",
							mptypes.Count, methodParameters.Length),
						"signature");

			var jrinfo = JniSignature.GetMarshalReturnType (signature);
			var mrinfo = GetMarshalToJniReturnType (method.ReturnType);
			if (mrinfo != jrinfo)
				throw new ArgumentException (
						string.Format ("JNI return type mismatch. Type '{0}' != '{1}.", jrinfo, mrinfo),
						"signature");
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
			if (typeof (IJavaPeerable).IsAssignableFrom (targetType))
				return Marshalers [typeof (IJavaPeerable)].FromJni (jvm, targetType, jniParameter);
			return null;
		}

		protected virtual Expression GetMarshalToJniExpression (Type sourceType, Expression managedParameter)
		{
			MarshalInfo v;
			if (Marshalers.TryGetValue (sourceType, out v))
				return v.ToJni (managedParameter);
			if (typeof (IJavaPeerable).IsAssignableFrom (sourceType))
				return Marshalers [typeof (IJavaPeerable)].ToJni (managedParameter);
			return null;
		}

		static readonly Dictionary<Type, MarshalInfo> Marshalers = new Dictionary<Type, MarshalInfo> () {
			{ typeof (string), new MarshalInfo {
					FromJni = (vm, t, p) => Expression.Call (F<IntPtr, string> (JniEnvironment.Strings.ToString).Method, p),
					ToJni   = p => Expression.Call (F<string, JniObjectReference> (JniEnvironment.Strings.NewString).Method, p)
			} },
			{ typeof (IJavaPeerable), new MarshalInfo {
					FromJni = (vm, t, p) => GetThis (vm, t, p),
					ToJni   = p => Expression.Call (F<IJavaPeerable, IntPtr> (JniEnvironment.References.NewReturnToJniRef).Method, p)
			} },
		};

		static Func<T, TRet> F<T, TRet> (Func<T, TRet> func)
		{
			return func;
		}

		static Expression CreateJniTransition (ParameterExpression jnienv)
		{
			return Expression.New (
					typeof (JniTransition).GetConstructor (new []{typeof (IntPtr)}),
					jnienv);
		}

		static CatchBlock CreateMarshalException  (ParameterExpression envp, LabelTarget exit)
		{
			var spe     = typeof (JniTransition).GetMethod ("SetPendingException");
			var ex      = Expression.Variable (typeof (Exception), "__e");
			var body = new List<Expression> () {
				Expression.Call (envp, spe, ex),
			};
			if (exit != null) {
				body.Add (Expression.Return (exit, Expression.Default (exit.Type)));
			}
			return Expression.Catch (ex, Expression.Block (body));
		}

		static Expression CreateDisposeJniEnvironment (ParameterExpression envp)
		{
			return Expression.Call (envp, typeof (JniTransition).GetMethod ("Dispose"));
		}

		static Expression GetThis (Expression vm, Type targetType, Expression context)
		{
			return Expression.Call (
					Expression.Property (vm, "ValueMarshaler"),
					"GetObject",
					new[]{targetType},
					context);
		}

		static Expression GetRuntime ()
		{
			var env     = typeof (JniEnvironment);
			var runtime = Expression.Property (null, env, "Runtime");
			return runtime;
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

	static class JniSignature {

		public static Type GetMarshalReturnType (string signature)
		{
			int idx = signature.LastIndexOf (')') + 1;
			return ExtractMarshalTypeFromSignature (signature, ref idx);
		}

		public static IEnumerable<Type> GetMarshalParameterTypes (string signature)
		{
			if (signature.StartsWith ("(", StringComparison.Ordinal)) {
				int e = signature.IndexOf (")", StringComparison.Ordinal);
				signature = signature.Substring (1, e >= 0 ? e-1 : signature.Length-1);
			}
			int i = 0;
			Type t;
			while ((t = ExtractMarshalTypeFromSignature (signature, ref i)) != null)
				yield return t;
		}

		// as per: http://java.sun.com/j2se/1.5.0/docs/guide/jni/spec/types.html
		static Type ExtractMarshalTypeFromSignature (string signature, ref int index)
		{
			#if false
			if (index >= signature.Length)
				return null;
			var i = index++;
			switch (signature [i]) {
			case 'B':   return typeof (sbyte);
			case 'C':   return typeof (char);
			case 'D':   return typeof (double);
			case 'F':   return typeof (float);
			case 'I':   return typeof (int);
			case 'J':   return typeof (long);
			case 'S':   return typeof (short);
			case 'V':   return typeof (void);
			case 'Z':   return typeof (bool);
			case '[':
			case 'L':   return typeof (IntPtr);
			default:
				throw new ArgumentException ("Unknown JNI Type '" + signature [i] + "' within: " + signature, "signature");
			}
			#else
			if (index >= signature.Length)
				return null;
			var i = index++;
			switch (signature [i]) {
			case 'B':   return typeof (sbyte);
			case 'C':   return typeof (char);
			case 'D':   return typeof (double);
			case 'F':   return typeof (float);
			case 'I':   return typeof (int);
			case 'J':   return typeof (long);
			case 'S':   return typeof (short);
			case 'V':   return typeof (void);
			case 'Z':   return typeof (bool);
			case '[':
				++i;
				if (i >= signature.Length)
					throw new ArgumentException ("Missing array type after '[' at index " + i + " in: " + signature, "signature");
				ExtractMarshalTypeFromSignature (signature, ref index);
				return typeof (IntPtr);
			case 'L': {
				var e = signature.IndexOf (";", index, StringComparison.Ordinal);
				if (e <= 0)
					throw new ArgumentException ("Missing reference type after 'L' at index " + i + "in: " + signature, "signature");
				index = e + 1;
				return typeof (IntPtr);
			}
			default:
				throw new ArgumentException ("Unknown JNI Type '" + signature [i] + "' within: " + signature, "signature");
			}
			#endif
		}
	}
}

