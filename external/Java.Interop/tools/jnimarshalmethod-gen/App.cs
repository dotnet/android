using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Java.Interop;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Options;
using Mono.Collections.Generic;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Expressions;

#if _DUMP_REGISTER_NATIVE_MEMBERS
using Mono.Linq.Expressions;
#endif  // _DUMP_REGISTER_NATIVE_MEMBERS

namespace Xamarin.Android.Tools.JniMarshalMethodGenerator {

	class App : MarshalByRefObject
	{

		internal const string Name = "jnimarshalmethod-gen";
		static DirectoryAssemblyResolver resolver;
		static readonly TypeDefinitionCache cache = new TypeDefinitionCache ();
		static Dictionary<string, TypeDefinition> typeMap = new Dictionary<string, TypeDefinition> ();
		static List<string> references = new List<string> ();
		static public bool Debug;
		static public bool Verbose => Verbosity > 0;
		static public int Verbosity;
		static bool keepTemporary;
		static bool forceRegeneration;
		static List<Regex> typeNameRegexes = new List<Regex> ();
		static string jvmDllPath;
		List<string> FilesToDelete = new List<string> ();
		// AssemblyLoadContext loadContext;
		static string outDirectory;

		static readonly string AppName;

		static App()
		{
			AppName = Path.GetFileNameWithoutExtension (Environment.GetCommandLineArgs () [0]);
			var r = new ReaderParameters {
				ReadSymbols                 = true,
				InMemory                    = true,
			};
			resolver = new DirectoryAssemblyResolver (
					logger:                 Log,
					loadDebugSymbols:       true,
					loadReaderParameters:   r
			);
		}

		public static int Main (string [] args)
		{
			var app = new App ();
			app.AddMonoPathToResolverSearchDirectories ();

			var assemblies = app.ProcessArguments (args);
			app.ProcessAssemblies (assemblies);
			var filesToDelete = app.FilesToDelete;

			foreach (var path in filesToDelete)
				File.Delete (path);

			return 0;
		}

		static void Log (TraceLevel level, string message)
		{
			switch (level) {
			case TraceLevel.Error:
				ColorMessage ($"{AppName}: error: ", ConsoleColor.Red, Console.Error, writeLine: false);
				ColorMessage (message, ConsoleColor.Red, Console.Error);
				break;
			case TraceLevel.Warning:
				ColorMessage ($"{AppName}: warning: ", ConsoleColor.Yellow, Console.Error, writeLine: false);
				ColorMessage (message, ConsoleColor.Yellow, Console.Error);
				break;
			case TraceLevel.Info:
				if (Verbose)
					ColorMessage (message, ConsoleColor.Cyan, Console.Out);
				break;
			case TraceLevel.Verbose:
				if (Verbosity > 1) {
					Console.WriteLine (message);
				}
				break;
			default:
				if (level == 0 || ((int) level) > Verbosity) {
					Console.WriteLine (message);
				}
				break;
			}
		}

		void AddMonoPathToResolverSearchDirectories ()
		{
			var monoPath = Environment.GetEnvironmentVariable ("MONO_PATH");
			if (string.IsNullOrWhiteSpace (monoPath))
				return;

			foreach (var path in monoPath.Split (new char [] { Path.PathSeparator })) {
				resolver.SearchDirectories.Add (path);

				var facadesDirectory = Path.Combine (path, "Facades");
				if (Directory.Exists (facadesDirectory))
					resolver.SearchDirectories.Add (facadesDirectory);
			}
		}

		List<string> ProcessArguments (string [] args)
		{
			var help = false;
			var options = new OptionSet {
				$"Usage: {Name}.exe OPTIONS* ASSEMBLY+ [@RESPONSE-FILES]",
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
				{ "jvm=",
				  "{JVM} shared library path.",
				  v => jvmDllPath = v },
				{ "keeptemp",
				  "Keep temporary *-JniMarshalMethod.dll files.",
				  v => keepTemporary = true },
				{ "L=",
				  "{DIRECTORY} to resolve assemblies from.",
				  v => resolver.SearchDirectories.Add (v) },
				{ "h|help|?",
				  "Show this message and exit",
				  v => help = v != null },
				{ "o=",
				  "{DIRECTORY} to write updated assemblies",
				  v => outDirectory = v },
				{ "r|reference=",
				  "Reference {ASSEMBLY} to use. Can be used multiple times.",
				  v => references.Add (v)
				},
				{ "types=",
				  "Generate marshaling methods only for types whose names match regex patterns listed {FILE}.\n" +
				  "One regex pattern per line.\n" +
				  "Empty lines and lines starting with '#' character are ignored as comments.",
				  v => LoadTypes (v) },
				{ "t|type=",
				  "Generate marshaling methods only for types whose names match {TYPE-REGEX}.",
				  v => typeNameRegexes.Add (new Regex (v)) },
				{ "v|verbose:",
				  "Output information about progress during the run of the tool",
				  (int? v) => Verbosity = v.HasValue ? v.Value : Verbosity + 1 },
				new ResponseFileSource(),
			};

			var assemblies = options.Parse (args);
			if (help || args.Length < 1) {
				options.WriteOptionDescriptions (Console.Out);

				Environment.Exit (0);
			}

			if (assemblies.Count < 1)
				ErrorAndExit (Message.ErrorAtLeastOneAssembly);

			return assemblies;
		}

		void LoadTypes (string typesPath)
		{
			try {
				foreach (var line in File.ReadLines (typesPath)) {
					if (string.IsNullOrWhiteSpace (line))
						continue;

					if (line [0] == '#')
						continue;

					typeNameRegexes.Add (new Regex (line));
				}
			} catch (Exception e) {
				ErrorAndExit (Message.ErrorUnableToReadProfile, typesPath, Environment.NewLine, e);
			}
		}

		void ProcessAssemblies (List<string> assemblies)
		{
			CreateJavaVM (jvmDllPath);

			var readerParameters    = new ReaderParameters {
				AssemblyResolver   = resolver,
				InMemory           = true,
				ReadSymbols        = true,
				ReadWrite          = false,
			};
			var readerParametersNoSymbols = new ReaderParameters {
				AssemblyResolver   = resolver,
				InMemory           = true,
				ReadSymbols        = false,
				ReadWrite          = false,
			};

			foreach (var r in references) {
				resolver.SearchDirectories.Add (Path.GetDirectoryName (r));
			}
			foreach (var assembly in assemblies) {
				resolver.SearchDirectories.Add (Path.GetDirectoryName (assembly));
			}
			var corlibDir   = Path.GetDirectoryName (typeof (object).Assembly.Location);
			if (corlibDir != null) {
				resolver.SearchDirectories.Add (corlibDir);
			}

			// loadContext = CreateLoadContext ();
			AppDomain.CurrentDomain.AssemblyResolve += (o, e) => {
				Log (TraceLevel.Verbose, $"# jonp: resolving assembly: {e.Name}");
				foreach (var d in resolver.SearchDirectories) {
					var a = Path.Combine (d, e.Name);
					var f = a + ".dll";
					if (File.Exists (f)) {
						return Assembly.LoadFile (Path.GetFullPath (f));
					}
					f = a + ".exe";
					if (File.Exists (f)) {
						return Assembly.LoadFile (Path.GetFullPath (f));
					}
				}
				return null;
			};

			foreach (var r in references) {
				try {
					// loadContext.LoadFromAssemblyPath (Path.GetFullPath (r));
					Assembly.LoadFile (Path.GetFullPath (r));
				} catch (Exception e) {
					Console.WriteLine (e);
					ErrorAndExit (Message.ErrorUnableToPreloadReference, r);
				}
			}

			foreach (var assembly in assemblies) {
				if (!File.Exists (assembly)) {
					ErrorAndExit (Message.ErrorPathDoesNotExist, assembly);
				}
				bool inPlaceUpdate      = string.IsNullOrEmpty (outDirectory) ||
					string.Equals (Path.GetFullPath (outDirectory), Path.GetDirectoryName (Path.GetFullPath (assembly)), StringComparison.OrdinalIgnoreCase);

				readerParameters.ReadWrite  = readerParametersNoSymbols.ReadWrite = inPlaceUpdate;

				AssemblyDefinition ad;
				try {
					if (inPlaceUpdate) {
						File.Copy (assembly, assembly + ".orig");
					}
					ad = AssemblyDefinition.ReadAssembly (assembly, readerParameters);
					resolver.AddToCache (ad);
				} catch (Exception) {
					if (Verbose)
						Information ($"Unable to read assembly '{assembly}' with symbols. Retrying to load it without them.");

					ad = AssemblyDefinition.ReadAssembly (assembly, readerParametersNoSymbols);
					resolver.AddToCache (ad);
				}

				Extensions.MethodMap.Clear ();
			}

			foreach (var assembly in assemblies) {
				try {
					CreateMarshalMethodAssembly (assembly);
				} catch (Exception e) {
					ErrorAndExit (Message.ErrorUnableToProcessAssembly, assembly, Environment.NewLine, e.Message, e);
				}
			}
		}

		void CreateJavaVM (string jvmDllPath)
		{
			if (string.IsNullOrEmpty (jvmDllPath)) {
				jvmDllPath  = ReadJavaSdkDirectoryFromJdkInfoProps ();
			}
			var builder = new JreRuntimeOptions {
				JvmLibraryPath  = jvmDllPath,
			};

			try {
				builder.CreateJreVM ();
			} catch (Exception e) {
				ErrorAndExit (Message.ErrorUnableToCreateJavaVM, Environment.NewLine, e);
			}
		}

		static string ReadJavaSdkDirectoryFromJdkInfoProps ()
		{
			var location    = typeof (App).Assembly.Location;	// …/bin/Debug-net7.0/jnimarshalmethod-gen.dll
			var binDir      = Path.GetDirectoryName (Path.GetDirectoryName (location)) ?? Environment.CurrentDirectory;
			var dirName     = Path.GetFileName (Path.GetDirectoryName (location));
			if (binDir == null || dirName == null) {
				return null;
			}
			if (!dirName.StartsWith ("Debug", StringComparison.OrdinalIgnoreCase) &&
					!dirName.StartsWith ("Release", StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			var buildName   = "Build" + dirName;
			if (buildName.Contains ('-')) {
				buildName = buildName.Substring (0, buildName.IndexOf ('-'));
			}
			var jdkPropFile = Path.Combine (binDir, buildName, "JdkInfo.props");
			if (!File.Exists (jdkPropFile)) {
				return null;
			}

			var msbuild = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");

			var jdkProps = XDocument.Load (jdkPropFile);
			var jdkJvmPath = jdkProps.Elements ()
				.Elements (msbuild + "Choose")
				.Elements (msbuild + "When")
				.Elements (msbuild + "PropertyGroup")
				.Elements (msbuild + "JdkJvmPath")
				.FirstOrDefault ();
			if (jdkJvmPath == null) {
				return null;
			}
			return jdkJvmPath.Value;
		}

		AssemblyLoadContext CreateLoadContext ()
		{
			var c = new AssemblyLoadContext ("jnimarshalmethod-gen", isCollectible: true);
			c.Resolving += (context, name) => {
				Log (TraceLevel.Verbose, $"# jonp: trying to load assembly: {name}");
				if (name.Name == "Java.Interop") {
					return typeof (IJavaPeerable).Assembly;
				}
				if (name.Name == "Java.Interop.Export") {
					return typeof (JavaCallableAttribute).Assembly;
				}
				foreach (var d in resolver.SearchDirectories) {
					var a = Path.Combine (d, name.Name);
					var f = a + ".dll";
					if (File.Exists (f)) {
						return context.LoadFromAssemblyPath (Path.GetFullPath (f));
					}
					f = a + ".exe";
					if (File.Exists (f)) {
						return context.LoadFromAssemblyPath (Path.GetFullPath (f));
					}
				}
				return null;
			};
			return c;
		}

		static JniRuntime.JniMarshalMemberBuilder CreateExportedMemberBuilder ()
		{
			return JniEnvironment.Runtime.MarshalMemberBuilder;
		}

		class MethodsComparer : IComparer<MethodInfo>
		{
			readonly Type type;
			readonly TypeDefinition td;

			public MethodsComparer (Type type, TypeDefinition td)
			{
				this.type = type;
				this.td = td;
			}

			public int Compare (MethodInfo a, MethodInfo b)
			{
				if (a.DeclaringType != type)
					return 1;

				var atd = td.GetMethodDefinition (a);
				if (atd == null)
					return 1;

				if (b.DeclaringType != type)
					return -1;

				var btd = td.GetMethodDefinition (b);
				if (btd == null)
					return -1;

				if (atd.HasOverrides ^ btd.HasOverrides)
					return btd.HasOverrides ? -1 : 1;

				return string.Compare (a.Name, b.Name, StringComparison.Ordinal);
			}
		}

		static HashSet<string> addedMethods = new HashSet<string> ();

		void CreateMarshalMethodAssembly (string path)
		{
			var baseName        = Path.GetFileNameWithoutExtension (path);
			var assemblyName    = new AssemblyName (baseName + "-JniMarshalMethods");
			var fileName        = assemblyName.Name + ".dll";
			var destDir         = string.IsNullOrEmpty (outDirectory) ? Path.GetDirectoryName (path) : outDirectory;
			var builder         = CreateExportedMemberBuilder ();
			var matchType       = typeNameRegexes.Count > 0;

			if (Verbose)
				ColorWriteLine ($"Preparing marshal method assembly '{assemblyName}'", ConsoleColor.Cyan);

			var ad = resolver.GetAssembly (path);

			var assemblyBuilder = new ExpressionAssemblyBuilder (ad, Log) {
				KeepTemporaryFiles  = keepTemporary,
			};

			PrepareTypeMap (ad.MainModule);

//			var assembly        = loadContext.LoadFromStream (File.OpenRead (path));
			var assemblyBytes   = File.ReadAllBytes (path);
			var assembly        = Assembly.Load (assemblyBytes);

			Type[] types = null;
			try {
				types = assembly.GetTypes ();
			} catch (ReflectionTypeLoadException e) {
				types = e.Types;
				foreach (var le in e.LoaderExceptions)
					Warning (Message.WarningTypeLoadException, Environment.NewLine, le);
				if (Verbose) {
					ColorMessage ($"Exception: {e.ToString ()}", ConsoleColor.Red, Console.Error);
				}
			}

			foreach (var systemType in types) {
				if (systemType == null)
					continue;

				var type = systemType.GetTypeInfo ();

				if (matchType) {
					var matched = false;

					foreach (var r in typeNameRegexes)
						matched |= r.IsMatch (type.FullName);

					if (!matched)
						continue;
				}

				if (type.IsInterface || type.IsGenericType || type.IsGenericTypeDefinition)
					continue;

				var td = FindType (type);

				if (td == null) {
					if (Verbose)
						Warning (Message.WarningUnableToFindTypeDefinition, type);

					continue;
				}
				if (!td.ImplementsInterface ("Java.Interop.IJavaPeerable", cache))
					continue;

				var existingMarshalMethodsType = td.GetNestedType (TypeMover.NestedName);
				if (existingMarshalMethodsType != null && !forceRegeneration) {
					Warning (Message.WarningMarshalMethodsTypeAlreadyExists, existingMarshalMethodsType.GetAssemblyQualifiedName (cache), assemblyName);

					return;
				}

				if (Verbose)
					ColorWriteLine ($"Processing {type} type", ConsoleColor.Yellow);

				var registrations           = new List<ExpressionMethodRegistration> ();
				var targetType              = Expression.Variable (typeof(Type), "targetType");

				var flags = BindingFlags.Public | BindingFlags.NonPublic |
						BindingFlags.Instance | BindingFlags.Static;

				var methods = type.GetMethods (flags);
				Array.Sort (methods, new MethodsComparer (type, td));

				addedMethods.Clear ();
				var mmTypeDef = new TypeDefinition (
						@namespace: null,
						name:       TypeMover.NestedName,
						attributes: Mono.Cecil.TypeAttributes.NestedPrivate
				);
				mmTypeDef.BaseType = assemblyBuilder.DeclaringAssemblyDefinition.MainModule.TypeSystem.Object;

				foreach (var method in methods) {
					// TODO: Constructors
					var export  = method.GetCustomAttribute<JavaCallableAttribute> ();
					var exportObj = method.GetCustomAttributes (inherit:false).SingleOrDefault (a => a.GetType ().Name == "JavaCallableAttribute");
					string signature = null;
					string name = null;
					string methodName = method.Name;

					if (exportObj != null) {
						dynamic e = exportObj;
						name = e.Name;
						signature = e.Signature;
					}
					else {
						if (method.IsGenericMethod || method.ContainsGenericParameters || method.IsGenericMethodDefinition || method.ReturnType.IsGenericType)
							continue;

						if (method.DeclaringType != type)
							continue;

						var md = td.GetMethodDefinition (method);

						if (md == null) {
							if (Verbose)
								Warning (Message.WarningUnableToFindMethodDefinition, method);

							continue;
						}

						if (!md.NeedsMarshalMethod (resolver, cache, method, ref name, ref methodName, ref signature))
							continue;
					}

					if (addedMethods.Contains (methodName)) {
						Log (TraceLevel.Verbose, $"# jonp: method `{methodName}` already added (?!)");
						continue;
					}

					if (Verbose) {
						Console.Write ("Adding marshal method for ");
						ColorWriteLine ($"{method}", ConsoleColor.Green );
					}

					var lambda  = builder.CreateMarshalToManagedExpression (method);
#if _DUMP_REGISTER_NATIVE_MEMBERS
					Log (TraceLevel.Verbose, $"## Dumping contents of marshal method for `{td.FullName}::{method.Name}({string.Join (", ", method.GetParameters ().Select (p => p.ParameterType))})`:");
					Console.WriteLine (lambda.ToCSharpCode ());
#endif  // _DUMP_REGISTER_NATIVE_MEMBERS
					var mmDef = assemblyBuilder.Compile (lambda);
					mmDef.Name = export?.Name ?? ("n_TODO" + lambda.GetHashCode ());
					mmTypeDef.Methods.Add (mmDef);

					if (export != null) {
						name = export.Name;
						signature = export.Signature;
					}

					if (signature == null) {
						signature = builder.GetJniMethodSignature (method);
					}

					registrations.Add (new ExpressionMethodRegistration (name, signature, mmDef));

					addedMethods.Add (methodName);
				}
				if (registrations.Count > 0) {
					var m = assemblyBuilder.CreateRegistrationMethod (registrations);
					mmTypeDef.Methods.Add (m);
					td.NestedTypes.Add (mmTypeDef);
				}
			}

			if (Verbose)
				ColorWriteLine ($"Marshal method assembly '{assemblyName}' created", ConsoleColor.Cyan);

			resolver.SearchDirectories.Add (destDir);
			// var dstAssembly = resolver.GetAssembly (fileName);

			if (!string.IsNullOrEmpty (outDirectory)) {
				Directory.CreateDirectory (outDirectory);
				path = Path.Combine (outDirectory, Path.GetFileName (path));
			}

			assemblyBuilder.Write (path);

			// var mover = new TypeMover (dstAssembly, ad, path, definedTypes, resolver, cache);
			// mover.Move ();

			// if (!keepTemporary)
			// 	FilesToDelete.Add (dstAssembly.MainModule.FileName);
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
			Expression registrationDelegateType = null;
			if (lambda.Type.Assembly == typeof (object).Assembly ||
					lambda.Type.Assembly == typeof (System.Linq.Enumerable).Assembly) {
				registrationDelegateType = Expression.Constant (lambda.Type, typeof (Type));
			}
			else {
				Func<string, bool, Type> getType = Type.GetType;
				registrationDelegateType = Expression.Call (getType.GetMethodInfo (),
						Expression.Constant (lambda.Type.FullName, typeof (string)),
						Expression.Constant (true, typeof (bool)));
				registrationDelegateType = Expression.Convert (registrationDelegateType, typeof (Type));
			}

			var d = Expression.Call (Delegate_CreateDelegate, registrationDelegateType, targetType, Expression.Constant (methodName));
			return Expression.New (JniNativeMethodRegistration_ctor,
					Expression.Constant (method),
					Expression.Constant (signature),
					d);
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

		public static void ErrorAndExit (Message message, params object[] args) {
			ColorMessage ($"error JM{message.Code:X04}: {Name}: {string.Format (message.Localized, args)}", ConsoleColor.Red, Console.Error);
			Environment.Exit (message.Code - 0x4000);
		}

		public static void Warning (Message message, params object[] args) => ColorMessage ($"warning JM{message.Code:X04}: {Name}: {string.Format (message.Localized, args)}", ConsoleColor.Yellow, Console.Error);

		public static void Information (string message) => ColorMessage (message, ConsoleColor.Yellow, Console.Out);

		static void AddToTypeMap (TypeDefinition type)
		{
			typeMap [type.FullName] = type;

			if (!type.HasNestedTypes)
				return;

			foreach (var nested in type.NestedTypes)
				AddToTypeMap (nested);
		}

		static void PrepareTypeMap (ModuleDefinition md)
		{
			typeMap.Clear ();

			foreach (var type in md.Types)
				AddToTypeMap (type);
		}

		static TypeDefinition FindType (Type type)
		{
			TypeDefinition rv;
			string cecilName = type.GetCecilName ();

			typeMap.TryGetValue (cecilName, out rv);

			return rv;
		}
	}

	internal static class Extensions
	{
		public static string GetCecilName (this Type type)
		{
			return type.FullName?.Replace ('+', '/');
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

		internal static Dictionary<MethodInfo, MethodDefinition> MethodMap = new Dictionary<MethodInfo, MethodDefinition> ();

		public static MethodDefinition GetMethodDefinition (this TypeDefinition td, MethodInfo method)
		{
			if (MethodMap.TryGetValue (method, out var md))
				return md;

			foreach (var m in td.Methods)
				if (MethodsAreEqual (method, m)) {
					MethodMap [method] = m;

					return m;
				}

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

				if ((string.IsNullOrEmpty (name) || string.IsNullOrEmpty (signature)) && constructorArguments.Length != 3)
					continue;

				if (string.IsNullOrEmpty (name))
					name = constructorArguments [0].Value.ToString ();

				if (string.IsNullOrEmpty (signature))
					signature = constructorArguments [1].Value.ToString ();

				if (string.IsNullOrEmpty (name) || string.IsNullOrEmpty (signature))
					continue;

				methodName = MarshalMemberBuilder.GetMarshalMethodName (name, signature);
				name = $"n_{name}";

				return true;
			}

			return false;
		}

		public static bool NeedsMarshalMethod (this MethodDefinition md, DirectoryAssemblyResolver resolver, TypeDefinitionCache cache, MethodInfo method, ref string name, ref string methodName, ref string signature)
		{
			var m = md;

			while (m != null) {
				if (CheckMethod (m, ref name, ref methodName, ref signature))
					return true;

				m = m.GetBaseDefinition (cache);

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
					App.Warning (Message.WarningCouldntFindInterface, iface.FullName);
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
	}
}

class _Jonp_ReferenceCodeGen
{
	static void A (IntPtr jnienv, IntPtr klass, int value)
	{
		JniRuntime jvm = JniEnvironment.Runtime;
		JniRuntime.JniValueManager vm;

		var envp = new JniTransition (jnienv);
		try {
			vm = jvm.ValueManager;
			vm.WaitForGCBridgeProcessing ();
		} catch (Exception e) when (jvm.ExceptionShouldTransitionToJni (e)) {
			envp.SetPendingException (e);
		} finally {
			envp.Dispose ();
		}
	}

	static void B ()
	{
	}

	[JniAddNativeMethodRegistration]
	static void RegisterNativeMethods (JniNativeMethodRegistrationArguments args)
	{
		var methods = new [] {
			new JniNativeMethodRegistration ("a", "()V", new Action<IntPtr, IntPtr, int> (A)),
			new JniNativeMethodRegistration ("b", "()V", new Action (B)),
		};
		args.AddRegistrations (methods);
	}
}
