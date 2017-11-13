using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

using Java.Interop;

namespace Xamarin.Android.Tools.JniMarshalMethodGenerator {

	class App {

		internal const string Name = "jnimarshalmethod-gen";

		public static void Main (string[] args)
		{
			var jvm = CreateJavaVM ();

			foreach (var path in args) {
				if (!File.Exists (path)) {
					Console.Error.WriteLine ("{0}: Path '{1}' does not exist.", Name, path);
					continue;
				}
				try {
					CreateMarshalMethodAssembly (path);
				}
				catch (Exception e) {
					Console.Error.WriteLine ("{0}: {1}", Name, e.Message);
					Console.WriteLine (e);
					Environment.ExitCode    = 1;
				}
			}

			jvm.Dispose ();
		}

		static JniRuntime CreateJavaVM ()
		{
			var builder = new JreRuntimeOptions ();
			return builder.CreateJreVM ();
		}

		static JniRuntime.JniMarshalMemberBuilder CreateExportedMemberBuilder ()
		{
			return JniEnvironment.Runtime.MarshalMemberBuilder;
		}

		static void CreateMarshalMethodAssembly (string path)
		{
			var assembly        = Assembly.LoadFile (path);

			var baseName        = Path.GetFileNameWithoutExtension (path);
			var assemblyName    = new AssemblyName (baseName + "-JniMarshalMethods");
			var destPath        = assemblyName.Name + ".dll";
			var builder         = CreateExportedMemberBuilder ();

			var da = AppDomain.CurrentDomain.DefineDynamicAssembly (
					assemblyName,
					AssemblyBuilderAccess.Save,
					Path.GetDirectoryName (path));

			var dm = da.DefineDynamicModule ("<default>", destPath);

			foreach (var type in assembly.DefinedTypes) {
				var registrationElements    = new List<Expression> ();
				var targetType              = Expression.Variable (typeof(Type), "targetType");
				TypeBuilder dt = null;

				var flags = BindingFlags.Public | BindingFlags.NonPublic |
						BindingFlags.Instance | BindingFlags.Static;
				foreach (var method in type.GetMethods (flags )) {
					// TODO: Constructors, [Register] methods
					var export  = method.GetCustomAttribute<JavaCallableAttribute> ();
					if (export == null)
						continue;
					if (dt == null)
						dt = dm.DefineType (type.FullName, TypeAttributes.Public | TypeAttributes.Sealed);

					var mb = dt.DefineMethod (
							method.Name,
							MethodAttributes.Public | MethodAttributes.Static);
					var lambda  = builder.CreateMarshalToManagedExpression (method);
					lambda.CompileToMethod (mb);
					var signature = export.Signature ??
							builder.GetJniMethodSignature (method);
					registrationElements.Add (CreateRegistration (export.Name, signature, lambda, targetType, method.Name));
				}
				if (dt != null) {
					AddRegisterNativeMembers (dt, targetType, registrationElements);
					dt.CreateType ();
				}
			}
			da.Save (destPath);
		}

		static  readonly    MethodInfo          Delegate_CreateDelegate             = typeof (Delegate).GetMethod ("CreateDelegate", new[] {
			typeof (Type),
			typeof (Type),
			typeof (string),
		});
		static  readonly    ConstructorInfo     JniNativeMethodRegistration_ctor    = typeof (JniNativeMethodRegistration).GetConstructor (new[] {
			typeof (string),
			typeof (string),
			typeof (Delegate),
		});
		static  readonly    MethodInfo          JniNativeMethodRegistrationArguments_AddRegistrations = typeof (JniNativeMethodRegistrationArguments).GetMethod ("AddRegistrations", new[] {
			typeof (IEnumerable<JniNativeMethodRegistration>),
		});
		static  readonly    MethodInfo          Type_GetType                        = typeof (Type).GetMethod ("GetType", new[] {
			typeof (string),
		});

		static Expression CreateRegistration (string method, string signature, LambdaExpression lambda, ParameterExpression targetType, string methodName)
		{
			var d = Expression.Call (Delegate_CreateDelegate, Expression.Constant (lambda.Type, typeof (Type)), targetType, Expression.Constant (methodName));
			return Expression.New (JniNativeMethodRegistration_ctor,
					Expression.Constant (method),
					Expression.Constant (signature),
					d);
		}

		static void AddRegisterNativeMembers (TypeBuilder dt, ParameterExpression targetType, List<Expression> registrationElements)
		{
			var args    = Expression.Parameter (typeof (JniNativeMethodRegistrationArguments),   "args");

			var body = Expression.Block (
					new[]{targetType},
					Expression.Assign (targetType, Expression.Call (Type_GetType, Expression.Constant (dt.FullName))),
					Expression.Call (args, JniNativeMethodRegistrationArguments_AddRegistrations, Expression.NewArrayInit (typeof (JniNativeMethodRegistration), registrationElements.ToArray ())));

			var lambda  = Expression.Lambda<Action<JniNativeMethodRegistrationArguments>> (body, new[]{ args });

			var rb = dt.DefineMethod ("__RegisterNativeMembers",
					MethodAttributes.Public | MethodAttributes.Static);
			rb.SetCustomAttribute (new CustomAttributeBuilder (typeof (JniAddNativeMethodRegistrationAttribute).GetConstructor (Type.EmptyTypes), new object[0]));
			lambda.CompileToMethod (rb);
		}
	}
}
