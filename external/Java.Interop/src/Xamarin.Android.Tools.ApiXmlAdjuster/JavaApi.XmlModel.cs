using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public partial class JavaApi
	{
		public JavaApi ()
		{
			Packages = new List<JavaPackage> ();
		}

		public string ExtendedApiSource { get; set; }
		public IList<JavaPackage> Packages { get; set; }
	}

	public partial class JavaPackage
	{
		public JavaPackage (JavaApi parent)
		{
			Parent = parent;
			
			Types = new List<JavaType> ();
		}
		
		public JavaApi Parent { get; private set; }

		public string Name { get; set; }
		public IList<JavaType> Types { get; set; }
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return string.Format ("[Package] " + Name);
		}
	}

	public abstract partial class JavaType
	{
		protected JavaType (JavaPackage parent)
		{
			Parent = parent;
			
			Implements = new List<JavaImplements> ();
			Members = new List<JavaMember> ();
		}

		public JavaPackage Parent { get; private set; }

		public bool IsReferenceOnly { get; set; }

		public bool Abstract { get; set; }
		public string Deprecated { get; set; }
		public bool Final { get; set; }
		public string Name { get; set; }
		public bool Static { get; set; }
		public string Visibility { get; set; }

		public string ExtendedJniSignature { get; set; }

		public IList<JavaImplements> Implements { get; set; }
		public JavaTypeParameters TypeParameters { get; set; }
		public IList<JavaMember> Members { get; set; }

		public string FullName {
			get { return Parent.Name + (Parent.Name.Length > 0 ? "." : string.Empty) + Name; }
		}
		
		// Content of this value is not stable.
		public string ToStringHelper ()
		{
			// FIXME: add type attributes.
			return Parent.Name + "." + Name;
		}
	}

	public partial class JavaInterface : JavaType
	{
		public JavaInterface (JavaPackage parent)
			: base (parent)
		{
		}
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Interface] " + ToStringHelper ();
		}
	}

	public partial class JavaClass : JavaType
	{
		public JavaClass (JavaPackage parent)
			: base (parent)
		{
		}
		
		public string Extends { get; set; }
		public string ExtendsGeneric { get; set; }
		public string ExtendedJniExtends { get; set; }

		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Class] " + ToStringHelper ();
		}
	}


	class ManagedType : JavaType
	{
		static JavaPackage dummy_system_package, dummy_system_io_package;
		static JavaType system_object, system_exception, system_io_stream;

		static ManagedType ()
		{
			dummy_system_package = new JavaPackage (null) { Name = "System" };
			system_object = new ManagedType (dummy_system_package) { Name = "Object" };
			system_exception = new ManagedType (dummy_system_package) { Name = "Exception" };
			dummy_system_package.Types.Add (system_object);
			dummy_system_package.Types.Add (system_exception);
			dummy_system_io_package = new JavaPackage (null) { Name = "System.IO" };
			system_io_stream = new ManagedType (dummy_system_package) { Name = "Stream" };
			dummy_system_io_package.Types.Add (system_io_stream);
		}

		public static IEnumerable<JavaPackage> DummyManagedPackages {
			get {
				yield return dummy_system_package; 
				yield return dummy_system_io_package;
			}
		}

		public ManagedType (JavaPackage package) : base (package) 
		{
		}
	}


	public partial class JavaImplements
	{
		public string Name { get; set; }
		public string NameGeneric { get; set; }
		
		public string ExtendedJniType { get; set; }
	}

	public partial class JavaMember
	{
		protected JavaMember (JavaType parent)
		{
			Parent = parent;
		}
		
		public JavaType Parent { get; private set; }
		
		public string Deprecated { get; set; }
		public bool Final { get; set; }
		public string Name { get; set; }
		public bool Static { get; set; }
		public string Visibility { get; set; }
	}

	public partial class JavaField : JavaMember
	{
		public JavaField (JavaType parent)
			: base (parent)
		{
		}
		
		public bool Transient { get; set; }
		public string Type { get; set; }
		public string TypeGeneric { get; set; }
		public string Value { get; set; }
		public bool Volatile { get; set; }
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Field] " + TypeGeneric + " " + Name;
		}
	}

	public partial class JavaMethodBase : JavaMember
	{
		protected JavaMethodBase (JavaType parent)
			: base (parent)
		{
			Parameters = new List<JavaParameter> ();
			Exceptions = new List<JavaException> ();
		}

		public IList<JavaParameter> Parameters { get; set; }
		public IList<JavaException> Exceptions { get; set; }
		
		public bool ExtendedBridge { get; set; }
		public string ExtendedJniReturn { get; set; }
		public bool ExtendedSynthetic { get; set; }
		// We cannot get 'ExtendedJniSignature' from DLLs, so we don't have it here.

		// Content of this value is not stable.
		public string ToStringHelper (string returnType, string name, JavaTypeParameters typeParameters)
		{
			return string.Format ("{0}{1}{2}{3}{4}{5}({6})",
				returnType,
				returnType == null ? null : " ",
				Name,
				typeParameters == null ? null : "<",
				typeParameters == null ? null : string.Join (", ", typeParameters.TypeParameters),
				typeParameters == null ? null : ">",
				string.Join (", ", Parameters));
		}
	}

	public partial class JavaConstructor : JavaMethodBase
	{
		public JavaConstructor (JavaType parent)
			: base (parent)
		{
		}
		
		// it was required in the original API XML, but removed in class-parsed...
		public string Type { get; set; }
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Constructor] " + ToStringHelper (null, Parent.Name, null);
		}
	}

	public partial class JavaMethod : JavaMethodBase
	{
		public JavaMethod (JavaType parent)
			: base (parent)
		{
		}
		
		public bool Abstract { get; set; }
		public bool Native { get; set; }
		public string Return { get; set; }
		public bool Synchronized { get; set; }
		public JavaTypeParameters TypeParameters { get; set; }
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Method] " + ToStringHelper (Return, Name, TypeParameters);
		}
	}

	public partial class JavaParameter
	{
		public JavaParameter (JavaMethodBase parent)
		{
			Parent = parent;
		}

		public JavaMethodBase Parent { get; private set; }
		public string Name { get; set; }
		public string Type { get; set; }
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return Type + " " + Name;
		}
	}

	public partial class JavaException
	{
		public string Name { get; set; }
		public string Type { get; set; }
	}

	public partial class JavaTypeParameters
	{
		public JavaTypeParameters (JavaType parent)
		{
			ParentType = parent;
			TypeParameters = new List<JavaTypeParameter> ();
		}
		
		public JavaTypeParameters (JavaMethod parent)
		{
			ParentMethod = parent;
			TypeParameters = new List<JavaTypeParameter> ();
		}
		
		public JavaType ParentType { get; set; }
		public JavaMethod ParentMethod { get; set; }
		
		public IList<JavaTypeParameter> TypeParameters { get; set; }
	}

	public partial class JavaTypeParameter
	{
		public JavaTypeParameter (JavaTypeParameters parent)
		{
			Parent = parent;
		}
		
		public JavaTypeParameters Parent { get; set; }
		
		public string Name { get; set; }
		
		public string ExtendedJniClassBound { get; set; }
		public string ExtendedClassBound { get; set; }
		public string ExtendedInterfaceBounds { get; set; }
		public string ExtendedJniInterfaceBounds { get; set; }
		
		public JavaGenericConstraints GenericConstraints { get; set; }
		
		public override string ToString ()
		{
			return Name;
		}
	}

	public partial class JavaGenericConstraints
	{
		public JavaGenericConstraints ()
		{
			GenericConstraints = new List<JavaGenericConstraint> ();
		}
		
		public string BoundsType { get; set; } // extends / super
		
		public IList<JavaGenericConstraint> GenericConstraints { get; set; }
		
		public override string ToString ()
		{
			string csts = string.Join (" & ", GenericConstraints);
			if (csts == "java.lang.Object")
				return string.Empty;
			return " " + (BoundsType ?? "extends") + " " + csts;
		}
	}
	
	public partial class JavaGenericConstraint
	{
		public string Type { get; set; }
		
		public override string ToString ()
		{
			return Type;
		}
	}
}
