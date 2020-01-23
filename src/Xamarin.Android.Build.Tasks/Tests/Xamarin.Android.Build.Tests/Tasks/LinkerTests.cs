using System;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Linker;
using MonoDroid.Tuner;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-2")]
	public class LinkerTests : BaseTest
	{
		[Test]
		public void FixAbstractMethodsStep_SkipDimMembers ()
		{
			var path = Path.Combine (Path.GetFullPath (XABuildPaths.TestOutputDirectory), "temp", TestName);
			var step = new FixAbstractMethodsStep (new TypeDefinitionCache ());
			var pipeline = new Pipeline ();

			Directory.CreateDirectory (path);

			using (var context = new LinkContext (pipeline)) {

				context.Resolver.AddSearchDirectory (path);

				var myAssemblyPath = Path.Combine (path, "MyAssembly.dll");

				using (var android = CreateFauxMonoAndroidAssembly ()) {
					android.Write (Path.Combine (path, "Mono.Android.dll"));
					CreateAbstractIfaceImplementation (myAssemblyPath, android);
				}

				using (var assm = context.Resolve (myAssemblyPath)) {
					step.Process (context);

					var impl = assm.MainModule.GetType ("MyNamespace.MyClass");

					Assert.IsTrue (impl.Methods.Any (m => m.Name == "MyAbstractMethod"), "We should have generated an override for MyAbstractMethod");
					Assert.IsFalse (impl.Methods.Any (m => m.Name == "MyDefaultMethod"), "We should not have generated an override for MyDefaultMethod");
				}
			}

			Directory.Delete (path, true);
		}

		static void CreateAbstractIfaceImplementation (string assemblyPath, AssemblyDefinition android)
		{
			using (var assm = AssemblyDefinition.CreateAssembly (new AssemblyNameDefinition ("DimTest", new Version ()), "DimTest", ModuleKind.Dll)) {
				var void_type = assm.MainModule.ImportReference (typeof (void));

				// Create interface
				var iface = new TypeDefinition ("MyNamespace", "IMyInterface", TypeAttributes.Interface);

				var abstract_method = new MethodDefinition ("MyAbstractMethod", MethodAttributes.Abstract, void_type);
				var default_method = new MethodDefinition ("MyDefaultMethod", MethodAttributes.Public, void_type);

				iface.Methods.Add (abstract_method);
				iface.Methods.Add (default_method);

				assm.MainModule.Types.Add (iface);

				// Create implementing class
				var jlo = assm.MainModule.Import (android.MainModule.GetType ("Java.Lang.Object"));
				var impl = new TypeDefinition ("MyNamespace", "MyClass", TypeAttributes.Public, jlo);
				impl.Interfaces.Add (new InterfaceImplementation (iface));

				assm.MainModule.Types.Add (impl);
				assm.Write (assemblyPath);
			}
		}

		[Test]
		public void FixAbstractMethodsStep_Explicit ()
		{
			var path = Path.Combine (Path.GetFullPath (XABuildPaths.TestOutputDirectory), "temp", TestName);
			var step = new FixAbstractMethodsStep (new TypeDefinitionCache ());
			var pipeline = new Pipeline ();

			Directory.CreateDirectory (path);

			using (var context = new LinkContext (pipeline)) {

				context.Resolver.AddSearchDirectory (path);

				var myAssemblyPath = Path.Combine (path, "MyAssembly.dll");

				using (var android = CreateFauxMonoAndroidAssembly ()) {
					android.Write (Path.Combine (path, "Mono.Android.dll"));
					CreateExplicitInterface (myAssemblyPath, android);
				}

				using (var assm = context.Resolve (myAssemblyPath)) {
					step.Process (context);

					var impl = assm.MainModule.GetType ("MyNamespace.MyClass");
					Assert.AreEqual (2, impl.Methods.Count, "MyClass should contain 2 methods");
					var method = impl.Methods.FirstOrDefault (m => m.Name == "MyNamespace.IMyInterface.MyMethod");
					Assert.IsNotNull (method, "MyNamespace.IMyInterface.MyMethod should exist");
					method = impl.Methods.FirstOrDefault (m => m.Name == "MyMissingMethod");
					Assert.IsNotNull (method, "MyMissingMethod should exist");
				}
			}

			Directory.Delete (path, true);
		}

		static void CreateExplicitInterface (string assemblyPath, AssemblyDefinition android)
		{
			using (var assm = AssemblyDefinition.CreateAssembly (new AssemblyNameDefinition ("NestedIFaceTest", new Version ()), "NestedIFaceTest", ModuleKind.Dll)) {
				var void_type = assm.MainModule.ImportReference (typeof (void));

				// Create interface
				var iface = new TypeDefinition ("MyNamespace", "IMyInterface", TypeAttributes.Interface);

				var iface_method = new MethodDefinition ("MyMethod", MethodAttributes.Abstract, void_type);
				iface.Methods.Add (iface_method);
				iface.Methods.Add (new MethodDefinition ("MyMissingMethod", MethodAttributes.Abstract, void_type));

				assm.MainModule.Types.Add (iface);

				// Create implementing class
				var jlo = assm.MainModule.Import (android.MainModule.GetType ("Java.Lang.Object"));
				var impl = new TypeDefinition ("MyNamespace", "MyClass", TypeAttributes.Public, jlo);
				impl.Interfaces.Add (new InterfaceImplementation (iface));

				var explicit_method = new MethodDefinition ("MyNamespace.IMyInterface.MyMethod", MethodAttributes.Abstract, void_type);
				explicit_method.Overrides.Add (new MethodReference (iface_method.Name, void_type, iface));
				impl.Methods.Add (explicit_method);

				assm.MainModule.Types.Add (impl);
				assm.Write (assemblyPath);
			}
		}

		static AssemblyDefinition CreateFauxMonoAndroidAssembly ()
		{
			var assm = AssemblyDefinition.CreateAssembly (new AssemblyNameDefinition ("Mono.Android", new Version ()), "DimTest", ModuleKind.Dll);
			var void_type = assm.MainModule.ImportReference (typeof (void));

			// Create fake JLO type
			var jlo = new TypeDefinition ("Java.Lang", "Object", TypeAttributes.Public);
			assm.MainModule.Types.Add (jlo);

			// Create fake Java.Lang.AbstractMethodError type
			var ame = new TypeDefinition ("Java.Lang", "AbstractMethodError", TypeAttributes.Public);
			ame.Methods.Add (new MethodDefinition (".ctor", MethodAttributes.Public, void_type));
			assm.MainModule.Types.Add (ame);

			return assm;
		}

		private void PreserveCustomHttpClientHandler (string handlerType, string handlerAssembly, string testProjectName, string assemblyPath)
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = true };
			proj.AddReferences ("System.Net.Http");
			string handlerTypeFullName = string.IsNullOrEmpty(handlerAssembly) ? handlerType : handlerType + ", " + handlerAssembly;
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidHttpClientHandlerType", handlerTypeFullName);
			proj.MainActivity = proj.DefaultMainActivity.Replace ("base.OnCreate (bundle);", "base.OnCreate (bundle);\nvar client = new System.Net.Http.HttpClient ();");
			using (var b = CreateApkBuilder (testProjectName)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				using (var assembly = AssemblyDefinition.ReadAssembly (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, assemblyPath))) {
					Assert.IsTrue (assembly.MainModule.GetType (handlerType) != null, $"'{handlerTypeFullName}' should have been preserved by the linker.");
				}
			}
		}

		[Test]
		public void PreserveCustomHttpClientHandlers ()
		{
			PreserveCustomHttpClientHandler ("Xamarin.Android.Net.AndroidClientHandler", "",
				"temp/PreserveAndroidHttpClientHandler", "android/assets/Mono.Android.dll");
			PreserveCustomHttpClientHandler ("System.Net.Http.MonoWebRequestHandler", "System.Net.Http",
				"temp/PreserveMonoWebRequestHandler", "android/assets/System.Net.Http.dll");
		}

		[Test]
		public void WarnAboutAppDomainsRelease ()
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = true };
			WarnAboutAppDomains (proj, TestName);
		}

		[Test]
		public void WarnAboutAppDomainsDebug ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			WarnAboutAppDomains (proj, TestName);
		}

		void WarnAboutAppDomains (XamarinAndroidApplicationProject proj, string testName)
		{
			proj.MainActivity = proj.DefaultMainActivity.Replace ("base.OnCreate (bundle);", "base.OnCreate (bundle);\nvar appDomain = System.AppDomain.CreateDomain (\"myDomain\");");
			var projDirectory = Path.Combine ("temp", testName);
			using (var b = CreateApkBuilder (projDirectory)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "2 Warning(s)"), "MSBuild should count 2 warnings.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "warning CS0618: 'AppDomain.CreateDomain(string)' is obsolete: 'AppDomain.CreateDomain will no longer be supported in .NET 5 and later."), "Should warn CS0618 about creating AppDomain.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "warning XA2000: Use of AppDomain.CreateDomain()"), "Should warn XA2000 about creating AppDomain.");
			}
		}
	}
}
