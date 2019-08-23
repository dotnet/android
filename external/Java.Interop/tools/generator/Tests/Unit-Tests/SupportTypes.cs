using MonoDroid.Generation;
using System.Linq;

namespace generatortests
{
	class TestClass : ClassGen
	{
		public TestClass (string baseType, string javaName) : base (new TestBaseSupport (javaName))
		{
			BaseType = baseType;
			IsAbstract = false;
			IsFinal = false;
		}
	}

	class TestBaseSupport : GenBaseSupport
	{
		public TestBaseSupport (string javaName)
		{
			var split = javaName.Split ('.');
			Name = split.Last ();
			FullName = javaName;
			JavaSimpleName = Name;
			PackageName = javaName.Substring (0, javaName.Length - Name.Length - 1);
			Namespace = PackageName;
			IsGeneratable = true;
			Visibility = "public";
			TypeParameters = new GenericParameterDefinitionList ();
		}
	}

	class TestField : Field
	{
		public TestField (string type, string name)
		{
			TypeName = type;
			JavaName = name;
			Name = name;
			Visibility = "public";

			//NOTE: passing `type` for `managedType`, required since `SymbolTable` is no longer static
			//	This currently isn't causing any test failures
			SetterParameter = new Parameter ("value", TypeName, TypeName, IsEnumified);
		}

		public TestField SetStatic ()
		{
			IsStatic = true;
			return this;
		}

		public TestField SetConstant (string value = null)
		{
			IsFinal = true;
			IsStatic = true;
			Value = value;
			return this;
		}

		public TestField SetEnumified ()
		{
			IsEnumified = true;
			SetterParameter = new Parameter ("value", TypeName, TypeName, IsEnumified);
			return this;
		}

		public TestField SetDeprecated (string comment = null)
		{
			IsDeprecated = true;
			DeprecatedComment = comment;
			return this;
		}

		public TestField SetVisibility (string visibility)
		{
			Visibility = visibility;
			return this;
		}

		public TestField SetValue (string value)
		{
			Value = value;
			return this;
		}
	}

	class TestMethod : Method
	{
		public TestMethod (GenBase @class, string name, string @return = "void") : base (@class)
		{
			Name = name;
			JavaName = name;
			SourceApiLevel = 27;
			IsVirtual = true;
			Visibility = "public";
			Return = @return;

			FillReturnType ();
		}

		public TestMethod SetApiLevel (int apiLevel)
		{
			SourceApiLevel = apiLevel;
			return this;
		}

		public TestMethod SetManagedReturn (string managedReturn)
		{
			ManagedReturn = managedReturn;
			FillReturnType ();
			return this;
		}

		public TestMethod SetFinal ()
		{
			IsFinal = true;
			IsVirtual = false;
			return this;
		}

		public TestMethod SetAbstract ()
		{
			IsAbstract = true;
			return this;
		}

		public TestMethod SetStatic ()
		{
			IsFinal =
				IsStatic = true;
			IsVirtual = false;
			return this;
		}

		public TestMethod SetAsyncify ()
		{
			GenerateAsyncWrapper = true;
			return this;
		}

		public TestMethod SetVisibility (string visibility)
		{
			Visibility = visibility;
			return this;
		}

		public TestMethod SetDeprecated (string deprecated)
		{
			Deprecated = deprecated;
			return this;
		}

		public TestMethod SetReturnEnumified ()
		{
			IsReturnEnumified = true;
			return this;
		}

		public TestMethod SetDefaultInterfaceMethod ()
		{
			IsAbstract = false;
			IsStatic = false;
			IsInterfaceDefaultMethod = true;

			return this;
		}
	}

	class TestCtor : Ctor
	{
		public TestCtor (GenBase @class, string name) : base (@class)
		{
			Name = name;
		}

		public TestCtor SetAnnotation (string value)
		{
			Annotation = value;
			return this;
		}

		public TestCtor SetCustomAttributes (string value)
		{
			CustomAttributes = value;
			return this;
		}

		public TestCtor SetDeprecated (string value)
		{
			Deprecated = value;
			return this;
		}

		public TestCtor SetIsNonStaticNestedType (bool value)
		{
			IsNonStaticNestedType = value;
			return this;
		}

		public TestCtor SetVisibility (string value)
		{
			Visibility = value;
			return this;
		}
	}

	class TestInterface : InterfaceGen
	{
		public TestInterface (string argsType, string javaName) : base (new TestBaseSupport (javaName))
		{
			ArgsType = argsType;
		}
	}

	static class SupportTypeBuilder
	{
		public static TestClass CreateClass (string className, CodeGenerationOptions options)
		{
			var @class = new TestClass ("Object", className);

			var ctor_name = className.Contains ('.') ? className.Substring (className.LastIndexOf ('.')) : className;
			@class.Ctors.Add (CreateConstructor (@class, ctor_name, options));
			@class.Ctors.Add (CreateConstructor (@class, ctor_name, options, new Parameter ("p0", "java.lang.String", "string", false)));

			@class.Properties.Add (CreateProperty (@class, "Count", "int", options));
			@class.Properties.Add (CreateProperty (@class, "Key", "java.lang.String", options));
			@class.Properties.Add (CreateProperty (@class, "StaticCount", "int", options, true));
			@class.Properties.Add (CreateProperty (@class, "AbstractCount", "int", options, false, true));

			@class.Methods.Add (CreateMethod (@class, "GetCountForKey", options, "int", false, parameters: new Parameter ("key", "java.lang.String", "string", false)));
			@class.Methods.Add (CreateMethod (@class, "Key", options, "java.lang.String"));
			@class.Methods.Add (CreateMethod (@class, "StaticMethod", options, "void", true));
			@class.Methods.Add (CreateMethod (@class, "AbstractMethod", options, "void", false, true));

			return @class;
		}

		public static TestClass CreateClassWithProperty (string className, string classJavaName, string propertyName, string propertyType, CodeGenerationOptions options)
		{
			var @class = new TestClass (className, classJavaName);

			@class.Properties.Add (CreateProperty (@class, propertyName, propertyType, options));

			return @class;
		}

		public static TestCtor CreateConstructor (GenBase parent, string methodName, CodeGenerationOptions options, params Parameter [] parameters)
		{
			var ctor = new TestCtor (parent, methodName);

			foreach (var p in parameters)
				ctor.Parameters.Add (p);

			ctor.Validate (options, null, new CodeGeneratorContext ());

			return ctor;
		}

		public static TestInterface CreateEmptyInterface (string interfaceName)
		{
			var iface = new TestInterface (null, interfaceName);

			return iface;
		}

		public static TestInterface CreateInterface (string interfaceName, CodeGenerationOptions options)
		{
			var iface = CreateEmptyInterface (interfaceName);

			iface.Properties.Add (CreateProperty (iface, "Count", "int", options));
			iface.Properties.Add (CreateProperty (iface, "Key", "java.lang.String", options));
			iface.Properties.Add (CreateProperty (iface, "StaticCount", "int", options, true));
			iface.Properties.Add (CreateProperty (iface, "AbstractCount", "int", options, false, true));

			iface.Methods.Add (CreateMethod (iface, "GetCountForKey", options, "int", false, parameters: new Parameter ("key", "java.lang.String", "string", false)));
			iface.Methods.Add (CreateMethod (iface, "Key", options, "java.lang.String"));
			iface.Methods.Add (CreateMethod (iface, "StaticMethod", options, "void", true));
			iface.Methods.Add (CreateMethod (iface, "AbstractMethod", options, "void", false, true));

			return iface;
		}

		public static TestMethod CreateMethod (GenBase parent, string methodName, CodeGenerationOptions options, string returnType = "void", bool isStatic = false, bool isAbstract = false, params Parameter[] parameters)
		{
			var method = new TestMethod (parent, methodName, returnType);

			if (isStatic)
				method.SetStatic ();
			if (isAbstract)
				method.SetAbstract ();

			foreach (var p in parameters)
				method.Parameters.Add (p);

			method.Validate (options, null, new CodeGeneratorContext ());
			method.RetVal.Validate (options, null, new CodeGeneratorContext ());

			return method;
		}

		public static Property CreateProperty (GenBase parent, string propertyName, string propertyType, CodeGenerationOptions options, bool isStatic = false, bool isAbstract = false)
		{
			var prop = new Property (propertyName) {
				Getter = CreateMethod (parent, $"get_{propertyName}", options, propertyType, isStatic, isAbstract),
				Setter = CreateMethod (parent, $"set_{propertyName}", options, "void", isStatic, isAbstract, parameters: new Parameter ("value", propertyType, propertyType, false))
			};

			return prop;
		}

		public static ParameterList CreateParameterList (CodeGenerationOptions options)
		{
			var list = new ParameterList {
				new Parameter ("value", "int", "int", false),
				new Parameter ("str", "java.lang.String", "string", false),
				new Parameter ("flag", "int", "OptionTypes", true)
			};

			foreach (var p in list)
				p.Validate (options, null, new CodeGeneratorContext ());

			return list;
		}
	}
}
