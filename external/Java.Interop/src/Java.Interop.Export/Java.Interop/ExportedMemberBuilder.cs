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

		public override LambdaExpression CreateMarshalToManagedExpression (MethodInfo method)
		{
			if (method == null)
				throw new ArgumentNullException (nameof (method));

			return CreateMarshalToManagedExpression (method, null, method.DeclaringType);
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
				yield return CreateMarshalToManagedMethodRegistration (export, method, declaringType);
			}
		}

		public JniNativeMethodRegistration CreateMarshalToManagedMethodRegistration (JavaCallableAttribute export, MethodInfo method, Type type = null)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (method == null)
				throw new ArgumentNullException ("method");

			string signature = GetJniMethodSignature (export, method);
			return new JniNativeMethodRegistration () {
				Name        = GetJniMethodName (export, method),
				Signature   = signature,
				Marshaler   = CreateJniMethodMarshaler (method, export, type),
			};
		}

		string GetJniMethodName (JavaCallableAttribute export, MethodInfo method)
		{
			return export.Name ?? "n_" + method.Name;
		}

		public string GetJniMethodSignature (JavaCallableAttribute export, MethodInfo method)
		{
			if (export == null)
				throw new ArgumentNullException ("export");
			if (method == null)
				throw new ArgumentNullException ("method");

			if (export.Signature != null)
				return export.Signature;

			var signature = new StringBuilder ().Append ("(");
			var methodParameters    = method.GetParameters ();
			foreach (var p in IsDirectMethod (methodParameters) ? methodParameters.Skip (2) : methodParameters) {
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

		Delegate CreateJniMethodMarshaler (MethodInfo method, JavaCallableAttribute export, Type type)
		{
			var e = CreateMarshalToManagedExpression (method, export, type);
			return e.Compile ();
		}

		public LambdaExpression CreateMarshalToManagedExpression (MethodInfo method, JavaCallableAttribute callable, Type type = null)
		{
			if (method == null)
				throw new ArgumentNullException ("method");
			type        = type ?? method.DeclaringType;

			var methodParameters = method.GetParameters ();

			CheckMarshalTypesMatch (method, callable?.Signature, methodParameters);

			bool direct = IsDirectMethod (methodParameters);

			var jnienv  = Expression.Parameter (typeof (IntPtr), direct ? methodParameters [0].Name : "__jnienv");
			var context = Expression.Parameter (typeof (IntPtr), direct ? methodParameters [1].Name : (method.IsStatic ? "__class" : "__this"));

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
				ParameterExpression np;
				if (i > 1 || !direct)
					np = Expression.Parameter (marshaler.MarshalType, methodParameters [i].Name);
				else {
					if (i == 0)
						np = jnienv;
					else if (i == 1)
						np = context;
					else
						throw new InvalidOperationException ("Should not be reached.");
				}
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

			var funcTypeParams  = new List<Type> ();
			var bodyParams      = new List<ParameterExpression> ();
			if (!direct) {
				funcTypeParams.Add (typeof (IntPtr));
				funcTypeParams.Add (typeof (IntPtr));
				bodyParams.Add (jnienv);
				bodyParams.Add (context);
			}
			foreach (var p in marshalParameters)
				funcTypeParams.Add (p.Type);
			if (ret != null)
				funcTypeParams.Add (ret.Type);
			var marshalerType = ret == null
				? Expression.GetActionType (funcTypeParams.ToArray ())
				: Expression.GetFuncType (funcTypeParams.ToArray ());

			bodyParams.AddRange (marshalParameters);
			var body = Expression.Block (envpVars, envpBody);
			return Expression.Lambda (marshalerType, body, bodyParams);
		}

		// Heuristic: if first two parameters are IntPtr, this is a "direct" wrapper.
		static bool IsDirectMethod (ParameterInfo[] methodParameters)
		{
			return methodParameters.Length >= 2 &&
				methodParameters [0].ParameterType == typeof (IntPtr) &&
				methodParameters [1].ParameterType == typeof (IntPtr);
		}

		JniValueMarshaler GetValueMarshaler (ParameterInfo parameter)
		{
			if (parameter.ParameterType == typeof(IntPtr))
				return IntPtrValueMarshaler.Instance;

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
			int rpcount = methodParameters.Length;
			int len     = Math.Min (methodParameters.Length, mptypes.Count);
			int start   = 0;
			if (IsDirectMethod (methodParameters)) {
				start   += 2;
				rpcount -= 2;
			}
			for (int i = start; i < len; ++i) {
				var vm  = GetValueMarshaler (methodParameters [i]);
				var jni = vm.MarshalType;
				if (mptypes [i] != jni)
					throw new ArgumentException (
							string.Format ("JNI parameter type mismatch. Type '{0}' != '{1}.", jni, mptypes [i]),
							"signature");
			}

			if (mptypes.Count != rpcount)
				throw new ArgumentException (
						string.Format ("JNI parametr count mismatch: signature contains {0} parameters, method contains {1}.",
							mptypes.Count, methodParameters.Length),
						nameof (signature));

			var jrinfo = JniSignature.GetMarshalReturnType (signature);
			var mrvm   = GetValueMarshaler (method.ReturnParameter);
			var mrinfo = mrvm.MarshalType;
			if (mrinfo != jrinfo)
				throw new ArgumentException (
						string.Format ("JNI return type mismatch. Type '{0}' != '{1}'.", jrinfo, mrinfo),
						nameof (signature));
		}

		static ConstructorInfo  JniTransitionConstructor    =
			(from c in typeof (JniTransition).GetTypeInfo ().DeclaredConstructors
			 let  p = c.GetParameters ()
			 where p.Length == 1 && p [0].ParameterType == typeof (IntPtr)
			 select c)
			.First ();

		static Expression CreateJniTransition (ParameterExpression jnienv)
		{
			return Expression.New (
					JniTransitionConstructor,
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

	class IntPtrValueMarshaler : JniValueMarshaler<IntPtr> {
		internal    static  IntPtrValueMarshaler Instance = new IntPtrValueMarshaler ();

		public override Expression CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			return sourceValue;
		}

		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type targetType)
		{
			return sourceValue;
		}

		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			return sourceValue;
		}


		public override object CreateValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			throw new NotImplementedException ();
		}

		public override IntPtr CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			throw new NotImplementedException ();
		}

		public override JniValueMarshalerState CreateArgumentState (object value, ParameterAttributes synchronize)
		{
			throw new NotSupportedException ();
		}

		public override JniValueMarshalerState CreateGenericArgumentState (IntPtr value, ParameterAttributes synchronize)
		{
			throw new NotSupportedException ();
		}

		public override JniValueMarshalerState CreateObjectReferenceArgumentState (object value, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (IntPtr value, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}

		public override void DestroyArgumentState (object value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}

		public override void DestroyGenericArgumentState (IntPtr value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}
	}
}

