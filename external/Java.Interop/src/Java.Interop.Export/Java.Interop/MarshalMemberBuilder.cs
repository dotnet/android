using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using Java.Interop.Expressions;

namespace Java.Interop {

	public class MarshalMemberBuilder : JniRuntime.JniMarshalMemberBuilder
	{
		public MarshalMemberBuilder ()
		{
		}

		public MarshalMemberBuilder (JniRuntime runtime)
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

		public JniNativeMethodRegistration CreateMarshalToManagedMethodRegistration (JavaCallableAttribute export, MethodInfo method, Type? type = null)
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

			return export.Signature = GetJniMethodSignature (method);
		}

		string GetTypeSignature (ParameterInfo p)
		{
			var info        = Runtime.TypeManager.GetTypeSignature (p.ParameterType);
			if (info.IsValid)
				return info.QualifiedReference;

			var marshaler   = GetParameterMarshaler (p);
			info            = Runtime.TypeManager.GetTypeSignature (marshaler.MarshalType);
			if (info.IsValid)
				return info.QualifiedReference;

			throw new NotSupportedException ("Don't know how to determine JNI signature for parameter type: " + p.ParameterType.FullName + ".");
		}

		Delegate CreateJniMethodMarshaler (MethodInfo method, JavaCallableAttribute? export, Type? type)
		{
			var e = CreateMarshalToManagedExpression (method, export, type);
			return e.Compile ();
		}

		public LambdaExpression CreateMarshalToManagedExpression (MethodInfo method, JavaCallableAttribute? callable, Type? type = null)
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
			var vm          = Expression.Variable (typeof (JniRuntime.JniValueManager), "__vm");
			var envpVars    = new List<ParameterExpression> () {
				envp,
				jvm,
			};

			int peerableParametersCount = 0;
			for (int i = 0; i < methodParameters.Length; ++i) {
				var marshaler = GetParameterMarshaler (methodParameters [i]);

				if (typeof (IJavaPeerable).GetTypeInfo ().IsAssignableFrom (methodParameters [i].ParameterType.GetTypeInfo ()))
				    peerableParametersCount ++;
			}

			bool useVmVariable = (!method.IsStatic || peerableParametersCount > 0) && !direct;
			if (useVmVariable)
				envpVars.Add (vm);

			var envpBody    = new List<Expression> () {
				Expression.Assign (envp, CreateJniTransition (jnienv)),
			};

			var waitForGCBridge     = typeof(JniRuntime.JniValueManager)
				.GetRuntimeMethod (nameof (JniRuntime.JniValueManager.WaitForGCBridgeProcessing), new Type [0]) ??
				throw new NotSupportedException ("Could not find JniRuntime.JniValueManager.WaitForGCBridgeProcessing()");

			var marshalBody = new List<Expression> () {
				Expression.Assign (jvm, GetRuntime ()),
			};

			if (useVmVariable) {
				marshalBody.Add (Expression.Assign (vm, Expression.Property (jvm, "ValueManager")));
				marshalBody.Add (Expression.Call (vm, waitForGCBridge));
			} else
				marshalBody.Add (Expression.Call (Expression.Property (jvm, "ValueManager"), waitForGCBridge));

			Expression? self        = null;
			var marshalerContext    = new JniValueMarshalerContext (jvm, useVmVariable ? vm : null);
			if (!method.IsStatic) {
				var selfMarshaler   = Runtime.ValueManager.GetValueMarshaler (type!);
				self                = selfMarshaler.CreateParameterToManagedExpression (marshalerContext, context, 0, type);
			}

			var marshalParameters   = new List<ParameterExpression> (methodParameters.Length);
			var invokeParameters    = new List<Expression> (methodParameters.Length);
			for (int i = 0; i < methodParameters.Length; ++i) {
				var marshaler   = GetParameterMarshaler (methodParameters [i]);
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
			Expression? ret     = null;
			if (method.ReturnType == typeof (void)) {
				envpVars.AddRange (marshalerContext.LocalVariables);

				marshalBody.Add (invoke);
				envpBody.Add (
						Expression.TryCatchFinally (
							Expression.Block (marshalBody),
							CreateDisposeJniEnvironment (envp, marshalerContext.CleanupStatements),
							CreateMarshalException (envp, jvm, null)));
			} else {
				var rmarshaler  = GetParameterMarshaler (method.ReturnParameter);
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
							CreateMarshalException (envp, jvm, exit)));

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
			var marshalerType = GetMarshalerType (ret?.Type, funcTypeParams, method.DeclaringType);

			bodyParams.AddRange (marshalParameters);
			var body = Expression.Block (envpVars, envpBody);

			return marshalerType == null
				? Expression.Lambda (body, bodyParams)
				: Expression.Lambda (marshalerType, body, bodyParams);
		}

		static Type? GetMarshalerType (Type? returnType, List<Type> funcTypeParams, Type? declaringType)
		{
			// Too many parameters; does a `_JniMarshal_*` type exist in the type's declaring assembly?
			funcTypeParams.RemoveRange (0, 2);
			var marshalDelegateName = new StringBuilder ();
			marshalDelegateName.Append ("_JniMarshal_PP");
			foreach (var paramType in funcTypeParams) {
				marshalDelegateName.Append (GetJniMarshalDelegateParameterIdentifier (paramType));
			}
			marshalDelegateName.Append ("_");
			if (returnType == null) {
				marshalDelegateName.Append ("V");
			} else {
				marshalDelegateName.Append (GetJniMarshalDelegateParameterIdentifier (returnType));
			}

			Type? marshalDelegateType = declaringType?.Assembly.GetType (marshalDelegateName.ToString (), throwOnError: false);
			if (marshalDelegateType != null) {
				return marshalDelegateType;
			}

#if !NET
			// Punt?; System.Linq.Expressions will automagically produce the needed delegate type.
			// Unfortunately, this won't work with jnimarshalmethod-gen.exe.
			return marshalDelegateType;
#else   // NET
			return CreateMarshalDelegateType (marshalDelegateName.ToString (), returnType, funcTypeParams);
#endif  // NET
		}

#if NET
		static object           ab_lock         = new object ();
		static AssemblyBuilder? assemblyBuilder;
		static ModuleBuilder?   moduleBuilder;
		static Type[]?          DelegateCtorSignature;

		static Type? CreateMarshalDelegateType (string name, Type? returnType, List<Type> funcTypeParams)
		{
			lock (ab_lock) {
				if (assemblyBuilder == null) {
					var aname       = new AssemblyName ("jni-marshal-method-delegates");
					assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly (aname, AssemblyBuilderAccess.Run);
					moduleBuilder   = assemblyBuilder.DefineDynamicModule (aname.Name!);

					DelegateCtorSignature = new Type[] {
						typeof (object),
						typeof (IntPtr)
					};
				}
				funcTypeParams.Insert (0, typeof (IntPtr));
				funcTypeParams.Insert (0, typeof (IntPtr));
				var typeBuilder = moduleBuilder!.DefineType (
					name,
					TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass,
					typeof (MulticastDelegate)
				);

				const MethodAttributes      CtorAttributes      = MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public;
				const MethodImplAttributes  ImplAttributes      = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
				const MethodAttributes      InvokeAttributes    = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;

				typeBuilder.DefineConstructor (CtorAttributes, CallingConventions.Standard, DelegateCtorSignature)
					.SetImplementationFlags (ImplAttributes);
				typeBuilder.DefineMethod ("Invoke", InvokeAttributes, returnType, funcTypeParams.ToArray ())
					.SetImplementationFlags (ImplAttributes);
				return typeBuilder.CreateTypeInfo ();
			}
		}
#endif  // NET

		static char GetJniMarshalDelegateParameterIdentifier (Type type)
		{
			if (type == typeof (bool))      return 'Z';
			if (type == typeof (byte))      return 'B';
			if (type == typeof (sbyte))     return 'B';
			if (type == typeof (char))      return 'C';
			if (type == typeof (short))     return 'S';
			if (type == typeof (ushort))	return 's';
			if (type == typeof (int))       return 'I';
			if (type == typeof (uint))      return 'i';
			if (type == typeof (long))      return 'J';
			if (type == typeof (ulong))     return 'j';
			if (type == typeof (float))     return 'F';
			if (type == typeof (double))    return 'D';
			if (type == typeof (void))      return 'V';
			return 'L';
		}

		void CheckMarshalTypesMatch (MethodInfo method, string? signature, ParameterInfo[] methodParameters)
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
				var vm  = GetParameterMarshaler (methodParameters [i]);
				var jni = vm.MarshalType;
				if (mptypes [i] != jni)
					throw new ArgumentException (
							$"JNI parameter type mismatch. Type `{jni}` != `{mptypes [i]}` at index {i} in `{signature}`.",
							"signature");
			}

			if (mptypes.Count != rpcount)
				throw new ArgumentException (
						string.Format ("JNI parametr count mismatch: signature contains {0} parameters, method contains {1}.",
							mptypes.Count, methodParameters.Length),
						nameof (signature));

			var jrinfo = JniSignature.GetMarshalReturnType (signature);
			var mrvm   = GetParameterMarshaler (method.ReturnParameter);
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

		static  readonly    MethodInfo  JniRuntime_ExceptionShouldTransitionToJni   =
			typeof(JniRuntime).GetRuntimeMethod ("ExceptionShouldTransitionToJni", new[] { typeof (Exception) }) ??
			throw new NotSupportedException ("Could not find `JniRuntime.ExceptionShouldTransitionToJni()`");
		static  readonly    MethodInfo  JniTransition_SetPendingException   =
			((Action<Exception>) (new JniTransition ().SetPendingException)).Method;

		static CatchBlock CreateMarshalException  (ParameterExpression envp, ParameterExpression jvm, LabelTarget? exit)
		{
			var ex      = Expression.Variable (typeof (Exception), "__e");
			var body = new List<Expression> () {
				Expression.Call (envp, JniTransition_SetPendingException, ex),
			};
			if (exit != null) {
				body.Add (Expression.Return (exit, Expression.Default (exit.Type)));
			}
			var filter  = Expression.Call (jvm, JniRuntime_ExceptionShouldTransitionToJni, ex);
			return Expression.Catch (ex, Expression.Block (body), filter);
		}

		static readonly MethodInfo JniTransition_Dispose    = ((Action) (new JniTransition ().Dispose)).Method;

		static Expression CreateDisposeJniEnvironment (ParameterExpression envp, IList<Expression> cleanup)
		{
			var disposeTransition   = Expression.Call (envp, JniTransition_Dispose);
			return Expression.Block (
					cleanup.Reverse ().Concat (new[]{ disposeTransition }));;
		}

		static Expression GetRuntime ()
		{
			return Expression.Property (null, typeof (JniEnvironment), "Runtime");
		}

		static  MethodInfo  FormatterServices_GetUninitializedObject    =
#if NETCOREAPP
			((Func<Type, object>) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject)
#else   // !NETCOREAPP
			((Func<Type, object>) System.Runtime.Serialization.FormatterServices.GetUninitializedObject)
#endif  // NETCOREAPP
			.Method;
		static  MethodInfo  IJavaPeerable_SetPeerReference              =
			typeof (IJavaPeerable).GetRuntimeMethod ("SetPeerReference", new[]{typeof (JniObjectReference)}) ??
			throw new NotSupportedException ("Could not find IJavaPeerable.SetPeerReference()!");

		public override Expression<Func<ConstructorInfo, JniObjectReference, object?[]?, object>> CreateConstructActivationPeerExpression (ConstructorInfo constructor)
		{
			if (constructor == null)
				throw new ArgumentNullException (nameof (constructor));

			Func<object?, object?[]?, object?>  mbi = constructor.Invoke;

			var c   = Expression.Parameter (typeof (ConstructorInfo),       "constructor");
			var r   = Expression.Parameter (typeof (JniObjectReference),    "reference");
			var p   = Expression.Parameter (typeof (object[]),              "parameters");

			var t   = Expression.Variable (typeof (Type),   "type");
			var s   = Expression.Variable (typeof (object), "self");
			var b   = Expression.Block (
					new []{t, s},
					Expression.Assign (t, Expression.Property (c, "DeclaringType")),
					Expression.Assign (s, Expression.Call (FormatterServices_GetUninitializedObject, t)),
					Expression.Call (Expression.Convert (s, typeof (IJavaPeerable)), IJavaPeerable_SetPeerReference, r),
					Expression.Call (c, mbi.GetMethodInfo (), s, p),
					s);
			return Expression.Lambda<Func<ConstructorInfo, JniObjectReference, object?[]?, object>> (b, new []{c, r, p});
		}

		public static string GetMarshalMethodName (string name, string signature)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (signature == null)
				throw new ArgumentNullException (nameof (signature));

			var idx1 = signature.IndexOf ('(');
			var idx2 = signature.IndexOf (')');
			var arguments = signature;

			if (idx1 >= 0 && idx2 >= idx1)
				arguments = arguments.Substring (idx1 + 1, idx2 - idx1 - 1);

			return $"n_{name}{(string.IsNullOrEmpty (arguments) ? "" : "_")}{arguments?.Replace ('/', '_')?.Replace (';', '_')}";
		}
	}

	static class JniSignature {

		public static Type? GetMarshalReturnType (string signature)
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
			Type? t;
			while ((t = ExtractMarshalTypeFromSignature (signature, ref i)) != null)
				yield return t;
		}

		// as per: http://java.sun.com/j2se/1.5.0/docs/guide/jni/spec/types.html
		static Type? ExtractMarshalTypeFromSignature (string signature, ref int index)
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

