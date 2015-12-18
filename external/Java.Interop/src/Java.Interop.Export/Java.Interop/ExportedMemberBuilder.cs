using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Java.Interop.Expressions;

namespace Java.Interop {

	public class ExportedMemberBuilder : JniRuntime.JniExportedMemberBuilder
	{
		public ExportedMemberBuilder ()
		{
		}

		public ExportedMemberBuilder (JniRuntime runtime)
		{
			if (runtime == null)
				throw new ArgumentNullException (nameof (runtime));

			OnSetRuntime (runtime);
		}

		public override IEnumerable<JniNativeMethodRegistration> GetExportedMemberRegistrations (Type declaringType)
		{
			if (declaringType == null)
				throw new ArgumentNullException ("declaringType");
			return CreateExportedMemberRegistrationIterator (declaringType);
		}

		IEnumerable<JniNativeMethodRegistration> CreateExportedMemberRegistrationIterator (Type declaringType)
		{
			foreach (var method in declaringType.GetTypeInfo ().DeclaredMethods) {
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
				signature.Append (GetTypeSignature (p));
			}
			signature.Append (")");
			signature.Append (GetTypeSignature (method.ReturnParameter));
			return export.Signature = signature.ToString ();
		}

		string GetTypeSignature (ParameterInfo p)
		{
			var info        = Runtime.TypeManager.GetTypeSignature (p.ParameterType);
			if (info.IsValid)
				return info.QualifiedReference;

			var marshaler   = GetValueMarshaler (p);
			info            = Runtime.TypeManager.GetTypeSignature (marshaler.MarshalType);
			if (info.IsValid)
				return info.QualifiedReference;

			throw new NotSupportedException ("Don't know how to determine JNI signature for parameter type: " + p.ParameterType.FullName + ".");
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
			var context = Expression.Parameter (typeof (IntPtr), method.IsStatic ? "__class" : "__this");

			var envp        = Expression.Variable (typeof (JniTransition), "__envp");
			var jvm         = Expression.Variable (typeof (JniRuntime), "__jvm");
			var envpVars    = new List<ParameterExpression> () {
				envp,
				jvm,
			};

			var envpBody    = new List<Expression> () {
				Expression.Assign (envp, CreateJniTransition (jnienv)),
			};

			var marshalBody = new List<Expression> () {
				Expression.Assign (jvm, GetRuntime ()),
			};

			Expression self = null;
			var marshalerContext    = new JniValueMarshalerContext (jvm);
			if (!method.IsStatic) {
				var selfMarshaler   = Runtime.ValueManager.GetValueMarshaler (type);
				self                = selfMarshaler.CreateParameterToManagedExpression (marshalerContext, context, 0, type);
			}

			var marshalParameters   = new List<ParameterExpression> (methodParameters.Length);
			var invokeParameters    = new List<Expression> (methodParameters.Length);
			for (int i = 0; i < methodParameters.Length; ++i) {
				var marshaler   = GetValueMarshaler (methodParameters [i]);
				var np          = Expression.Parameter (marshaler.MarshalType, methodParameters [i].Name);
				var p           = marshaler.CreateParameterToManagedExpression (marshalerContext, np, methodParameters [i].Attributes, methodParameters [i].ParameterType);
				marshalParameters.Add (np);
				invokeParameters.Add (p);
			}

			marshalBody.AddRange (marshalerContext.CreationStatements);

			Expression invoke = method.IsStatic
				? Expression.Call (method, invokeParameters)
				: Expression.Call (self, method, invokeParameters);
			Expression ret = null;
			if (method.ReturnType == typeof (void)) {
				envpVars.AddRange (marshalerContext.LocalVariables);

				marshalBody.Add (invoke);
				envpBody.Add (
						Expression.TryCatchFinally (
							Expression.Block (marshalBody),
							CreateDisposeJniEnvironment (envp, marshalerContext.CleanupStatements),
							CreateMarshalException (envp, null)));
			} else {
				var rmarshaler  = GetValueMarshaler (method.ReturnParameter);
				var jniRType    = rmarshaler.MarshalType;
				var exit        = Expression.Label (jniRType, "__exit");
				var mret        = Expression.Variable (method.ReturnType, "__mret");
				envpVars.Add (mret);
				marshalBody.Add (Expression.Assign (mret, invoke));
				marshalerContext.CreationStatements.Clear ();
				ret = rmarshaler.CreateReturnValueFromManagedExpression (marshalerContext, mret);
				marshalBody.AddRange (marshalerContext.CreationStatements);
				marshalBody.Add (Expression.Return (exit, ret));

				envpVars.AddRange (marshalerContext.LocalVariables);

				envpBody.Add (
						Expression.TryCatchFinally (
							Expression.Block (marshalBody),
							CreateDisposeJniEnvironment (envp, marshalerContext.CleanupStatements),
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

		JniValueMarshaler GetValueMarshaler (ParameterInfo parameter)
		{
			var attr = parameter.GetCustomAttribute<JniValueMarshalerAttribute> ();
			if (attr != null) {
				return (JniValueMarshaler) Activator.CreateInstance (attr.MarshalerType);
			}
			return Runtime.ValueManager.GetValueMarshaler (parameter.ParameterType);
		}

		void CheckMarshalTypesMatch (MethodInfo method, string signature, ParameterInfo[] methodParameters)
		{
			if (signature == null)
				return;

			var mptypes = JniSignature.GetMarshalParameterTypes (signature).ToList ();
			int len     = Math.Min (methodParameters.Length, mptypes.Count);
			for (int i = 0; i < len; ++i) {
				var vm  = GetValueMarshaler (methodParameters [i]);
				var jni = vm.MarshalType;
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
			var mrvm   = GetValueMarshaler (method.ReturnParameter);
			var mrinfo = mrvm.MarshalType;
			if (mrinfo != jrinfo)
				throw new ArgumentException (
						string.Format ("JNI return type mismatch. Type '{0}' != '{1}'.", jrinfo, mrinfo),
						"signature");
		}

		static Expression CreateJniTransition (ParameterExpression jnienv)
		{
			var ctor =
				(from c in typeof(JniTransition).GetTypeInfo ().DeclaredConstructors
				 let p = c.GetParameters ()
				 where p.Length == 1 && p [0].ParameterType == typeof (IntPtr)
				 select c)
				.First ();
			return Expression.New (
					ctor,
					jnienv);
		}

		static CatchBlock CreateMarshalException  (ParameterExpression envp, LabelTarget exit)
		{
			var spe     = typeof (JniTransition).GetTypeInfo ().GetDeclaredMethod ("SetPendingException");
			var ex      = Expression.Variable (typeof (Exception), "__e");
			var body = new List<Expression> () {
				Expression.Call (envp, spe, ex),
			};
			if (exit != null) {
				body.Add (Expression.Return (exit, Expression.Default (exit.Type)));
			}
			return Expression.Catch (ex, Expression.Block (body));
		}

		static Expression CreateDisposeJniEnvironment (ParameterExpression envp, IList<Expression> cleanup)
		{
			var disposeTransition   = Expression.Call (envp, typeof(JniTransition).GetTypeInfo ().GetDeclaredMethod ("Dispose"));
			return Expression.Block (
					cleanup.Reverse ().Concat (new[]{ disposeTransition }));;
		}

		static Expression GetRuntime ()
		{
			var env     = typeof (JniEnvironment);
			var runtime = Expression.Property (null, env, "Runtime");
			return runtime;
		}
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

