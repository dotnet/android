using System;
using System.Linq;
using Mono.Cecil;
using Mono.Linker;
using MonoDroid.Tuner;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	public class LinkerTests : BaseTest
	{
		[Test]
		public void FixAbstractMethodsStep_SkipDimMembers ()
		{
			var step = new FixAbstractMethodsStep ();
			var pipeline = new Pipeline ();
			var context = new LinkContext (pipeline);

			var android = CreateFauxMonoAndroidAssembly ();
			var jlo = android.MainModule.GetType ("Java.Lang.Object");

			context.Resolver.CacheAssembly (android);

			var assm = AssemblyDefinition.CreateAssembly (new AssemblyNameDefinition ("DimTest", new Version ()), "DimTest", ModuleKind.Dll);
			var void_type = assm.MainModule.ImportReference (typeof (void));

			// Create interface
			var iface = new TypeDefinition ("MyNamespace", "IMyInterface", TypeAttributes.Interface);

			var abstract_method = new MethodDefinition ("MyAbstractMethod", MethodAttributes.Abstract, void_type);
			var default_method = new MethodDefinition ("MyDefaultMethod", MethodAttributes.Public, void_type);

			iface.Methods.Add (abstract_method);
			iface.Methods.Add (default_method);

			assm.MainModule.Types.Add (iface);

			// Create implementing class
			var impl = new TypeDefinition ("MyNamespace", "MyClass", TypeAttributes.Public, jlo);
			impl.Interfaces.Add (new InterfaceImplementation (iface));

			assm.MainModule.Types.Add (impl);

			context.Resolver.CacheAssembly (assm);

			step.Process (context);

			// We should have generated an override for MyAbstractMethod
			Assert.IsTrue (impl.Methods.Any (m => m.Name == "MyAbstractMethod"));

			// We should not have generated an override for MyDefaultMethod
			Assert.IsFalse (impl.Methods.Any (m => m.Name == "MyDefaultMethod"));
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
	}
}
