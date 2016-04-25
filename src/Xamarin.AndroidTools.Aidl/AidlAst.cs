using System;
using System.Linq;
using Irony.Parsing;

namespace Xamarin.AndroidTools.Aidl
{
	public class CompilationUnit
	{
		public CompilationUnit (TypeName package, TypeName [] imports, ITypeDeclaration [] types)
		{
			Package = package;
			Imports = imports;
			Types = types;
		}
		
		public TypeName Package { get; private set; }
		public TypeName [] Imports { get; private set; }
		public ITypeDeclaration [] Types { get; private set; }
	}
	
	public interface ITypeDeclaration
	{
	}
	
	public class Parcelable : ITypeDeclaration
	{
		public Parcelable (TypeName name)
		{
			Name = name;
		}
		
		public TypeName Name { get; private set; }
	}
	
	public class Interface : ITypeDeclaration
	{
		public Interface (string modifier, string name, Method [] methods)
		{
			Modifier = modifier;
			JavaName = name;
			Name = name [0] == 'I' ? name : 'I' + name;
			Methods = methods ?? new Method [0];
		}
		
		public string Modifier { get; private set; }
		public string JavaName { get; private set; }
		public string Name { get; private set; }
		public Method [] Methods { get; private set; }
	}
	
	public class Method
	{
		public Method (string modifier, TypeName returnType, string name, Argument [] args)
		{
			Modifier = modifier;
			ReturnType = returnType;
			JavaName = name;
			Name = Util.ToPascalCase (name);
			Arguments = args ?? new Argument [0];
		}

		public string Modifier { get; private set; }
		public TypeName ReturnType { get; private set; }
		public string JavaName { get; private set; }
		public string Name { get; private set; }
		public Argument [] Arguments { get; private set; }
	}
	
	public class Argument
	{
		public Argument (string modifier, TypeName type, string name)
		{
			Modifier = modifier;
			Type = type;
			Name = name;
		}
		
		public string Modifier { get; private set; }
		public TypeName Type { get; private set; }
		public string Name { get; private set; }
	}
	
	public class TypeName
	{
		public TypeName (string [] identifiers)
		{
			if (identifiers == null)
				throw new ArgumentNullException ("identifiers");
			if (identifiers.Any (i => i == null))
				throw new ArgumentException ("'identifiers' contain one or more null values: {0}", String.Concat (identifiers));
			this.Identifiers = identifiers;
		}
		
		public TypeName (string [] identifiers, TypeName [] genericArguments)
		{
			if (identifiers == null)
				throw new ArgumentNullException ("identifiers");
			if (identifiers.Any (i => i == null))
				throw new ArgumentException ("'identifiers' contain one or more null values: {0}", String.Concat (identifiers));
			if (genericArguments != null && genericArguments.Any (g => g == null))
				throw new ArgumentException (String.Format ("With {0} elementType, 'genericArguments' contain one or more null values", String.Concat (".", identifiers)));
			this.Identifiers = identifiers;
			this.GenericArguments = genericArguments;
		}
		
		public string [] Identifiers { get; private set; }
		//public TypeName ElementType { get; private set; }
		public TypeName [] GenericArguments { get; private set; }
		public int ArrayDimension { get; internal set; }
		
		public string GetNamespace ()
		{
			return GetFormattedIdentifiers (Identifiers.Length - 1);
		}
		
		public string GetPackage ()
		{
			return GetJavaIdentifiers (Identifiers.Length - 1);
		}

		public override string ToString ()
		{
			string baseName = GetFormattedIdentifiers (Identifiers.Length);
			if (GenericArguments != null)
				baseName += "<" + String.Join (",", (from g in GenericArguments select g.ToString ()).ToArray ()) + ">";
			for (int i = 0; i < ArrayDimension; i++)
				baseName += " []";
			return baseName;
		}
		
		public string ToJavaString ()
		{
			string baseName = GetJavaIdentifiers (Identifiers.Length);
			if (GenericArguments != null)
				baseName += "<" + String.Join (",", (from g in GenericArguments select g.ToJavaString ()).ToArray ()) + ">";
			for (int i = 0; i < ArrayDimension; i++)
				baseName += " []";
			return baseName;
		}
		
		string GetJavaIdentifiers (int count)
		{
			return String.Join (".", Identifiers.Take (count));
		}
		
		string GetFormattedIdentifiers (int count)
		{
			return String.Join (".", (from n in Identifiers.Take (count) select Util.ToPascalCase (n)).ToArray ());
		}
	}
	
	static class Util
	{
		public static string ToPascalCase (string s)
		{
			switch (s) {
			case "int":
			case "string":
			case "void":
			case "long":
			case "float":
			case "double":
			case "byte":
			case "char":
				return s;
			}
			return Char.ToUpper (s [0]) + s.Substring (1);
		}
	}
}
