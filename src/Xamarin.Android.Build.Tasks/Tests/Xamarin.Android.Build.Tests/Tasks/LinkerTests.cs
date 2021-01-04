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
		[Category ("DotNetIgnore")] // HttpClientHandler options not implemented in .NET 5+ yet
		public void PreserveCustomHttpClientHandlers ()
		{
			PreserveCustomHttpClientHandler ("Xamarin.Android.Net.AndroidClientHandler", "",
				"temp/PreserveAndroidHttpClientHandler", "android/assets/Mono.Android.dll");
			PreserveCustomHttpClientHandler ("System.Net.Http.MonoWebRequestHandler", "System.Net.Http",
				"temp/PreserveMonoWebRequestHandler", "android/assets/System.Net.Http.dll");
		}

		[Test]
		[Category ("DotNetIgnore")] // n/a on .NET 5+
		public void WarnAboutAppDomains ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = isRelease };
			proj.MainActivity = proj.DefaultMainActivity.Replace ("base.OnCreate (bundle);", "base.OnCreate (bundle);\nvar appDomain = System.AppDomain.CreateDomain (\"myDomain\");");
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "2 Warning(s)"), "MSBuild should count 2 warnings.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "warning CS0618: 'AppDomain.CreateDomain(string)' is obsolete: 'AppDomain.CreateDomain will no longer be supported in .NET 5 and later."), "Should warn CS0618 about creating AppDomain.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "warning XA2000: Use of AppDomain.CreateDomain()"), "Should warn XA2000 about creating AppDomain.");
			}
		}

		[Test]
		public void LinkDescription ()
		{
			string assembly_name = Builder.UseDotNet ? "System.Console" : "mscorlib";
			string linker_xml = "<linker/>";

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem ("LinkDescription", "linker.xml") {
						TextContent = () => linker_xml
					}
				}
			};
			// So we can use Mono.Cecil to open assemblies directly
			proj.SetProperty ("AndroidEnableAssemblyCompression", "False");

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");

				linker_xml =
$@"<linker>
	<assembly fullname=""{assembly_name}"">
		<type fullname=""System.Console"">
			<method name=""Beep"" />
		</type>
	</assembly>
</linker>";
				proj.Touch ("linker.xml");

				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build should have succeeded.");

				var apk = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}.apk");
				FileAssert.Exists (apk);
				using (var zip = ZipHelper.OpenZip (apk)) {
					var entry = zip.ReadEntry ($"assemblies/{assembly_name}.dll");
					Assert.IsNotNull (entry, $"{assembly_name}.dll should exist in apk!");
					using (var stream = new MemoryStream ()) {
						entry.Extract (stream);
						stream.Position = 0;
						using (var assembly = AssemblyDefinition.ReadAssembly (stream)) {
							var type = assembly.MainModule.GetType ("System.Console");
							var method = type.Methods.FirstOrDefault (p => p.Name == "Beep");
							Assert.IsNotNull (method, "System.Console.Beep should exist!");
						}
					}
				}
			}
		}

		[Test]
		public void LinkWithNullAttribute ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem ("Compile", "NullAttribute.cs") { TextContent = () => @"
using System;
using Android.Content;
using Android.Widget;
namespace UnnamedProject {
	public class MyAttribute : Attribute
	{
		Type[] types;

		public Type[] Types {
			get { return types; }
		}

		public MyAttribute (Type[] ta)
		{
			types = ta;
		}
	}

	[MyAttribute (null)]
	public class AttributedButtonStub : Button
	{
		public AttributedButtonStub (Context context) : base (context)
		{
		}
	}
}"
					},
				}
			};

			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
$@"			var myButton = new AttributedButtonStub (this);
			myButton.Text = ""Bug #35710"";
");

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Building a project with a null attribute value should have succeded.");
			}
		}

		[Test]
		[Category ("DotNetIgnore")]
		public void AndroidAddKeepAlives ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem ("Compile", "Method.cs") { TextContent = () => @"
using System;
using Java.Interop;

namespace UnnamedProject {
	public class MyClass : Java.Lang.Object
	{
		[Android.Runtime.Register(""MyMethod"")]
		public unsafe bool MyMethod (Android.OS.IBinder windowToken, [global::Android.Runtime.GeneratedEnum] Android.Views.InputMethods.HideSoftInputFlags flags)
        {
            const string __id = ""hideSoftInputFromWindow.(Landroid/os/IBinder;I)Z"";
            try {
                JniArgumentValue* __args = stackalloc JniArgumentValue [1];
                __args [0] = new JniArgumentValue ((windowToken == null) ? IntPtr.Zero : ((global::Java.Lang.Object) windowToken).Handle);
                __args [1] = new JniArgumentValue ((int) flags);
                var __rm = JniPeerMembers.InstanceMethods.InvokeAbstractBooleanMethod (__id, this, __args);
                return __rm;
            } finally {
            }
        }
	}
}"
					},
				}
			};

			proj.SetProperty ("AllowUnsafeBlocks", "True");

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Building a project should have succeded.");

				var assemblyPath = b.Output.GetIntermediaryPath (Path.Combine ("android", "assets", "UnnamedProject.dll"));
				using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath)) {
					Assert.IsTrue (assembly != null);

					var td = assembly.MainModule.GetType ("UnnamedProject.MyClass");
					Assert.IsTrue (td != null);

					var mr = td.GetMethods ().Where (m => m.Name == "MyMethod").FirstOrDefault ();
					Assert.IsTrue (mr != null);

					var md = mr.Resolve ();
					Assert.IsTrue (md != null);

					bool hasKeepAliveCall = false;
					foreach (var i in md.Body.Instructions) {
						if (i.OpCode.Code != Mono.Cecil.Cil.Code.Call)
							continue;

						if (!i.Operand.ToString ().Contains ("System.GC::KeepAlive"))
							continue;

						hasKeepAliveCall = true;
						break;
					}

					Assert.IsTrue (hasKeepAliveCall);
				}
			}
		}
	}
}
