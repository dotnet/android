using Android.Runtime;
using Mono.Cecil;
using MonoDroid.Generation;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace Java.Lang
{
	[Register ("java/lang/Object")]
	public class Object { }

	[Register ("java/lang/String")]
	public sealed class String : Object { }
}

namespace Com.Mypackage
{
	[Register ("com/mypackage/foo")]
	public class Foo : Java.Lang.Object
	{
		[Register ("foo", "()V", "")]
		public Foo () { }

		[Register ("bar", "()V", "")]
		public void Bar () { }

		[Register ("barWithParams", "(ZID)Ljava/lang/String;", "")]
		public string BarWithParams (bool a, int b, double c) => string.Empty;

		[Register ("unknownTypes", "(Lmy/package/foo/Unknown;)V", "")]
		public void UnknownTypes (object unknown) { }

		[Register ("unknownTypes", "(Lmy/package/foo/Unknown;)Lmy/package/foo/Unknown;", "")]
		public object UnknownTypesReturn (object unknown) => null;

		[Register ("value")]
		public const int Value = 1234;
	}

	[Register ("com/mypackage/service")]
	public interface IService { }
}

namespace generatortests
{
	[TestFixture]
	public class ManagedTests
	{
		string tempFile;
		ModuleDefinition module;
		CodeGenerationOptions options;

		[SetUp]
		public void SetUp ()
		{
			tempFile = Path.GetTempFileName ();
			File.Copy (GetType ().Assembly.Location, tempFile, true);
			module = ModuleDefinition.ReadModule (tempFile);
			options = new CodeGenerationOptions ();

			foreach (var type in module.Types.Where(t => t.IsClass && t.Namespace == "Java.Lang")) {
				var @class = new ManagedClassGen (type, options);
				Assert.IsTrue (@class.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext()), "@class.Validate failed!");
				options.SymbolTable.AddType (@class);
			}
		}

		[TearDown]
		public void TearDown ()
		{
			module.Dispose ();
			File.Delete (tempFile);
		}

		[Test]
		public void Class ()
		{
			var @class = new ManagedClassGen (module.GetType ("Com.Mypackage.Foo"), options);
			Assert.IsTrue (@class.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "@class.Validate failed!");

			Assert.AreEqual ("public", @class.Visibility);
			Assert.AreEqual ("Foo", @class.Name);
			Assert.AreEqual ("com.mypackage.foo", @class.JavaName);
			Assert.AreEqual ("Lcom/mypackage/foo;", @class.JniName);
			Assert.IsFalse (@class.IsAbstract);
			Assert.IsFalse (@class.IsFinal);
			Assert.IsFalse (@class.IsDeprecated);
			Assert.IsNull (@class.DeprecatedComment);
		}

		[Test]
		public void Method ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = new ManagedClassGen (type, options);
			var method = new ManagedMethod (@class, type.Methods.First (m => m.Name == "Bar"));
			Assert.IsTrue (method.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");

			Assert.AreEqual ("public", method.Visibility);
			Assert.AreEqual ("void", method.Return);
			Assert.AreEqual ("System.Void", method.ReturnType);
			Assert.AreEqual ("Bar", method.Name);
			Assert.AreEqual ("bar", method.JavaName);
			Assert.AreEqual ("()V", method.JniSignature);
			Assert.IsFalse (method.IsAbstract);
			Assert.IsFalse (method.IsFinal);
			Assert.IsFalse (method.IsStatic);
			Assert.IsNull (method.Deprecated);
		}

		[Test]
		public void Method_Matches_True ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = new ManagedClassGen (type, options);
			var unknownTypes = type.Methods.First (m => m.Name == "UnknownTypes");
			var methodA = new ManagedMethod (@class, unknownTypes);
			var methodB = new ManagedMethod (@class, unknownTypes);
			Assert.IsTrue (methodA.Matches (methodB), "Methods should match!");
		}

		[Test]
		public void Method_Matches_False ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = new ManagedClassGen (type, options);
			var unknownTypesA = type.Methods.First (m => m.Name == "UnknownTypes");
			var unknownTypesB = type.Methods.First (m => m.Name == "UnknownTypesReturn");
			unknownTypesB.Name = "UnknownTypes";
			var methodA = new ManagedMethod (@class, unknownTypesA);
			var methodB = new ManagedMethod (@class, unknownTypesB);
			//Everything the same besides return type
			Assert.IsFalse (methodA.Matches (methodB), "Methods should not match!");
		}

		[Test]
		public void MethodWithParameters ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = new ManagedClassGen (type, options);
			var method = new ManagedMethod (@class, type.Methods.First (m => m.Name == "BarWithParams"));
			Assert.IsTrue (method.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			Assert.AreEqual ("(ZID)Ljava/lang/String;", method.JniSignature);
			Assert.AreEqual ("java.lang.String", method.Return);
			Assert.AreEqual ("System.String", method.ManagedReturn);

			var parameter = method.Parameters [0];
			Assert.AreEqual ("a", parameter.Name);
			Assert.AreEqual ("bool", parameter.Type);
			Assert.AreEqual ("boolean", parameter.JavaType);
			Assert.AreEqual ("Z", parameter.JniType);

			parameter = method.Parameters [1];
			Assert.AreEqual ("b", parameter.Name);
			Assert.AreEqual ("int", parameter.Type);
			Assert.AreEqual ("int", parameter.JavaType);
			Assert.AreEqual ("I", parameter.JniType);

			parameter = method.Parameters [2];
			Assert.AreEqual ("c", parameter.Name);
			Assert.AreEqual ("double", parameter.Type);
			Assert.AreEqual ("double", parameter.JavaType);
			Assert.AreEqual ("D", parameter.JniType);
		}

		[Test]
		public void Ctor ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = new ManagedClassGen (type, options);
			var ctor = new ManagedCtor (@class, type.Methods.First (m => m.IsConstructor && !m.IsStatic));
			Assert.IsTrue (ctor.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "ctor.Validate failed!");

			Assert.AreEqual ("public", ctor.Visibility);
			Assert.AreEqual (".ctor", ctor.Name);
			Assert.AreEqual ("()V", ctor.JniSignature);
			Assert.IsNull (ctor.Deprecated);
		}

		[Test]
		public void Field ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = new ManagedClassGen (type, options);
			var field = new ManagedField (type.Fields.First (f => f.Name == "Value"));
			Assert.IsTrue (field.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "field.Validate failed!");

			Assert.AreEqual ("Value", field.Name);
			Assert.AreEqual ("value", field.JavaName);
			Assert.AreEqual ("1234", field.Value);
			Assert.AreEqual ("System.Int32", field.TypeName);
			Assert.IsTrue (field.IsStatic);
			Assert.IsTrue (field.IsConst);
		}

		[Test]
		public void Interface ()
		{
			var type = module.GetType ("Com.Mypackage.IService");
			var @interface = new ManagedInterfaceGen (type, options);
			Assert.IsTrue (@interface.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "interface.Validate failed!");

			Assert.AreEqual ("public", @interface.Visibility);
			Assert.AreEqual ("IService", @interface.Name);
			Assert.AreEqual ("com.mypackage.service", @interface.JavaName);
			Assert.AreEqual ("Lcom/mypackage/service;", @interface.JniName);
		}
	}
}
