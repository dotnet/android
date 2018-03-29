using MonoDroid.Generation;
using System.Linq;

namespace generatortests
{
	class TestClass : ClassGen
	{
		public TestClass (string baseType, string javaName) : base (new TestBaseSupport (javaName))
		{
			this.BaseType = BaseType;
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
		ISymbol managedSymbol;
		Parameter setterParameter;

		public TestField (string type, string name)
		{
			this.type = type;
			Name = name;

			//HACK: SymbolTable is static, hence problematic for testing
			//	If running a unit test first, we need to add java.lang.String.
			//	If an integration test was run, java.lang.String exists already.
			//	Down the line SymbolTable should be refactored to be non-static, so this could be done in [SetUp]
			managedSymbol = SymbolTable.Lookup (type) ?? 
				new SimpleSymbol ("", "java.lang.String", "Ljava/lang/String;", "Java.Lang.String");
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
				if (setterParameter == null)
					setterParameter = new Parameter ("value", type, managedSymbol.FullName, isEnumified);
				return setterParameter;
			}
		}
	}
}
