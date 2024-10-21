using Android.Runtime;
using Mono.Cecil;
using MonoDroid.Generation;
using NUnit.Framework;
using System.Collections.Generic;
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

	[Register ("com/mypackage/FieldClass")]
	public class FieldClass : Java.Lang.Object
	{
		public NestedFieldClass field;

		public class NestedFieldClass : Java.Lang.Object { }
	}

}

namespace GenericTestClasses
{
	public class MyCollection<T> : List<T>
	{
		[Register ("mycollection", "()V", "")]
		public MyCollection (List<T> p0, List<string> p1)
		{
		}

		public List<T> field;

		public Dictionary<string, T> field2;

		[Register ("dostuff", "()V", "")]
		public Dictionary<T, List<string>> DoStuff (IEnumerable<KeyValuePair<T, List<List<T>>>> p)
		{
			return new Dictionary<T, List<string>> ();
		}
	}
}

namespace NullableTestTypes
{
#nullable enable
	public class NullableClass
	{
		[Register ("<init>", "(Ljava/lang/String;Ljava/lang/String;)", "")]
		public NullableClass (string notnull, string? nullable)
		{
		}

		public string? null_field;
		public string not_null_field = string.Empty;

		[Register ("nullable_return_method", "(Ljava/lang/String;Ljava/lang/String;)Ljava/lang/String;", "")]
		public string? NullableReturnMethod (string notnull, string? nullable) => null;

		[Register ("not_null_return_method", "(Ljava/lang/String;Ljava/lang/String;)Ljava/lang/String;", "")]
		public string NotNullReturnMethod (string notnull, string? nullable) => string.Empty;
	}
}
#nullable disable

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
				var @class = CecilApiImporter.CreateClass (type, options);
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
			var @class = CecilApiImporter.CreateClass (module.GetType ("Com.Mypackage.Foo"), options);
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
		public void FieldWithNestedType ()
		{
			var @class = CecilApiImporter.CreateClass (module.GetType ("Com.Mypackage.FieldClass"), options);
			var @class2 = CecilApiImporter.CreateClass (module.GetType ("Com.Mypackage.FieldClass/NestedFieldClass"), options);

			options.SymbolTable.AddType (@class);
			options.SymbolTable.AddType (@class2);

			Assert.IsTrue (@class.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "@class.Validate failed!");

			// Ensure the front slash is replaced with a period
			Assert.AreEqual ("Com.Mypackage.FieldClass.NestedFieldClass", @class.Fields [0].TypeName);
		}

		[Test]
		public void Method ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = CecilApiImporter.CreateClass (type, options);
			var method = CecilApiImporter.CreateMethod (@class, type.Methods.First (m => m.Name == "Bar"));
			Assert.IsTrue (method.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");

			Assert.AreEqual ("public", method.Visibility);
			Assert.AreEqual ("void", method.Return);
			Assert.AreEqual ("void", method.ReturnType);
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
			var @class = CecilApiImporter.CreateClass (type, options);
			var unknownTypes = type.Methods.First (m => m.Name == "UnknownTypes");
			var methodA = CecilApiImporter.CreateMethod (@class, unknownTypes);
			var methodB = CecilApiImporter.CreateMethod (@class, unknownTypes);
			Assert.IsTrue (methodA.Matches (methodB), "Methods should match!");
		}

		[Test]
		public void Method_Matches_False ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = CecilApiImporter.CreateClass (type, options);
			var unknownTypesA = type.Methods.First (m => m.Name == "UnknownTypes");
			var unknownTypesB = type.Methods.First (m => m.Name == "UnknownTypesReturn");
			unknownTypesB.Name = "UnknownTypes";
			var methodA = CecilApiImporter.CreateMethod (@class, unknownTypesA);
			var methodB = CecilApiImporter.CreateMethod (@class, unknownTypesB);
			//Everything the same besides return type
			Assert.IsFalse (methodA.Matches (methodB), "Methods should not match!");
		}

		[Test]
		public void MethodWithParameters ()
		{
			var type = module.GetType ("Com.Mypackage.Foo");
			var @class = CecilApiImporter.CreateClass (type, options);
			var method = CecilApiImporter.CreateMethod (@class, type.Methods.First (m => m.Name == "BarWithParams"));
			Assert.IsTrue (method.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "method.Validate failed!");
			Assert.AreEqual ("(ZID)Ljava/lang/String;", method.JniSignature);
			Assert.AreEqual ("java.lang.String", method.Return);
			Assert.AreEqual ("string", method.ManagedReturn);

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
			var @class = CecilApiImporter.CreateClass (type, options);
			var ctor = CecilApiImporter.CreateCtor (@class, type.Methods.First (m => m.IsConstructor && !m.IsStatic));
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
			var @class = CecilApiImporter.CreateClass (type, options);
			var field = CecilApiImporter.CreateField (type.Fields.First (f => f.Name == "Value"));
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
			var @interface = CecilApiImporter.CreateInterface (type, options);
			Assert.IsTrue (@interface.Validate (new CodeGenerationOptions (), new GenericParameterDefinitionList (), new CodeGeneratorContext ()), "interface.Validate failed!");

			Assert.AreEqual ("public", @interface.Visibility);
			Assert.AreEqual ("IService", @interface.Name);
			Assert.AreEqual ("com.mypackage.service", @interface.JavaName);
			Assert.AreEqual ("Lcom/mypackage/service;", @interface.JniName);
		}

		[Test]
		public void StripArity ()
		{
			var @class = CecilApiImporter.CreateClass (module.GetType ("GenericTestClasses.MyCollection`1"), options);

			// Class (Leave Arity on types)
			Assert.AreEqual ("GenericTestClasses.MyCollection`1", @class.FullName);

			// Constructor
			Assert.AreEqual ("System.Collections.Generic.List<T>", @class.Ctors [0].Parameters [0].RawNativeType);
			Assert.AreEqual ("System.Collections.Generic.List<System.String>", @class.Ctors [0].Parameters [1].RawNativeType);

			// Field
			Assert.AreEqual ("System.Collections.Generic.List<T>", @class.Fields [0].TypeName);
			Assert.AreEqual ("System.Collections.Generic.Dictionary<System.String,T>", @class.Fields [1].TypeName);

			// Method
			Assert.AreEqual ("System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<T,System.Collections.Generic.List<System.Collections.Generic.List<T>>>>", @class.Methods [0].Parameters [0].RawNativeType);
			Assert.AreEqual ("System.Collections.Generic.Dictionary<T,System.Collections.Generic.List<System.String>>", @class.Methods [0].ReturnType);
			Assert.AreEqual ("System.Collections.Generic.Dictionary<T,System.Collections.Generic.List<System.String>>", @class.Methods [0].ManagedReturn);
		}

		[Test]
		public void TypeNullability ()
		{
			var type = module.GetType ("NullableTestTypes.NullableClass");
			var gen = CecilApiImporter.CreateClass (module.GetType ("NullableTestTypes.NullableClass"), options);

			var not_null_field = CecilApiImporter.CreateField (type.Fields.First (f => f.Name == "not_null_field"));
			Assert.AreEqual (true, not_null_field.NotNull);

			var null_field = CecilApiImporter.CreateField (type.Fields.First (f => f.Name == "null_field"));
			Assert.AreEqual (false, null_field.NotNull);

			var null_method = CecilApiImporter.CreateMethod (gen, type.Methods.First (f => f.Name == "NullableReturnMethod"));
			Assert.AreEqual (false, null_method.ReturnNotNull);
			Assert.AreEqual (true, null_method.Parameters.First (f => f.Name == "notnull").NotNull);
			Assert.AreEqual (false, null_method.Parameters.First (f => f.Name == "nullable").NotNull);

			var not_null_method = CecilApiImporter.CreateMethod (gen, type.Methods.First (f => f.Name == "NotNullReturnMethod"));
			Assert.AreEqual (true, not_null_method.ReturnNotNull);
			Assert.AreEqual (true, not_null_method.Parameters.First (f => f.Name == "notnull").NotNull);
			Assert.AreEqual (false, not_null_method.Parameters.First (f => f.Name == "nullable").NotNull);

			var ctor = CecilApiImporter.CreateCtor (gen, type.Methods.First (f => f.Name == ".ctor"));
			Assert.AreEqual (true, ctor.Parameters.First (f => f.Name == "notnull").NotNull);
			Assert.AreEqual (false, ctor.Parameters.First (f => f.Name == "nullable").NotNull);
		}
	}
}
