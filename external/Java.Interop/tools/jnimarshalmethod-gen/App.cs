using System;
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

		static ExportedMemberBuilder CreateExportedMemberBuilder ()
		{
			return new ExportedMemberBuilder (JniEnvironment.Runtime);
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
					var lambda  = builder.CreateMarshalFromJniMethodExpression (export, type, method);
					lambda.CompileToMethod (mb);
				}
				if (dt != null)
					dt.CreateType ();
			}
			da.Save (destPath);
		}
	}
}
