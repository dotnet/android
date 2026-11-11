using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class JavaNativeTypeManagerTests
	{
		PackageNamingPolicy existingValue;

		[SetUp]
		public void SetUp ()
		{
			existingValue = JavaNativeTypeManager.PackageNamingPolicy;
		}

		[TearDown]
		public void TearDown ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = existingValue;
		}

		[Test]
		public void Crc64 ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseCrc64;
			// System.String moved assemblies in .NET
			Assert.AreEqual ("crc64d04135c992393d83", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[Test]
		public void Lowercase ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.Lowercase;
			Assert.AreEqual ("system", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[Test]
		public void LowercaseWithAssemblyName ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseWithAssemblyName;
			// System.String moved assemblies in .NET
			Assert.AreEqual ("assembly_system_private_corelib.system", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[TestCase (PackageNamingPolicy.Lowercase, "example/Peer")]
		[TestCase (PackageNamingPolicy.LowercaseWithAssemblyName, "assembly_policyprobe/example/Peer")]
		[TestCase (PackageNamingPolicy.LowercaseCrc64, "crc642048bf8bbd1f1138/Peer")]
		public void ExplicitPolicyIgnoresGlobalPolicy (PackageNamingPolicy policy, string expected)
		{
			using var assembly = AssemblyDefinition.CreateAssembly (
				new AssemblyNameDefinition ("PolicyProbe", new Version (1, 0)), "PolicyProbe", ModuleKind.Dll);
			var module = assembly.MainModule;
			var javaObject = new TypeDefinition ("Android.Runtime", "IJavaObject", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
			module.Types.Add (javaObject);
			var peer = new TypeDefinition ("Example", "Peer", TypeAttributes.Public);
			peer.Interfaces.Add (new InterfaceImplementation (javaObject));
			module.Types.Add (peer);
			var nested = new TypeDefinition ("", "Nested", TypeAttributes.NestedPublic);
			nested.Interfaces.Add (new InterfaceImplementation (javaObject));
			peer.NestedTypes.Add (nested);
			var cache = new TypeDefinitionCache ();

			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.Lowercase;
			Assert.AreEqual (expected, JavaNativeTypeManager.ToJniName (peer, cache, policy));
			Assert.AreEqual ("assembly_policyprobe/example/Peer",
				JavaNativeTypeManager.ToJniName (peer, cache, PackageNamingPolicy.LowercaseWithAssemblyName));
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseWithAssemblyName;
			Assert.AreEqual (expected, JavaNativeTypeManager.ToJniName (peer, cache, policy));
			Assert.AreEqual (expected + "_Nested", JavaNativeTypeManager.ToJniName (nested, cache, policy));
			Assert.AreEqual (PackageNamingPolicy.LowercaseWithAssemblyName, JavaNativeTypeManager.PackageNamingPolicy);
		}

		[Test]
		public void ExplicitPolicyPreservesRegisteredAndFrameworkNames ()
		{
			using var assembly = AssemblyDefinition.ReadAssembly (typeof (Java.Lang.Object).Assembly.Location);
			var cache = new TypeDefinitionCache ();
			Assert.AreEqual ("java/lang/Object", JavaNativeTypeManager.ToJniName (
				assembly.MainModule.GetType ("Java.Lang.Object"), cache, PackageNamingPolicy.LowercaseWithAssemblyName));

			using var framework = AssemblyDefinition.CreateAssembly (
				new AssemblyNameDefinition ("Mono.Android", new Version (1, 0)), "Mono.Android", ModuleKind.Dll);
			var type = new TypeDefinition ("Android.Views", "View", TypeAttributes.Public);
			framework.MainModule.Types.Add (type);
			Assert.AreEqual ("android.views", JavaNativeTypeManager.GetPackageName (type, cache, PackageNamingPolicy.LowercaseWithAssemblyName));
		}

		[Test]
		[TestCase (typeof (string), "java/lang/String")]
		[TestCase (typeof (Type),   "java/lang/Object")]
		public void ToJniName (Type type, string expected)
		{
			string actual = JavaNativeTypeManager.ToJniName (type);
			Assert.AreEqual (expected, actual);
		}
	}
}
