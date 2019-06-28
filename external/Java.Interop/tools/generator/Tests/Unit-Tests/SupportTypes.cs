using MonoDroid.Generation;
using System.Linq;

namespace generatortests
{
	class TestClass : ClassGen
	{
		public TestClass (string baseType, string javaName) : base (new TestBaseSupport (javaName))
		{
			this.BaseType = baseType;
		}

		public override bool IsAbstract => false;

		public override bool IsFinal => false;

		public override string BaseType { get; set; }
	}

	class TestBaseSupport : GenBaseSupport
	{
		public TestBaseSupport (string javaName)
		{
			var split = javaName.Split ('.');
			Name = split.Last ();
			FullName = javaName;
			PackageName = javaName.Substring (0, javaName.Length - Name.Length - 1);
		}

		public override bool IsAcw => false;

		public override bool IsDeprecated => false;

		public override string DeprecatedComment => string.Empty;

		public override bool IsGeneratable => true;

		public override bool IsGeneric => false;

		public override bool IsObfuscated => false;

		public override string FullName { get; set; }

		public override string Name { get; set; }

		public override string Namespace => PackageName;

		public override string JavaSimpleName => Name;

		public override string PackageName { get; set; }

		public override string Visibility => "public";

		GenericParameterDefinitionList typeParameters = new GenericParameterDefinitionList ();

		public override GenericParameterDefinitionList TypeParameters => typeParameters;
	}

	class TestField : Field
	{
		bool isFinal, isStatic, isEnumified, isDeprecated;
		string type, value, deprecatedComment, visibility = "public";
		Parameter setterParameter;

		public TestField (string type, string name)
		{
			this.type = type;
			Name = name;
		}

		public TestField SetStatic ()
		{
			isStatic = true;
			return this;
		}

		public TestField SetConstant (string value = null)
		{
			isFinal =
				isStatic = true;
			this.value = value;
			return this;
		}

		public TestField SetEnumified ()
		{
			isEnumified = true;
			return this;
		}

		public TestField SetDeprecated (string comment = null)
		{
			isDeprecated = true;
			deprecatedComment = comment;
			return this;
		}

		public TestField SetVisibility (string visibility)
		{
			this.visibility = visibility;
			return this;
		}

		public TestField SetValue (string value)
		{
			this.value = value;
			return this;
		}

		public override bool IsDeprecated => isDeprecated;

		public override string DeprecatedComment => deprecatedComment;

		public override bool IsFinal => isFinal;

		public override bool IsStatic => isStatic;

		public override string JavaName => Name;

		public override bool IsEnumified => isEnumified;

		public override string TypeName => type;

		public override string Name { get; set; }

		public override string Value => value;

		public override string Visibility => visibility;

		protected override Parameter SetterParameter {
			get {
				if (setterParameter == null) {
					//NOTE: passing `type` for `managedType`, required since `SymbolTable` is no longer static
					//	This currently isn't causing any test failures
					setterParameter = new Parameter ("value", type, type, isEnumified);
				}
				return setterParameter;
			}
		}
	}

	class TestMethod : Method
	{
		int apiLevel = 27;
		string @return, managedReturn, visibility = "public", deprecated;
		bool isAbstract, isFinal, isStatic, asyncify, isReturnEnumified;

		public TestMethod (GenBase @class, string name, string @return = "void") : base (@class)
		{
			Name = name;
			this.@return = @return;
			FillReturnType ();
		}

		public TestMethod SetApiLevel (int apiLevel)
		{
			this.apiLevel = apiLevel;
			return this;
		}

		public TestMethod SetManagedReturn (string managedReturn)
		{
			this.managedReturn = managedReturn;
			FillReturnType ();
			return this;
		}

		public TestMethod SetFinal ()
		{
			isFinal = true;
			IsVirtual = false;
			return this;
		}

		public TestMethod SetAbstract ()
		{
			isAbstract = true;
			return this;
		}

		public TestMethod SetStatic ()
		{
			isFinal =
				isStatic = true;
			IsVirtual = false;
			return this;
		}

		public TestMethod SetAsyncify ()
		{
			asyncify = true;
			return this;
		}

		public TestMethod SetVisibility (string visibility)
		{
			this.visibility = visibility;
			return this;
		}

		public TestMethod SetDeprecated (string deprecated)
		{
			this.deprecated = deprecated;
			return this;
		}

		public TestMethod SetReturnEnumified ()
		{
			this.isReturnEnumified = true;
			return this;
		}

		public override string ArgsType => null;

		public override string EventName => null;

		public override bool IsAbstract => isAbstract;

		public override bool IsFinal => isFinal;

		public override bool IsInterfaceDefaultMethod => false;

		public override string JavaName => Name;

		public override bool IsStatic => isStatic;

		public override bool IsVirtual { get; set; } = true;

		public override string Return => @return;

		public override bool IsReturnEnumified => isReturnEnumified;

		public override string ManagedReturn => managedReturn;

		public override int SourceApiLevel => apiLevel;

		public override bool Asyncify => asyncify;

		public override string CustomAttributes => null;

		public override string Name { get; set; }

		protected override string PropertyNameOverride => null;

		public override string AssemblyName => null;

		public override string Deprecated => deprecated;

		public override string Visibility => visibility;
	}

	class TestCtor : Ctor
	{
		string custom_attributes;
		string deprecated;
		bool is_non_static_nested_type;
		string visibility;

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
			custom_attributes = value;
			return this;
		}

		public TestCtor SetDeprecated (string value)
		{
			deprecated = value;
			return this;
		}

		public TestCtor SetIsNonStaticNestedType (bool value)
		{
			is_non_static_nested_type = value;
			return this;
		}

		public TestCtor SetVisibility (string value)
		{
			visibility = value;
			return this;
		}

		public override string CustomAttributes => custom_attributes;

		public override string Deprecated => deprecated;

		public override bool IsNonStaticNestedType => is_non_static_nested_type;

		public override string Name { get; set; }

		public override string Visibility => visibility;
	}

	class TestInterface : InterfaceGen
	{
		string args_type;

		public TestInterface (string argsType, string javaName) : base (new TestBaseSupport (javaName))
		{
			args_type = argsType;
		}

		public override string ArgsType => args_type;
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

			ctor.Validate (options, null);

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

			method.Validate (options, null);
			method.RetVal.Validate (options, null);

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
				p.Validate (options, null);

			return list;
		}
	}
}
