using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

using Java.Interop;

using Mono.Cecil;
using Mono.Options;
using Mono.Collections.Generic;
using Java.Interop.Tools.Cecil;

namespace Xamarin.Android.Tools.JniMarshalMethodGenerator {

	class App : MarshalByRefObject
	{

		internal const string Name = "jnimarshalmethod-gen";
		static DirectoryAssemblyResolver resolver = new DirectoryAssemblyResolver (logger: (l, v) => { Console.WriteLine (v); }, loadDebugSymbols: true, loadReaderParameters: new ReaderParameters () { ReadSymbols = true });
		static Dictionary<string, TypeBuilder> definedTypes = new Dictionary<string, TypeBuilder> ();
		static public bool Debug;
		static public bool Verbose;
		static bool keepTemporary;
		static bool forceRegeneration;
		static List<Regex> typeNameRegexes = new List<Regex> ();
		List<string> FilesToDelete = new List<string> ();

		public static int Main (string [] args)
		{
			var domain = AppDomain.CreateDomain ("workspace");
			var app = (App)domain.CreateInstanceAndUnwrap (typeof (App).Assembly.FullName, typeof (App).FullName);

			var assemblies = app.ProcessArguments (args);
			app.ProcessAssemblies (assemblies);
			var filesToDelete = app.FilesToDelete;

			AppDomain.Unload (domain);

			foreach (var path in filesToDelete)
				File.Delete (path);

			return 0;
		}

		List<string> ProcessArguments (string [] args)
		{
			var help = false;
			var options = new OptionSet {
				$"Usage: {Name}.exe OPTIONS* ASSEMBLY+",
				"",
				"Generates helper marshaling methods for specified assemblies.",
				"",
				"Copyright 2018 Microsoft Corporation",
				"",
				"Options:",
				{ "d|debug",
				  "Inject debug messages",
				  v => Debug = true },
				{ "f",
				  "Force regeneration of marshal methods",
				  v => forceRegeneration = true },
				{ "keeptemp",
				  "Keep temporary *-JniMarshalMethod.dll files.",
				  v => keepTemporary = true },
				{ "L=",
				  "{DIRECTORY} to resolve assemblies from.",
				  v => resolver.SearchDirectories.Add (v) },
				{ "h|help|?",
				  "Show this message and exit",
				  v => help = v != null },
				{ "t|type=",
				  "Generate marshaling methods only for types whose names match {TYPE-REGEX}.",
				  v => typeNameRegexes.Add (new Regex (v)) },
				{ "v|verbose",
				  "Output information about progress during the run of the tool",
				  v => Verbose = true },
			};

			var assemblies = options.Parse (args);
			if (help || args.Length < 1) {
				options.WriteOptionDescriptions (Console.Out);

				Environment.Exit (0);
			}

			if (assemblies.Count < 1) {
				Error ("Please specify at least one ASSEMBLY to process.");
				Environment.Exit (2);
			}

			return assemblies;
		}

		void ProcessAssemblies (List<string> assemblies)
		{
			CreateJavaVM ();

			var readWriteParameters    = new ReaderParameters {
				AssemblyResolver   = resolver,
				ReadSymbols        = true,
				ReadWrite          = true,
			};
			var readWriteParametersNoSymbols    = new ReaderParameters {
				AssemblyResolver   = resolver,
				ReadSymbols        = false,
				ReadWrite          = true,
			};

			foreach (var assembly in assemblies) {
				if (!File.Exists (assembly)) {
					Error ($"Path '{assembly}' does not exist.");
					Environment.Exit (1);
				}

				resolver.SearchDirectories.Add (Path.GetDirectoryName (assembly));
				AssemblyDefinition ad;
				try {
					ad = AssemblyDefinition.ReadAssembly (assembly, readWriteParameters);
					resolver.AddToCache (ad);
				} catch (Exception) {
					Warning ($"Unable to read assembly '{assembly}' with symbols. Retrying to load it without them.");
					ad = AssemblyDefinition.ReadAssembly (assembly, readWriteParametersNoSymbols);
					resolver.AddToCache (ad);
				}
			}

			foreach (var assembly in assemblies) {
				try {
					CreateMarshalMethodAssembly (assembly);
				} catch (Exception e) {
					Error ($"Unable to process assembly '{assembly}'\n{e.Message}\n{e}");
					Environment.Exit (1);
				}
			}
		}

		void CreateJavaVM ()
		{
			var builder = new JreRuntimeOptions ();
			builder.CreateJreVM ();
		}

		static JniRuntime.JniMarshalMemberBuilder CreateExportedMemberBuilder ()
		{
			return JniEnvironment.Runtime.MarshalMemberBuilder;
		}

		static TypeBuilder GetTypeBuilder (ModuleBuilder mb, Type type)
		{
			if (definedTypes.ContainsKey (type.FullName))
				return definedTypes [type.FullName];

			if (type.IsNested) {
				var outer = GetTypeBuilder (mb, type.DeclaringType);
				var nested = outer.DefineNestedType (type.Name, System.Reflection.TypeAttributes.NestedPublic);
				definedTypes [type.FullName] = nested;
				return nested;
			}

			var tb = mb.DefineType (type.FullName, System.Reflection.TypeAttributes.Public);
			definedTypes [type.FullName] = tb;

			return tb;
		}

		void CreateMarshalMethodAssembly (string path)
		{
			var assembly        = Assembly.LoadFile (path);

			var baseName        = Path.GetFileNameWithoutExtension (path);
			var assemblyName    = new AssemblyName (baseName + "-JniMarshalMethods");
			var destPath        = assemblyName.Name + ".dll";
			var builder         = CreateExportedMemberBuilder ();
			var matchType       = typeNameRegexes.Count > 0;

			if (Verbose)
				ColorWriteLine ($"Preparing marshal method assembly '{assemblyName}'", ConsoleColor.Cyan);

			var da = AppDomain.CurrentDomain.DefineDynamicAssembly (
					assemblyName,
					AssemblyBuilderAccess.Save,
					Path.GetDirectoryName (path));

			var dm = da.DefineDynamicModule ("<default>", destPath);

			var ad = resolver.GetAssembly (path);

			foreach (var type in assembly.DefinedTypes) {
				if (matchType) {
					var matched = false;

					foreach (var r in typeNameRegexes)
						matched |= r.IsMatch (type.FullName);

					if (!matched)
						continue;
				}

				if (type.IsGenericType || type.IsGenericTypeDefinition)
					continue;

				var td                      = ad.MainModule.FindType (type);

				if (td == null) {
					if (Verbose)
						Warning ($"Unable to find cecil's TypeDefinition of type {type}");
					continue;
				}
				if (!td.ImplementsInterface ("Java.Interop.IJavaPeerable"))
					continue;

				var existingMarshalMethodsType = td.GetNestedType (TypeMover.NestedName);
				if (existingMarshalMethodsType != null && !forceRegeneration) {
					Warning ($"Marshal methods type '{existingMarshalMethodsType.GetAssemblyQualifiedName ()}' already exists. Skipped generation of marshal methods. Use -f to force regeneration when desired.");

					continue;
				}

				var registrationElements    = new List<Expression> ();
				var targetType              = Expression.Variable (typeof(Type), "targetType");
				TypeBuilder dt = null;

				var flags = BindingFlags.Public | BindingFlags.NonPublic |
						BindingFlags.Instance | BindingFlags.Static;
				foreach (var method in type.GetMethods (flags)) {
					// TODO: Constructors
					var export  = method.GetCustomAttribute<JavaCallableAttribute> ();
					string signature = null;
					string name = null;
					string methodName = method.Name;

					if (export == null) {
						if (method.IsGenericMethod || method.ContainsGenericParameters || method.IsGenericMethodDefinition || method.ReturnType.IsGenericType)
							continue;

						if (method.DeclaringType != type)
							continue;

						var md = td.GetMethodDefinition (method);

						if (md == null) {
							if (Verbose)
								Warning ($"Unable to find cecil's MethodDefinition of method {method}");
							continue;
						}

						if (!md.NeedsMarshalMethod (resolver, method, ref name, ref methodName, ref signature))
							continue;
					}

					if (dt == null)
						dt = GetTypeBuilder (dm, type);

					if (Verbose) {
						Console.Write ("Adding marshal method for ");
						ColorWriteLine ($"{method}", ConsoleColor.Green );
					}

					var mb = dt.DefineMethod (
							methodName,
							System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static);

					var lambda  = builder.CreateMarshalToManagedExpression (method);
					lambda.CompileToMethod (mb);

					if (export != null) {
						name = export.Name;
						signature = export.Signature;
					}

					if (signature == null)
						signature = builder.GetJniMethodSignature (method);

					registrationElements.Add (CreateRegistration (name, signature, lambda, targetType, methodName));
				}
				if (dt != null)
					AddRegisterNativeMembers (dt, targetType, registrationElements);
			}

			foreach (var tb in definedTypes)
				tb.Value.CreateType ();

			da.Save (destPath);

			if (Verbose)
				ColorWriteLine ($"Marshal method assembly '{assemblyName}' created", ConsoleColor.Cyan);

			var dstAssembly = resolver.GetAssembly (destPath);
			var mover = new TypeMover (dstAssembly, ad, definedTypes, resolver);
			mover.Move ();

			if (!keepTemporary)
				FilesToDelete.Add (dstAssembly.MainModule.FileName);

			definedTypes.Clear ();
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
					System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static);
			rb.SetCustomAttribute (new CustomAttributeBuilder (typeof (JniAddNativeMethodRegistrationAttribute).GetConstructor (Type.EmptyTypes), new object[0]));
			lambda.CompileToMethod (rb);
		}

		static void ColorMessage (string message, ConsoleColor color, TextWriter writer, bool writeLine = true)
		{
			Console.ForegroundColor = color;
			if (writeLine)
				writer.WriteLine (message);
			else
				writer.Write (message);
			Console.ResetColor ();
		}

		public static void ColorWriteLine (string message, ConsoleColor color) => ColorMessage (message, color, Console.Out);

		public static void ColorWrite (string message, ConsoleColor color) => ColorMessage (message, color, Console.Out, false);

		public static void Error (string message) => ColorMessage ($"Error: {Name}: {message}", ConsoleColor.Red, Console.Error);

		public static void Warning (string message) => ColorMessage ($"Warning: {Name}: {message}", ConsoleColor.Yellow, Console.Error);
	}

	static class Extensions
	{
		public static string GetCecilName (this Type type)
		{
			return type.FullName.Replace ('+', '/');
		}

		static bool CompareTypes (Type reflectionType, TypeReference cecilType)
		{
			return cecilType.ToString () == reflectionType.GetCecilName ();
		}

		static bool MethodsAreEqual (MethodInfo methodInfo, MethodDefinition methodDefinition)
		{
			if (methodInfo.Name != methodDefinition.Name)
				return false;

			if (!CompareTypes (methodInfo.ReturnType, methodDefinition.ReturnType))
				return false;


			var parameters = methodInfo.GetParameters ();
			int infoParametersCount = parameters?.Length ?? 0;
			if (!methodDefinition.HasParameters && infoParametersCount == 0)
				return true;

			if (infoParametersCount != (methodDefinition.Parameters?.Count ?? 0))
				return false;


			int i = 0;
			foreach (var parameter in methodDefinition.Parameters) {
				if (!CompareTypes (parameters [i].ParameterType, parameter.ParameterType))
					return false;
				i++;
			}

			return true;
		}

		public static MethodDefinition GetMethodDefinition (this TypeDefinition td, MethodInfo method)
		{
			foreach (var m in td.Methods)
				if (MethodsAreEqual (method, m))
					return m;

			return null;
		}

		static bool CheckMethod (MethodDefinition m, ref string name, ref string methodName, ref string signature)
		{
			foreach (var registerAttribute in m.GetCustomAttributes ("Android.Runtime.RegisterAttribute")) {
				if (registerAttribute == null || !registerAttribute.HasConstructorArguments)
					continue;

				var constructorParameters = registerAttribute.Constructor.Parameters.ToArray ();
				var constructorArguments = registerAttribute.ConstructorArguments.ToArray ();

				for (int i = 0; i < constructorArguments.Length; i++) {
					switch (constructorParameters [i].Name) {
					case "name":
						name = constructorArguments [i].Value.ToString ();
						break;
					case "signature":
						signature = constructorArguments [i].Value.ToString ();
						break;
					}

				}

				if (string.IsNullOrEmpty (name) || string.IsNullOrEmpty (signature))
					continue;

				methodName = MarshalMemberBuilder.GetMarshalMethodName (name, signature);
				name = $"n_{name}";

				return true;
			}

			return false;
		}

		public static bool NeedsMarshalMethod (this MethodDefinition md, DirectoryAssemblyResolver resolver, MethodInfo method, ref string name, ref string methodName, ref string signature)
		{
			var m = md;

			while (m != null) {
				if (CheckMethod (m, ref name, ref methodName, ref signature))
					return true;

				m = m.GetBaseDefinition ();

				if (m == md)
					break;

				md = m;
			}

			foreach (var iface in method.DeclaringType.GetInterfaces ()) {
				if (iface.IsGenericType)
					continue;

				var ifaceMap = method.DeclaringType.GetInterfaceMap (iface);
				var ad = resolver.GetAssembly (iface.Assembly.Location);
				var id = ad.MainModule.GetType (iface.GetCecilName ());

				if (id == null) {
					App.Warning ($"Couln't find iterface {iface.FullName}");
					continue;
				}

				for (int i = 0; i < ifaceMap.TargetMethods.Length; i++)
					if (ifaceMap.TargetMethods [i] == method) {
						var imd = id.GetMethodDefinition (ifaceMap.InterfaceMethods [i]);

						if (CheckMethod (imd, ref name, ref methodName, ref signature))
							return true;
					}
			}

			return false;
		}

		static TypeDefinition FindType (Collection<TypeDefinition> types, string name)
		{
			foreach (var type in types) {
				if (type.FullName == name)
					return type;

				if (name.StartsWith (type.FullName + '/', StringComparison.InvariantCulture))
					return FindType (type.NestedTypes, name);
			}

			return null;
		}

		public static TypeDefinition FindType (this ModuleDefinition md, Type type)
		{
			return FindType (md.Types, type.GetCecilName ());
		}
	}
}
