using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace MonoDroid.Generation
{
	public abstract class GenBaseSupport
	{
		public abstract bool IsAcw { get; }
		public abstract bool IsDeprecated { get; }
		public abstract string DeprecatedComment { get; }
		public abstract bool IsGeneratable { get; }
		public abstract bool IsGeneric { get; }
		public abstract bool IsObfuscated { get; }
		public abstract string FullName { get; set; }
		public abstract string Name { get; set; }
		public abstract string Namespace { get; }
		public abstract string JavaSimpleName { get; }
		public abstract string PackageName { get; set; }
		//public abstract string Marshaler { get; }
		public abstract string Visibility { get; }
		public abstract GenericParameterDefinitionList TypeParameters { get; }

		public virtual string TypeNamePrefix {
			get { return String.Empty; }
		}
		
		public virtual bool OnValidate (CodeGenerationOptions opt)
		{
			// See com.google.inject.internal.util package for this case.
			// Some Java compiler-generated internals are named as $foobar (dollar prefixed).
			// Since our jar2xml replaces all '$' with '.', it results in ".." namespace.
			if (this.FullName.Contains (".."))
				return false;
			return true;
		}

		public static bool IsPrefixableName (string name)
		{
			// IBlahBlah is not prefixed with 'I'
			return name.Length <= 2 || name [0] != 'I' || !Char.IsUpper (name [1]);
		}
	}
	
	public class InterfaceXmlGenBaseSupport : XmlGenBaseSupport
	{
		public InterfaceXmlGenBaseSupport (XElement pkg, XElement elem)
			: base (pkg, elem)
		{
		}
		
		public override string TypeNamePrefix {
			get { return (IsPrefixableName (RawName) ? "I" : string.Empty); }
		}
	}
}


