using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public partial class JavaApi
	{
		public JavaApi ()
		{
			Packages = new Dictionary<string, JavaPackage> ();
		}

		public  string?                 ExtendedApiSource   { get; set; }
		public  string?                 Platform            { get; set; }
		public  IDictionary<string, JavaPackage>      Packages            { get; }

		public ICollection<JavaPackage> AllPackages => Packages.Values;
	}

	public partial class JavaPackage
	{
		private Dictionary<string, List<JavaType>> types = new Dictionary<string, List<JavaType>> ();

		public JavaPackage (JavaApi? parent)
		{
			Parent = parent;
		}
		
		public  JavaApi?                Parent              { get; private set; }

		public  string?                 Name                { get; set; }
		public  string?                 JniName             { get; set; }

		// Yes, there can be multiple types with the same *Java* name.
		// For example:
		// - MyInterface
		// - MyInterfaceConsts
		// It's debatable whether we handle this "properly", as most callers just
		// do `First ()`, but it's been working for years so I'm not changing it.
		// Exposes an IReadOnlyDictionary so caller cannot bypass our AddType/RemoveType code.
		public  IReadOnlyDictionary<string, List<JavaType>>   Types => types;

		// Use this for a flat list of *all* types
		public IEnumerable<JavaType> AllTypes => Types.Values.SelectMany (v => v);

		public void AddType (JavaType type)
		{
			// If this is a duplicate key, add it to existing list
			if (Types.TryGetValue (type.Name!, out var list)) {
				list.Add (type);
				return;
			}

			// Add to a new list
			var new_list = new List<JavaType> ();
			new_list.Add (type);

			types.Add (type.Name!, new_list);
		}

		public void RemoveType (JavaType type)
		{
			if (!Types.TryGetValue (type.Name!, out var list))
				return;

			// Remove 1 type from list if it contains multiple types
			if (list.Count > 1) {
				list.Remove (type);
				return;
			}

			// Remove the whole dictionary entry
			types.Remove (type.Name!);
		}

		public void ClearTypes ()
		{
			types.Clear ();
		}

		// Content of this value is not stable.
		public override string ToString ()
		{
			return string.Format ("[Package] " + Name);
		}
	}

	public abstract partial class JavaType
	{
		protected JavaType (JavaPackage? parent)
		{
			Parent = parent;
			
			Implements = new List<JavaImplements> ();
			Members = new List<JavaMember> ();
		}

		public  JavaPackage?            Parent                  { get; private set; }

		public  bool                    IsReferenceOnly         { get; set; }

		public  bool                    Abstract                { get; set; }
		public  string?                 Deprecated              { get; set; }
		public  bool                    Final                   { get; set; }
		public  string?                 Name                    { get; set; }
		public  bool                    Static                  { get; set; }
		public  string?                 Visibility              { get; set; }

		public  string?                 ExtendedJniSignature    { get; set; }

		public  IList<JavaImplements>   Implements              { get; set; }
		public  JavaTypeParameters?     TypeParameters          { get; set; }
		public  IList<JavaMember>       Members                 { get; set; }

		public string FullName {
			get { return Parent?.Name + ((Parent?.Name?.Length ?? 0) > 0 ? "." : string.Empty) + Name; }
		}
		
		// Content of this value is not stable.
		public string ToStringHelper ()
		{
			// FIXME: add type attributes.
			return (Parent?.Name == null ? "" : Parent.Name + ".") +
				(Name ?? "");
		}
	}

	public partial class JavaInterface : JavaType
	{
		public JavaInterface (JavaPackage? parent)
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
		public JavaClass (JavaPackage? parent)
			: base (parent)
		{
		}
		
		public  string?                 Extends             { get; set; }
		public  string?                 ExtendsGeneric      { get; set; }
		public  string?                 ExtendedJniExtends  { get; set; }

		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Class] " + ToStringHelper ();
		}
	}


	class ManagedType : JavaType
	{
		static JavaPackage dummy_system_package, dummy_system_io_package, dummy_system_xml_package;
		static JavaType system_object, system_exception, system_io_stream, system_xml_xmlreader;

		static ManagedType ()
		{
			dummy_system_package = new JavaPackage (null) { Name = "System" };
			system_object = new ManagedType (dummy_system_package) { Name = "Object" };
			system_exception = new ManagedType (dummy_system_package) { Name = "Exception" };
			dummy_system_package.AddType (system_object);
			dummy_system_package.AddType (system_exception);
			dummy_system_io_package = new JavaPackage (null) { Name = "System.IO" };
			system_io_stream = new ManagedType (dummy_system_io_package) { Name = "Stream" };
			dummy_system_io_package.AddType (system_io_stream);
			dummy_system_xml_package = new JavaPackage (null) { Name = "System.Xml" };
			system_xml_xmlreader = new ManagedType (dummy_system_xml_package) { Name = "XmlReader" };
			dummy_system_io_package.AddType (system_xml_xmlreader);
		}

		public static IEnumerable<JavaPackage> DummyManagedPackages {
			get {
				yield return dummy_system_package; 
				yield return dummy_system_io_package;
				yield return dummy_system_xml_package;
			}
		}

		public ManagedType (JavaPackage package) : base (package) 
		{
		}
	}


	public partial class JavaImplements
	{
		public  string?                 Name                { get; set; }
		public  string?                 NameGeneric         { get; set; }

		public  string?                 ExtendedJniType     { get; set; }
	}

	public partial class JavaMember
	{
		protected JavaMember (JavaType? parent)
		{
			Parent = parent;
		}
		
		public  JavaType?               Parent                  { get; private set; }

		public  string?                 Deprecated              { get; set; }
		public  bool                    Final                   { get; set; }
		public  string?                 Name                    { get; set; }
		public  bool                    Static                  { get; set; }
		public  string?                 Visibility              { get; set; }
		public  string?                 ExtendedJniSignature    { get; set; }
	}

	public partial class JavaField : JavaMember
	{
		public JavaField (JavaType? parent)
			: base (parent)
		{
		}

		public  bool                    NotNull                 { get; set; }
		public  bool                    Transient               { get; set; }
		public  string?                 Type                    { get; set; }
		public  string?                 TypeGeneric             { get; set; }
		public  string?                 Value                   { get; set; }
		public  bool                    Volatile                { get; set; }
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Field] " + TypeGeneric + " " + Name;
		}
	}

	public partial class JavaMethodBase : JavaMember
	{
		protected JavaMethodBase (JavaType? parent)
			: base (parent)
		{
			Parameters = new List<JavaParameter> ();
		}

		IList<JavaException>? exceptions;

		public  IList<JavaParameter>    Parameters          { get; set; }
		public  JavaTypeParameters?     TypeParameters      { get; set; }
		
		public  bool                    ExtendedBridge      { get; set; }
		public  string?                 ExtendedJniReturn   { get; set; }
		public  bool                    ExtendedSynthetic   { get; set; }

		[NotNull]
		public  IList<JavaException>?   Exceptions          {
			get => exceptions ?? (exceptions = new List<JavaException>());
			set => exceptions = value;
		}

		// Content of this value is not stable.
		public string ToStringHelper (string? returnType, string? name, JavaTypeParameters? typeParameters)
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
		public JavaConstructor (JavaType? parent)
			: base (parent)
		{
		}
		
		// it was required in the original API XML, but removed in class-parsed...
		public  string?                 Type                { get; set; }
		
		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Constructor] " + ToStringHelper (null, Parent?.Name, null);
		}
	}

	public partial class JavaMethod : JavaMethodBase
	{
		public JavaMethod (JavaType? parent)
			: base (parent)
		{
		}
		
		public  bool                    Abstract            { get; set; }
		public  bool                    Native              { get; set; }
		public  string?                 Return              { get; set; }
		public  bool                    ReturnNotNull       { get; set; }
		public  bool                    Synchronized        { get; set; }

		// Content of this value is not stable.
		public override string ToString ()
		{
			return "[Method] " + ToStringHelper (Return, Name, TypeParameters);
		}
	}

	public partial class JavaParameter
	{
		public JavaParameter (JavaMethodBase? parent)
		{
			Parent = parent;
		}

		public  JavaMethodBase?         Parent              { get; private set; }
		public  string?                 Name                { get; set; }
		public  string?                 Type                { get; set; }
		public  string?                 JniType             { get; set; }
		public  bool                    NotNull             { get; set; }

		// Content of this value is not stable.
		public override string ToString ()
		{
			return Type + " " + Name;
		}
	}

	public partial class JavaException
	{
		public  string?                 Name                { get; set; }
		public  string?                 Type                { get; set; }
		public  string?                 TypeGenericAware    { get; set; }
	}

	public partial class JavaTypeParameters
	{
		public JavaTypeParameters (JavaType parent)
		{
			ParentType = parent;
			TypeParameters = new List<JavaTypeParameter> ();
		}
		
		public JavaTypeParameters (JavaMethodBase? parent)
		{
			ParentMethod = parent;
			TypeParameters = new List<JavaTypeParameter> ();
		}
		
		public  JavaType?                   ParentType      { get; set; }
		public  JavaMethodBase?             ParentMethod    { get; set; }
		
		public  IList<JavaTypeParameter>    TypeParameters  { get; set; }
	}

	public partial class JavaTypeParameter
	{
		public JavaTypeParameter (JavaTypeParameters? parent)
		{
			Parent = parent;
		}
		
		public  JavaTypeParameters?     Parent                      { get; set; }
		
		public  string?                 Name                        { get; set; }

		public  string?                 ExtendedJniClassBound       { get; set; }
		public  string?                 ExtendedClassBound          { get; set; }
		public  string?                 ExtendedInterfaceBounds     { get; set; }
		public  string?                 ExtendedJniInterfaceBounds  { get; set; }
		
		public  JavaGenericConstraints? GenericConstraints          { get; set; }
		
		public override string ToString ()
		{
			return Name ?? "";
		}
	}

	public partial class JavaGenericConstraints
	{
		public JavaGenericConstraints ()
		{
			GenericConstraints = new List<JavaGenericConstraint> ();
		}
		
		public  string?                         BoundsType          { get; set; } // extends / super
		
		IList<JavaGenericConstraint>?   genericConstraints;

		[NotNull]
		public  IList<JavaGenericConstraint>?   GenericConstraints  {
			get => genericConstraints ?? (genericConstraints = new List<JavaGenericConstraint> ());
			set => genericConstraints = value;
		}
		
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
		public  string?                         Type                { get; set; }
		
		public override string ToString ()
		{
			return Type ?? "";
		}
	}
}
