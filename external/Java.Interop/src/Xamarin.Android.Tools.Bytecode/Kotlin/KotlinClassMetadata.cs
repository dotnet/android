using System;
using System.Collections.Generic;
using System.Linq;
using org.jetbrains.kotlin.metadata.jvm;
using Type = org.jetbrains.kotlin.metadata.jvm.Type;

namespace Xamarin.Android.Tools.Bytecode
{
	public class KotlinClass
	{
		public string CompanionObjectName { get; set; }
		public List<KotlinConstructor> Constructors { get; set; }
		public List<string> EnumEntries { get; set; }
		public KotlinClassFlags Flags { get; set; }
		public string FullyQualifiedName { get; set; }
		public List<KotlinFunction> Functions { get; set; }
		public List<string> NestedClassNames { get; set; } = new List<string> ();
		public List<KotlinProperty> Properties { get; set; }
		public List<string> SealedSubclassFullyQualifiedNames { get; set; }
		public List<string> SuperTypeIds { get; set; }
		public List<KotlinType> SuperTypes { get; set; }
		public List<KotlinTypeAlias> TypeAliases { get; set; }
		public List<KotlinTypeParameter> TypeParameters { get; set; }
		public KotlinTypeTable TypeTable { get; set; }
		public int [] VersionRequirements { get; set; }
		public KotlinVersionRequirementTable VersionRequirementTable { get; set; }

		internal static KotlinClass FromProtobuf (Class c, JvmNameResolver resolver)
		{
			return new KotlinClass {
				CompanionObjectName = c.CompanionObjectName > 0 ? resolver.GetString (c.CompanionObjectName) : null,
				Constructors = c.Constructors?.Select (ctor => KotlinConstructor.FromProtobuf (ctor, resolver)).ToList (),
				EnumEntries = c.EnumEntries?.Select (e => resolver.GetString (e.Name)).ToList (),
				Flags = (KotlinClassFlags)c.Flags,
				FullyQualifiedName = c.FqName > 0 ? resolver.GetString (c.FqName) : null,
				Functions = c.Functions?.Select (f => KotlinFunction.FromProtobuf (f, resolver)).ToList (),
				NestedClassNames = c.NestedClassNames?.Select (n => resolver.GetString (n)).ToList (),
				Properties = c.Properties?.Select (p => KotlinProperty.FromProtobuf (p, resolver)).ToList (),
				SealedSubclassFullyQualifiedNames = c.SealedSubclassFqNames?.Select (n => resolver.GetString (n)).ToList (),
				SuperTypeIds = c.SupertypeIds?.Select (n => resolver.GetString (n)).ToList (),
				SuperTypes = c.Supertypes?.Select (st => KotlinType.FromProtobuf (st, resolver)).ToList (),
				TypeAliases = c.TypeAlias?.Select (tp => KotlinTypeAlias.FromProtobuf (tp, resolver)).ToList (),
				TypeParameters = c.TypeParameters?.Select (tp => KotlinTypeParameter.FromProtobuf (tp, resolver)).ToList (),
				VersionRequirements = c.VersionRequirements,
				TypeTable = KotlinTypeTable.FromProtobuf (c.TypeTable, resolver),
				VersionRequirementTable = KotlinVersionRequirementTable.FromProtobuf (c.VersionRequirementTable, resolver)
			};
		}
	}

	public class KotlinConstructor
	{
		public int Flags { get; set; }
		public List<KotlinValueParameter> ValueParameters { get; set; }
		public int [] VersionRequirements { get; set; }

		internal static KotlinConstructor FromProtobuf (Constructor c, JvmNameResolver resolver)
		{
			if (c is null)
				return null;

			return new KotlinConstructor {
				Flags = c.Flags,
				ValueParameters = c.ValueParameters?.Select (vp => KotlinValueParameter.FromProtobuf (vp, resolver)).ToList (),
				VersionRequirements = c.VersionRequirements
			};
		}
	}

	public class KotlinAnnotation
	{
		public int Id { get; set; }
		public List<KotlinAnnotationArgument> Arguments { get; set; }

		internal static KotlinAnnotation FromProtobuf (org.jetbrains.kotlin.metadata.jvm.Annotation a, JvmNameResolver resolver)
		{
			if (a is null)
				return null;

			return new KotlinAnnotation {
				Id = a.Id,
				Arguments = a.Arguments?.Select (vp => KotlinAnnotationArgument.FromProtobuf (vp, resolver)).ToList ()
			};
		}
	}

	public class KotlinAnnotationArgument
	{
		public int NameId { get; set; }
		public KotlinAnnotationArgumentValue Value { get; set; }

		internal static KotlinAnnotationArgument FromProtobuf (org.jetbrains.kotlin.metadata.jvm.Annotation.Argument a, JvmNameResolver resolver)
		{
			if (a is null)
				return null;

			return new KotlinAnnotationArgument {
				NameId = a.NameId,
				Value = KotlinAnnotationArgumentValue.FromProtobuf (a.value, resolver)
			};
		}
	}

	public class KotlinAnnotationArgumentValue
	{
		public KotlinAnnotationArgumentType Type { get; set; }
		public long IntValue { get; set; }
		public float FloatValue { get; set; }
		public double DoubleValue { get; set; }
		public string StringValue { get; set; }
		public int ClassId { get; set; }
		public int EnumValueId { get; set; }
		public KotlinAnnotation Annotation { get; set; }
		public List<KotlinAnnotationArgumentValue> ArrayElements { get; set; }
		public int ArrayDimensionCount { get; set; }
		public int Flags { get; set; }

		internal static KotlinAnnotationArgumentValue FromProtobuf (org.jetbrains.kotlin.metadata.jvm.Annotation.Argument.Value value, JvmNameResolver resolver)
		{
			if (value is null)
				return null;

			return new KotlinAnnotationArgumentValue {
				Type = (KotlinAnnotationArgumentType) value.type,
				IntValue = value.IntValue,
				FloatValue = value.FloatValue,
				DoubleValue = value.DoubleValue,
				StringValue = resolver.GetString (value.StringValue),
				ClassId = value.ClassId,
				EnumValueId = value.EnumValueId,
				Annotation = KotlinAnnotation.FromProtobuf (value.Annotation, resolver),
				ArrayDimensionCount = value.ArrayDimensionCount,
				ArrayElements = value.ArrayElements?.Select (vp => KotlinAnnotationArgumentValue.FromProtobuf (vp, resolver)).ToList (),
				Flags = value.Flags
			};
		}
	}

	public class KotlinEffect
	{
		public KotlinEffectType EffectType { get; set; }
		public List<KotlinExpression> EffectConstructorArguments { get; set; }
		public KotlinExpression ConclusionOfConditionalEffect { get; set; }
		public KotlinInvocationKind Kind { get; set; }

		internal static KotlinEffect FromProtobuf (Effect ef, JvmNameResolver resolver)
		{
			if (ef is null)
				return null;

			return new KotlinEffect {
				EffectType = (KotlinEffectType) ef.effect_type,
				EffectConstructorArguments = ef.EffectConstructorArguments?.Select (vp => KotlinExpression.FromProtobuf (vp, resolver)).ToList (),
				ConclusionOfConditionalEffect = KotlinExpression.FromProtobuf (ef.ConclusionOfConditionalEffect, resolver),
				Kind = (KotlinInvocationKind) ef.Kind
			};
		}
	}

	public class KotlinExpression
	{
		public int Flags { get; set; }
		public int ValueParameterReference { get; set; }
		public KotlinConstantValue ConstantValue { get; set; }
		public KotlinType IsInstanceType { get; set; }
		public int IsInstanceTypeId { get; set; }
		public List<KotlinExpression> AndArguments { get; set; }
		public List<KotlinExpression> OrArguments { get; set; }

		internal static KotlinExpression FromProtobuf (Expression exp, JvmNameResolver resolver)
		{
			if (exp is null)
				return null;

			return new KotlinExpression {
				Flags = exp.Flags,
				ValueParameterReference = exp.ValueParameterReference,
				ConstantValue = (KotlinConstantValue) exp.constant_value,
				IsInstanceType = KotlinType.FromProtobuf (exp.IsInstanceType, resolver),
				IsInstanceTypeId = exp.IsInstanceTypeId,
				AndArguments = exp.AndArguments?.Select (tp => KotlinExpression.FromProtobuf (tp, resolver)).ToList (),
				OrArguments = exp.OrArguments?.Select (vp => KotlinExpression.FromProtobuf (vp, resolver)).ToList ()
			};
		}
	}

	public class KotlinFunction
	{
		public int Flags { get; set; }
		public string Name { get; set; }
		public KotlinType ReturnType { get; set; }
		public int ReturnTypeId { get; set; }
		public List<KotlinTypeParameter> TypeParameters { get; set; }
		public KotlinType ReceiverType { get; set; }
		public int ReceiverTypeId { get; set; }
		public List<KotlinValueParameter> ValueParameters { get; set; }
		public KotlinTypeTable TypeTable { get; set; }
		public int [] VersionRequirements { get; set; }
		public KotlinContract Contract { get; set; }

		internal static KotlinFunction FromProtobuf (Function f, JvmNameResolver resolver)
		{
			if (f is null)
				return null;

			return new KotlinFunction {
				Flags = f.Flags,
				Name = resolver.GetString (f.Name),
				ReturnType = KotlinType.FromProtobuf (f.ReturnType, resolver),
				ReturnTypeId = f.ReturnTypeId,
				ReceiverType = KotlinType.FromProtobuf (f.ReceiverType, resolver),
				ReceiverTypeId = f.ReceiverTypeId,
				TypeParameters = f.TypeParameters?.Select (tp => KotlinTypeParameter.FromProtobuf (tp, resolver)).ToList (),
				ValueParameters = f.ValueParameters?.Select (vp => KotlinValueParameter.FromProtobuf (vp, resolver)).ToList (),
				VersionRequirements = f.VersionRequirements
			};
		}
	}

	public class KotlinContract
	{
		public List<KotlinEffect> Effects { get; set; }

		internal static KotlinContract FromProtobuf (Contract c, JvmNameResolver resolver)
		{
			return new KotlinContract {
				Effects = c.Effects?.Select (tp => KotlinEffect.FromProtobuf (tp, resolver)).ToList ()
			};
		}
	}

	public class KotlinProperty
	{
		public int Flags { get; set; }
		public string Name { get; set; }
		public KotlinType ReturnType { get; set; }
		public int ReturnTypeId { get; set; }
		public List<KotlinTypeParameter> TypeParameters { get; set; }
		public KotlinType ReceiverType { get; set; }
		public int ReceiverTypeId { get; set; }
		public KotlinValueParameter SetterValueParameter { get; set; }
		public int GetterFlags { get; set; }
		public int SetterFlags { get; set; }
		public int [] VersionRequirements { get; set; }

		internal static KotlinProperty FromProtobuf (Property p, JvmNameResolver resolver)
		{
			if (p is null)
				return null;

			return new KotlinProperty {
				Flags = p.Flags,
				Name = resolver.GetString (p.Name),
				ReturnTypeId = p.ReturnTypeId,
				ReturnType = KotlinType.FromProtobuf (p.ReturnType, resolver),
				ReceiverType = KotlinType.FromProtobuf (p.ReceiverType, resolver),
				ReceiverTypeId = p.ReceiverTypeId,
				SetterValueParameter = KotlinValueParameter.FromProtobuf (p.SetterValueParameter, resolver),
				GetterFlags = p.GetterFlags,
				SetterFlags = p.SetterFlags,
				TypeParameters = p.TypeParameters?.Select (tp => KotlinTypeParameter.FromProtobuf (tp, resolver)).ToList (),
				VersionRequirements = p.VersionRequirements
			};
		}
	}

	public class KotlinType
	{
		public List<KotlinTypeArgument> Arguments { get; set; }
		public bool Nullable { get; set; }
		public int FlexibleTypeCapabilitiesId { get; set; }
		public KotlinType FlexibleUpperBound { get; set; }
		public int FlexibleUpperBoundId { get; set; }
		public string ClassName { get; set; }
		public int TypeParameter { get; set; }
		public string TypeParameterName { get; set; }
		public string TypeAliasName { get; set; }
		public KotlinType OuterType { get; set; }
		public int OuterTypeId { get; set; }
		public KotlinType AbbreviatedType { get; set; }
		public int AbbreviatedTypeId { get; set; }
		public int Flags { get; set; }

		internal static KotlinType FromProtobuf (Type t, JvmNameResolver resolver)
		{
			if (t is null)
				return null;

			return new KotlinType {
				Arguments = t.Arguments?.Select (a => KotlinTypeArgument.FromProtobuf (a, resolver)).ToList (),
				Nullable = t.Nullable,
				FlexibleTypeCapabilitiesId = t.FlexibleTypeCapabilitiesId,
				FlexibleUpperBound = FromProtobuf (t.FlexibleUpperBound, resolver),
				ClassName = t.ClassName > 0 ? resolver.GetString (t.ClassName) : null,
				TypeParameter = t.TypeParameter,
				TypeParameterName = t.TypeParameterName > 0 ? resolver.GetString (t.TypeParameterName) : null,
				OuterType = FromProtobuf (t.OuterType, resolver),
				OuterTypeId = t.OuterTypeId,
				AbbreviatedType = FromProtobuf (t.AbbreviatedType, resolver),
				AbbreviatedTypeId = t.AbbreviatedTypeId,
				Flags = t.Flags
			};
		}
	}

	public class KotlinTypeAlias
	{
		public int Flags { get; set; }
		public string Name { get; set; }
		public List<KotlinTypeParameter> TypeParameters { get; set; }
		public KotlinType UnderlyingType { get; set; }
		public int UnderlyingTypeId { get; set; }
		public KotlinType ExpandedType { get; set; }
		public int ExpandedTypeId { get; set; }
		public List<Annotation> Annotations { get; set; }
		public int [] VersionRequirements { get; set; }

		internal static KotlinTypeAlias FromProtobuf (TypeAlias ta, JvmNameResolver resolver)
		{
			if (ta is null)
				return null;

			return new KotlinTypeAlias {
				Flags = ta.Flags,
				Name = resolver.GetString (ta.Name),
				TypeParameters = ta.TypeParameters?.Select (tp => KotlinTypeParameter.FromProtobuf (tp, resolver)).ToList (),
				UnderlyingType = KotlinType.FromProtobuf (ta.UnderlyingType, resolver),
				UnderlyingTypeId = ta.UnderlyingTypeId,
				ExpandedType = KotlinType.FromProtobuf (ta.ExpandedType, resolver),
				ExpandedTypeId = ta.ExpandedTypeId,
				VersionRequirements = ta.VersionRequirements
			};
		}
	}

	public class KotlinTypeArgument
	{
		public KotlinProjection Projection { get; set; }
		public KotlinType Type { get; set; }
		public int TypeId { get; set; }

		internal static KotlinTypeArgument FromProtobuf (Type.Argument ta, JvmNameResolver resolver)
		{
			if (ta is null)
				return null;

			return new KotlinTypeArgument {
				Projection = (KotlinProjection) ta.projection,
				Type = KotlinType.FromProtobuf (ta.Type, resolver),
				TypeId = ta.TypeId
			};
		}
	}

	public class KotlinTypeParameter
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public bool Reified { get; set; }
		public KotlinVariance Variance { get; set; }
		public List<KotlinType> UpperBounds { get; set; }
		public int [] UpperBoundsIds { get; set; }

		internal static KotlinTypeParameter FromProtobuf (TypeParameter vp, JvmNameResolver resolver)
		{
			if (vp is null)
				return null;

			return new KotlinTypeParameter {
				Id = vp.Id,
				Name = resolver.GetString (vp.Name),
				Reified = vp.Reified,
				Variance = (KotlinVariance) vp.variance,
				UpperBounds = vp.UpperBounds?.Select (ub => KotlinType.FromProtobuf (ub, resolver)).ToList (),
				UpperBoundsIds = vp.UpperBoundIds
			};
		}
	}

	public class KotlinTypeTable
	{
		public List<KotlinType> Types { get; set; }
		public int FirstNullable { get; set; }

		internal static KotlinTypeTable FromProtobuf (TypeTable ta, JvmNameResolver resolver)
		{
			if (ta is null)
				return null;

			return new KotlinTypeTable {
				Types = ta.Types?.Select (t => KotlinType.FromProtobuf (t, resolver)).ToList (),
				FirstNullable = ta.FirstNullable
			};
		}
	}

	public class KotlinVersionRequirement
	{
		public int Version { get; set; }
		public int VersionFull { get; set; }
		public KotlinVersionLevel Level { get; set; }
		public int ErrorCode { get; set; }
		public int Message { get; set; }
		public KotlinVersionKind VersionKind { get; set; }

		internal static KotlinVersionRequirement FromProtobuf (VersionRequirement vr, JvmNameResolver resolver)
		{
			if (vr is null)
				return null;

			return new KotlinVersionRequirement {
				Version = vr.Version,
				VersionFull = vr.VersionFull,
				Level = (KotlinVersionLevel) vr.level,
				ErrorCode = vr.ErrorCode,
				Message = vr.Message,
				VersionKind = (KotlinVersionKind) vr.version_kind
			};
		}
	}

	public class KotlinVersionRequirementTable
	{
		public List<KotlinVersionRequirement> Requirements { get; set; }

		internal static KotlinVersionRequirementTable FromProtobuf (VersionRequirementTable vrt, JvmNameResolver resolver)
		{
			if (vrt is null)
				return null;

			return new KotlinVersionRequirementTable {
				Requirements = vrt.Requirements?.Select (t => KotlinVersionRequirement.FromProtobuf (t, resolver)).ToList ()
			};
		}
	}

	public class KotlinValueParameter
	{
		public int Flags { get; set; }
		public string Name { get; set; }
		public KotlinType Type { get; set; }
		public int TypeId { get; set; }
		public KotlinType VarArgElementType { get; set; }
		public int VarArgElementTypeId { get; set; }

		internal static KotlinValueParameter FromProtobuf (ValueParameter vp, JvmNameResolver resolver)
		{
			if (vp is null)
				return null;

			return new KotlinValueParameter {
				Flags = vp.Flags,
				Name = resolver.GetString (vp.Name),
				Type = KotlinType.FromProtobuf (vp.Type, resolver),
				TypeId = vp.TypeId,
				VarArgElementType = KotlinType.FromProtobuf (vp.VarargElementType, resolver),
				VarArgElementTypeId = vp.VarargElementTypeId
			};
		}
	}

	public enum KotlinVariance
	{
		In = 0,
		Out = 1,
		Inv = 2,
	}

	public enum KotlinProjection
	{
		In = 0,
		Out = 1,
		Inv = 2,
		Star = 3,
	}

	public enum KotlinEffectType
	{
		ReturnsConstant = 0,
		Calls = 1,
		ReturnsNotNull = 2,
	}

	public enum KotlinInvocationKind
	{
		AtMostOnce = 0,
		ExactlyOnce = 1,
		AtLeastOnce = 2,
	}

	public enum KotlinConstantValue
	{
		True = 0,
		False = 1,
		Null = 2,
	}

	public enum KotlinVersionLevel
	{
		Warning = 0,
		Error = 1,
		Hidden = 2,
	}

	public enum KotlinVersionKind
	{
		LanguageVersion = 0,
		CompilerVersion = 1,
		ApiVersion = 2,
	}

	public enum KotlinAnnotationArgumentType
	{
		Byte = 0,
		Char = 1,
		Short = 2,
		Int = 3,
		Long = 4,
		Float = 5,
		Double = 6,
		Boolean = 7,
		String = 8,
		Class = 9,
		Enum = 10,
		Annotation = 11,
		Array = 12,
	}

	[Flags]
	public enum KotlinClassFlags
	{
		HasAnnotations =	0b00_00_000_1,

		Internal =		0b00_00_000_0,
		Private =		0b00_00_001_0,
		Protected =		0b00_00_010_0,
		Public =		0b00_00_011_0,
		PrivateToThis =		0b00_00_100_0,
		Local =			0b00_00_101_0,

		Final =			0b00_00_000_0,
		Open =			0b00_01_000_0,
		Abstract =		0b00_10_000_0,
		Sealed =		0b00_11_000_0,

		Class =			0b000_00_000_0,
		Interface =		0b001_00_000_0,
		EnumClass =		0b010_00_000_0,
		EnumEntry =		0b011_00_000_0,
		AnnotationClass =	0b100_00_000_0,
		Object =		0b101_00_000_0,
		CompanionObject =	0b111_00_000_0,

		IsInner =		0b_00001_000_00_000_0,
		IsData =		0b_00010_000_00_000_0,
		IsExternalClass =	0b_00100_000_00_000_0,
		IsExpectClass =		0b_01000_000_00_000_0,
		IsInlineClass =		0b_10000_000_00_000_0
	}
}
