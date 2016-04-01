using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public class JavaTypeReference
	{
		public static readonly JavaTypeReference Void;
		public static readonly JavaTypeReference Boolean;
		public static readonly JavaTypeReference Char;
		public static readonly JavaTypeReference Byte;
		public static readonly JavaTypeReference Short;
		public static readonly JavaTypeReference Int;
		public static readonly JavaTypeReference Long;
		public static readonly JavaTypeReference Float;
		public static readonly JavaTypeReference Double;
		public static readonly JavaTypeReference GenericWildcard;
		
		internal static JavaTypeReference GetSpecialType (string name)
		{
			switch (name) {
			case "void": return Void;
			case "boolean": return Boolean;
			case "char": return Char;
			case "byte": return Byte;
			case "short": return Short;
			case "int": return Int;
			case "long": return Long;
			case "float": return Float;
			case "double": return Double;
			case "?": return GenericWildcard;
			}
			return null;
		}

		static JavaTypeReference ()
		{
			Void = new JavaTypeReference ("void");
			Boolean = new JavaTypeReference ("boolean");
			Char = new JavaTypeReference ("char");
			Byte = new JavaTypeReference ("byte");
			Short = new JavaTypeReference ("short");
			Int = new JavaTypeReference ("int");
			Long = new JavaTypeReference ("long");
			Float = new JavaTypeReference ("float");
			Double = new JavaTypeReference ("double");
			GenericWildcard = new JavaTypeReference ("?");
		}
		
		public JavaTypeReference (string specialName)
		{
			SpecialName = specialName;
		}
		
		public JavaTypeReference (JavaTypeReference referencedType, string arrayPart)
		{
			if (referencedType == null)
				throw new ArgumentNullException ("referencedType");
			SpecialName = referencedType.SpecialName;
			ReferencedType = referencedType.ReferencedType;
			TypeParameters = referencedType.TypeParameters;
			ArrayPart = arrayPart;
		}
		
		public JavaTypeReference (JavaTypeParameter referencedTypeParameter, string arrayPart)
		{
			if (referencedTypeParameter == null)
				throw new ArgumentNullException ("referencedTypeParameter");
			ReferencedTypeParameter = referencedTypeParameter;
			ArrayPart = arrayPart;
		}
		
		public JavaTypeReference (JavaType referencedType, IList<JavaTypeReference> typeParameters, string arrayPart)
		{
			if (referencedType == null)
				throw new ArgumentNullException ("referencedType");
			ReferencedType = referencedType;
			TypeParameters = typeParameters;
			ArrayPart = arrayPart;
		}
		
		public string SpecialName { get; private set; }
		public JavaType ReferencedType { get; private set; }
		public JavaTypeParameter ReferencedTypeParameter { get; private set; }
		public IList<JavaTypeReference> TypeParameters { get; private set; }
		public string ArrayPart { get; private set; }
		
		public override string ToString ()
		{
			if (SpecialName != null)
				return SpecialName;
			else if (ReferencedTypeParameter != null)
				return ReferencedTypeParameter.Name;
			else
				return string.Format ("{0}{1}{2}{3}{4}",
					ReferencedType.Parent.Name,
					string.IsNullOrEmpty (ReferencedType.Parent.Name) ? string.Empty : ".",
					ReferencedType.Name,
					TypeParameters == null ? null : '<' + string.Join (", ", TypeParameters.Select (_ => _.ToString ())) + '>',
					ArrayPart);
		}
		
		public override int GetHashCode ()
		{
			// it's skipping TypeParameters because it's too annoying...
			if (SpecialName != null)
				return SpecialName.GetHashCode ();
			return  (ReferencedType != null ? ReferencedType.Name.GetHashCode () : 0) << 15 +
				(ReferencedTypeParameter != null ? ReferencedTypeParameter.Name.GetHashCode () : 0) << 7 +
				(ArrayPart != null ? ArrayPart.GetHashCode () : 0);
		}
		
		public override bool Equals (object obj)
		{
			return AreEqual (this, obj as JavaTypeReference);
		}
		
		// It compares two JavaTypeReferences.
		// Note that it is to compare them as a type reference, not as its object entity.
		// So, for example, if one has a TypeParameter with T with some contraint and
		// the other has a TypeParameter with T somehow without it, they are still "same".
		public static bool AreEqual (JavaTypeReference tr1, JavaTypeReference tr2)
		{
			if (tr1 == null)
				return tr2 == null;
			else if (tr2 == null)
				return false;

			if (tr1.ArrayPart != tr2.ArrayPart)
				return false;
			
			if (tr1.SpecialName != null)
				return tr1.SpecialName == tr2.SpecialName;
			
			if (tr1.ReferencedTypeParameter != null) {
				if (tr2.ReferencedTypeParameter == null || tr1.ReferencedTypeParameter.Name != tr2.ReferencedTypeParameter.Name)
					return false;
				
				return true;
			}
			else if (tr2.ReferencedTypeParameter != null)
				return false;
			
			if (tr1.ReferencedType == null || tr2.ReferencedType == null)
				return false;
			if (tr1.ReferencedType.Parent != tr2.ReferencedType.Parent)
				return false;
			if (tr1.ReferencedType.Name != tr2.ReferencedType.Name)
				return false;
			return true;
		}
	}
}
