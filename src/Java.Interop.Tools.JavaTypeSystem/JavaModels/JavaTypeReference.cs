using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaTypeReference
	{
		public static readonly JavaTypeReference Void = new JavaTypeReference ("void");
		public static readonly JavaTypeReference Boolean = new JavaTypeReference ("boolean");
		public static readonly JavaTypeReference Char = new JavaTypeReference ("char");
		public static readonly JavaTypeReference Byte = new JavaTypeReference ("byte");
		public static readonly JavaTypeReference Short = new JavaTypeReference ("short");
		public static readonly JavaTypeReference Int = new JavaTypeReference ("int");
		public static readonly JavaTypeReference Long = new JavaTypeReference ("long");
		public static readonly JavaTypeReference Float = new JavaTypeReference ("float");
		public static readonly JavaTypeReference Double = new JavaTypeReference ("double");
		public static readonly JavaTypeReference GenericWildcard = new JavaTypeReference ("?");
		public static readonly JavaTypeReference UInt = new JavaTypeReference ("uint");
		public static readonly JavaTypeReference UShort = new JavaTypeReference ("ushort");
		public static readonly JavaTypeReference ULong = new JavaTypeReference ("ulong");
		public static readonly JavaTypeReference UByte = new JavaTypeReference ("ubyte");

		public string? SpecialName { get; private set; }
		public string? WildcardBoundsType { get; private set; }
		public IList<JavaTypeReference>? WildcardConstraints { get; private set; }
		public JavaTypeModel? ReferencedType { get; private set; }
		public JavaTypeParameter? ReferencedTypeParameter { get; private set; }
		public IList<JavaTypeReference>? TypeParameters { get; private set; }
		public string? ArrayPart { get; private set; }

		JavaTypeReference (string? specialName)
		{
			SpecialName = specialName;
		}

		public JavaTypeReference (string? constraintLabel, IEnumerable<JavaTypeReference>? wildcardConstraints, string? arrayPart)
		{
			SpecialName = GenericWildcard.SpecialName;
			ArrayPart = arrayPart;
			WildcardBoundsType = constraintLabel;
			WildcardConstraints = wildcardConstraints != null && wildcardConstraints.Any () ? wildcardConstraints.ToList () : null;
		}

		public JavaTypeReference (JavaTypeReference referencedType, string? arrayPart, string? wildcardBoundsType, IEnumerable<JavaTypeReference>? wildcardConstraints)
		{
			if (referencedType == null)
				throw new ArgumentNullException (nameof (referencedType));

			SpecialName = referencedType.SpecialName;
			WildcardBoundsType = wildcardBoundsType;
			WildcardConstraints = wildcardConstraints?.ToList ();
			ReferencedType = referencedType.ReferencedType;
			ReferencedTypeParameter = referencedType.ReferencedTypeParameter;
			TypeParameters = referencedType.TypeParameters;
			ArrayPart = arrayPart;
		}

		public JavaTypeReference (JavaTypeParameter referencedTypeParameter, string? arrayPart)
		{
			ReferencedTypeParameter = referencedTypeParameter ?? throw new ArgumentNullException (nameof (referencedTypeParameter));
			ArrayPart = arrayPart;
		}

		public JavaTypeReference (JavaTypeModel referencedType, IList<JavaTypeReference>? typeParameters, string? arrayPart)
		{
			ReferencedType = referencedType ?? throw new ArgumentNullException (nameof (referencedType));
			TypeParameters = typeParameters;
			ArrayPart = arrayPart;
		}

		internal static JavaTypeReference? GetSpecialType (string? name)
		{
			return name switch {
				"void" => Void,
				"boolean" => Boolean,
				"char" => Char,
				"byte" => Byte,
				"short" => Short,
				"int" => Int,
				"long" => Long,
				"float" => Float,
				"double" => Double,
				"uint" => UInt,
				"ushort" => UShort,
				"ulong" => ULong,
				"ubyte" => UByte,
				"?" => GenericWildcard,
				_ => null,
			};
		}

		public override string ToString ()
		{
			if (SpecialName == GenericWildcard.SpecialName && WildcardConstraints != null)
				return SpecialName + WildcardBoundsType + string.Join (" & ", WildcardConstraints);
			else if (SpecialName != null)
				return SpecialName + ArrayPart;
			else if (ReferencedTypeParameter != null)
				return ReferencedTypeParameter.Name + ArrayPart;
			else
				return string.Format ("{0}{1}{2}",
					ReferencedType?.FullName.Replace ('$', '.'),
					TypeParameters?.Any () == true ? '<' + string.Join (", ", TypeParameters.Select (_ => _.ToString ())) + '>' : null,
					ArrayPart);
		}

		public override int GetHashCode ()
		{
			// it's skipping TypeParameters because it's too annoying...
			if (SpecialName != null)
				return SpecialName.GetHashCode ();
			return (ReferencedType != null ? ReferencedType.Name?.GetHashCode () ?? 0 : 0) << 15 +
				(ReferencedTypeParameter != null ? ReferencedTypeParameter.Name?.GetHashCode () ?? 0 : 0) << 7 +
				(ArrayPart != null ? ArrayPart.GetHashCode () : 0);
		}

		public override bool Equals (object? obj)
		{
			return AreEqual (this, obj as JavaTypeReference);
		}

		// It compares two JavaTypeReferences.
		// Note that it is to compare them as a type reference, not as its object entity.
		// So, for example, if one has a TypeParameter with T with some contraint and
		// the other has a TypeParameter with T somehow without it, they are still "same".
		public static bool AreEqual (JavaTypeReference tr1, JavaTypeReference? tr2)
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
			} else if (tr2.ReferencedTypeParameter != null)
				return false;

			if (tr1.ReferencedType == null || tr2.ReferencedType == null)
				return false;
			if (tr1.ReferencedType.Package != tr2.ReferencedType.Package)
				return false;
			if (tr1.ReferencedType.NestedName != tr2.ReferencedType.NestedName)
				return false;
			return true;
		}
	}
}
