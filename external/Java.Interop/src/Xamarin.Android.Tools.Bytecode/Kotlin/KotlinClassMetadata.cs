using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using org.jetbrains.kotlin.metadata.jvm;
using ProtoBuf;
using Type = org.jetbrains.kotlin.metadata.jvm.Type;

namespace Xamarin.Android.Tools.Bytecode
{
	// https://github.com/JetBrains/kotlin/blob/master/core/metadata.jvm/src/jvm_metadata.proto
	public class KotlinFile
	{
		public List<KotlinFunction>? Functions { get; set; }
		public List<KotlinProperty>? Properties { get; set; }
		public List<KotlinTypeAlias>? TypeAliases { get; set; }
		public KotlinTypeTable? TypeTable { get; set; }
		public KotlinVersionRequirementTable? VersionRequirementTable { get; set; }

		internal static KotlinFile FromProtobuf (Package c, JvmNameResolver resolver)
		{
			return new KotlinFile {
				Functions = c.Functions.ToList (resolver, KotlinFunction.FromProtobuf),
				Properties = c.Properties.ToList (resolver, KotlinProperty.FromProtobuf),
				TypeAliases = c.TypeAlias.ToList (resolver, KotlinTypeAlias.FromProtobuf),
				TypeTable = KotlinTypeTable.FromProtobuf (c.TypeTable, resolver),
				VersionRequirementTable = KotlinVersionRequirementTable.FromProtobuf (c.VersionRequirementTable, resolver)
			};
		}
	}

	public class KotlinClass : KotlinFile
	{
		public string? CompanionObjectName { get; set; }
		public List<KotlinConstructor>? Constructors { get; set; }
		public List<string>? EnumEntries { get; set; }
		public KotlinClassFlags Flags { get; set; }
		public string? FullyQualifiedName { get; set; }
		public KotlinClassInheritability Inheritability { get; set; }
		public List<string> NestedClassNames { get; set; } = new List<string> ();
		public KotlinClassType ObjectType { get; set; }
		public List<string>? SealedSubclassFullyQualifiedNames { get; set; }
		public List<string>? SuperTypeIds { get; set; }
		public List<KotlinType>? SuperTypes { get; set; }
		public List<KotlinTypeParameter>? TypeParameters { get; set; }
		public int []? VersionRequirements { get; set; }
		public KotlinClassVisibility Visibility { get; set; }

		internal static KotlinClass FromProtobuf (Class c, JvmNameResolver resolver)
		{
			return new KotlinClass {
				CompanionObjectName = c.CompanionObjectName > 0 ? resolver.GetString (c.CompanionObjectName) : null,
				Constructors = c.Constructors.ToList (resolver, KotlinConstructor.FromProtobuf),
				EnumEntries = c.EnumEntries?.Select (e => resolver.GetString (e.Name)).ToList (),
				Flags = (KotlinClassFlags)c.Flags,
				FullyQualifiedName = c.FqName > 0 ? resolver.GetString (c.FqName) : null,
				Functions = c.Functions.ToList (resolver, KotlinFunction.FromProtobuf),
				Inheritability = (KotlinClassInheritability)((c.Flags & 0b110000) >> 4),
				NestedClassNames = c.NestedClassNames?.Select (n => resolver.GetString (n)).ToList () ?? new List<string> (),
				ObjectType = (KotlinClassType) ((c.Flags & 0b111000000) >> 6),
				Properties = c.Properties.ToList (resolver, KotlinProperty.FromProtobuf),
				SealedSubclassFullyQualifiedNames = c.SealedSubclassFqNames?.Select (n => resolver.GetString (n)).ToList (),
				SuperTypeIds = c.SupertypeIds?.Select (n => resolver.GetString (n)).ToList (),
				SuperTypes = c.Supertypes.ToList (resolver, KotlinType.FromProtobuf),
				TypeAliases = c.TypeAlias.ToList (resolver, KotlinTypeAlias.FromProtobuf),
				TypeParameters = c.TypeParameters.ToList (resolver, KotlinTypeParameter.FromProtobuf),
				VersionRequirements = c.VersionRequirements,
				TypeTable = KotlinTypeTable.FromProtobuf (c.TypeTable, resolver),
				VersionRequirementTable = KotlinVersionRequirementTable.FromProtobuf (c.VersionRequirementTable, resolver),
				Visibility = (KotlinClassVisibility)((c.Flags & 0b1110) >> 1)
			};
		}
	}

	public class KotlinMethodBase
	{
		public int []? VersionRequirements { get; set; }

		public virtual string GetSignature () => string.Empty;
	}

	public class KotlinConstructor : KotlinMethodBase
	{
		public KotlinConstructorFlags Flags { get; set; }
		public List<KotlinValueParameter>? ValueParameters { get; set; }

		internal static KotlinConstructor? FromProtobuf (Constructor c, JvmNameResolver resolver)
		{
			if (c is null)
				return null;

			return new KotlinConstructor {
				Flags = (KotlinConstructorFlags)c.Flags,
				ValueParameters = c.ValueParameters.ToList (resolver, KotlinValueParameter.FromProtobuf),
				VersionRequirements = c.VersionRequirements
			};
		}

		public override string GetSignature ()
		{
			return $"({ValueParameters?.GetSignature ()})V";
		}
	}

	public class KotlinAnnotation
	{
		public int Id { get; set; }
		public List<KotlinAnnotationArgument>? Arguments { get; set; }

		internal static KotlinAnnotation? FromProtobuf (org.jetbrains.kotlin.metadata.jvm.Annotation a, JvmNameResolver resolver)
		{
			if (a is null)
				return null;

			return new KotlinAnnotation {
				Id = a.Id,
				Arguments = a.Arguments.ToList (resolver, KotlinAnnotationArgument.FromProtobuf),
			};
		}
	}

	public class KotlinAnnotationArgument
	{
		public int NameId { get; set; }
		public KotlinAnnotationArgumentValue? Value { get; set; }

		internal static KotlinAnnotationArgument? FromProtobuf (org.jetbrains.kotlin.metadata.jvm.Annotation.Argument a, JvmNameResolver resolver)
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
		public string? StringValue { get; set; }
		public int ClassId { get; set; }
		public int EnumValueId { get; set; }
		public KotlinAnnotation? Annotation { get; set; }
		public List<KotlinAnnotationArgumentValue>? ArrayElements { get; set; }
		public int ArrayDimensionCount { get; set; }
		public KotlinAnnotationFlags Flags { get; set; }

		internal static KotlinAnnotationArgumentValue? FromProtobuf (org.jetbrains.kotlin.metadata.jvm.Annotation.Argument.Value value, JvmNameResolver resolver)
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
				ArrayElements = value.ArrayElements.ToList (resolver,KotlinAnnotationArgumentValue.FromProtobuf),
				Flags = (KotlinAnnotationFlags)value.Flags
			};
		}
	}

	public class KotlinEffect
	{
		public KotlinEffectType EffectType { get; set; }
		public List<KotlinExpression>? EffectConstructorArguments { get; set; }
		public KotlinExpression? ConclusionOfConditionalEffect { get; set; }
		public KotlinInvocationKind Kind { get; set; }

		internal static KotlinEffect? FromProtobuf (Effect ef, JvmNameResolver resolver)
		{
			if (ef is null)
				return null;

			return new KotlinEffect {
				EffectType = (KotlinEffectType) ef.effect_type,
				EffectConstructorArguments = ef.EffectConstructorArguments.ToList (resolver, KotlinExpression.FromProtobuf),
				ConclusionOfConditionalEffect = KotlinExpression.FromProtobuf (ef.ConclusionOfConditionalEffect, resolver),
				Kind = (KotlinInvocationKind) ef.Kind
			};
		}
	}

	public class KotlinExpression
	{
		public KotlinExpressionFlags Flags { get; set; }
		public int ValueParameterReference { get; set; }
		public KotlinConstantValue ConstantValue { get; set; }
		public KotlinType? IsInstanceType { get; set; }
		public int IsInstanceTypeId { get; set; }
		public List<KotlinExpression>? AndArguments { get; set; }
		public List<KotlinExpression>? OrArguments { get; set; }

		internal static KotlinExpression? FromProtobuf (Expression exp, JvmNameResolver resolver)
		{
			if (exp is null)
				return null;

			return new KotlinExpression {
				Flags = (KotlinExpressionFlags)exp.Flags,
				ValueParameterReference = exp.ValueParameterReference,
				ConstantValue = (KotlinConstantValue) exp.constant_value,
				IsInstanceType = KotlinType.FromProtobuf (exp.IsInstanceType, resolver),
				IsInstanceTypeId = exp.IsInstanceTypeId,
				AndArguments = exp.AndArguments.ToList (resolver, KotlinExpression.FromProtobuf),
				OrArguments = exp.OrArguments.ToList (resolver, KotlinExpression.FromProtobuf),
			};
		}
	}

	public class KotlinFunction : KotlinMethodBase
	{
		public string? Name { get; set; }
		public string? JvmName { get; set; }
		public string? JvmSignature { get; set; }
		public KotlinFunctionFlags Flags { get; set; }
		public KotlinType? ReturnType { get; set; }
		public int ReturnTypeId { get; set; }
		public List<KotlinTypeParameter>? TypeParameters { get; set; }
		public KotlinType? ReceiverType { get; set; }
		public int ReceiverTypeId { get; set; }
		public KotlinTypeTable? TypeTable { get; set; }
		public KotlinContract? Contract { get; set; }
		public List<KotlinValueParameter>? ValueParameters { get; set; }

		internal static KotlinFunction? FromProtobuf (Function f, JvmNameResolver resolver)
		{
			if (f is null)
				return null;

			var sig = Extensible.GetValue<JvmMethodSignature> (f, 100);

			return new KotlinFunction {
				Flags = (KotlinFunctionFlags)f.Flags,
				Name = resolver.GetString (f.Name),
				JvmName = resolver.GetString ((sig is null || sig.Name == 0) ? f.Name : sig.Name),
				JvmSignature = sig is null ? null : resolver.GetString (sig.Desc),
				ReturnType = KotlinType.FromProtobuf (f.ReturnType, resolver),
				ReturnTypeId = f.ReturnTypeId,
				ReceiverType = KotlinType.FromProtobuf (f.ReceiverType, resolver),
				ReceiverTypeId = f.ReceiverTypeId,
				TypeParameters = f.TypeParameters.ToList (resolver, KotlinTypeParameter.FromProtobuf),
				ValueParameters = f.ValueParameters.ToList (resolver, KotlinValueParameter.FromProtobuf),
				VersionRequirements = f.VersionRequirements
			};
		}

		public override string? ToString () => Name;

		public string GetFlags ()
		{
			var sb = new StringBuilder ();

			foreach (var f in Enum.GetNames (typeof (KotlinFunctionFlags))) {
				if (Flags.HasFlag ((KotlinFunctionFlags)Enum.Parse (typeof (KotlinFunctionFlags), f)))
					sb.Append (f);
			}

			return sb.ToString ();
		}
	}

	public class KotlinContract
	{
		public List<KotlinEffect>? Effects { get; set; }

		internal static KotlinContract? FromProtobuf (Contract c, JvmNameResolver resolver)
		{
			return new KotlinContract {
				Effects = c.Effects.ToList (resolver, KotlinEffect.FromProtobuf),
			};
		}
	}

	public class KotlinProperty : KotlinMethodBase
	{
		public string? Name { get; set; }
		public KotlinPropertyFlags Flags { get; set; }
		public KotlinType? ReturnType { get; set; }
		public int ReturnTypeId { get; set; }
		public List<KotlinTypeParameter>? TypeParameters { get; set; }
		public KotlinType? ReceiverType { get; set; }
		public int ReceiverTypeId { get; set; }
		public KotlinValueParameter? SetterValueParameter { get; set; }
		public int GetterFlags { get; set; }
		public int SetterFlags { get; set; }

		internal static KotlinProperty? FromProtobuf (Property p, JvmNameResolver resolver)
		{
			if (p is null)
				return null;

			return new KotlinProperty {
				Flags = (KotlinPropertyFlags)p.Flags,
				Name = resolver.GetString (p.Name),
				ReturnTypeId = p.ReturnTypeId,
				ReturnType = KotlinType.FromProtobuf (p.ReturnType, resolver),
				ReceiverType = KotlinType.FromProtobuf (p.ReceiverType, resolver),
				ReceiverTypeId = p.ReceiverTypeId,
				SetterValueParameter = KotlinValueParameter.FromProtobuf (p.SetterValueParameter, resolver),
				GetterFlags = p.GetterFlags,
				SetterFlags = p.SetterFlags,
				TypeParameters = p.TypeParameters.ToList (resolver, KotlinTypeParameter.FromProtobuf),
				VersionRequirements = p.VersionRequirements
			};
		}

		public override string? ToString () => Name;
	}

	public class KotlinType
	{
		public List<KotlinTypeArgument>? Arguments { get; set; }
		public bool Nullable { get; set; }
		public int? FlexibleTypeCapabilitiesId { get; set; }
		public KotlinType? FlexibleUpperBound { get; set; }
		public int FlexibleUpperBoundId { get; set; }
		public string? ClassName { get; set; }
		public int? TypeParameter { get; set; }
		public string? TypeParameterName { get; set; }
		public string? TypeAliasName { get; set; }
		public KotlinType? OuterType { get; set; }
		public int? OuterTypeId { get; set; }
		public KotlinType? AbbreviatedType { get; set; }
		public int? AbbreviatedTypeId { get; set; }
		public KotlinTypeFlags Flags { get; set; }

		internal static KotlinType? FromProtobuf (Type t, JvmNameResolver resolver)
		{
			if (t is null)
				return null;

			return new KotlinType {
				Arguments = t.Arguments.ToList (resolver, KotlinTypeArgument.FromProtobuf),
				Nullable = t.Nullable,
				FlexibleTypeCapabilitiesId = t.FlexibleTypeCapabilitiesId,
				FlexibleUpperBound = FromProtobuf (t.FlexibleUpperBound, resolver),
				ClassName = t.ClassName >= 0 ? resolver.GetString (t.ClassName.Value) : null,
				TypeParameter = t.TypeParameter,
				TypeParameterName = t.TypeParameterName >= 0 ? resolver.GetString (t.TypeParameterName.GetValueOrDefault ()) : null,
				OuterType = FromProtobuf (t.OuterType, resolver),
				OuterTypeId = t.OuterTypeId,
				AbbreviatedType = FromProtobuf (t.AbbreviatedType, resolver),
				AbbreviatedTypeId = t.AbbreviatedTypeId,
				Flags = (KotlinTypeFlags)t.Flags
			};
		}

		public string GetSignature ()
		{
			return KotlinUtilities.ConvertKotlinTypeSignature (this);
		}
	}

	public class KotlinTypeAlias
	{
		public int Flags { get; set; }
		public string? Name { get; set; }
		public List<KotlinTypeParameter>? TypeParameters { get; set; }
		public KotlinType? UnderlyingType { get; set; }
		public int UnderlyingTypeId { get; set; }
		public KotlinType? ExpandedType { get; set; }
		public int ExpandedTypeId { get; set; }
		public List<Annotation>? Annotations { get; set; }
		public int []? VersionRequirements { get; set; }

		internal static KotlinTypeAlias? FromProtobuf (TypeAlias ta, JvmNameResolver resolver)
		{
			if (ta is null)
				return null;

			return new KotlinTypeAlias {
				Flags = ta.Flags,
				Name = resolver.GetString (ta.Name),
				TypeParameters = ta.TypeParameters.ToList (resolver, KotlinTypeParameter.FromProtobuf),
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
		public KotlinType? Type { get; set; }
		public int TypeId { get; set; }

		internal static KotlinTypeArgument? FromProtobuf (Type.Argument ta, JvmNameResolver resolver)
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
		public string? Name { get; set; }
		public bool Reified { get; set; }
		public KotlinVariance Variance { get; set; }
		public List<KotlinType>? UpperBounds { get; set; }
		public int []? UpperBoundsIds { get; set; }

		internal static KotlinTypeParameter? FromProtobuf (TypeParameter vp, JvmNameResolver resolver)
		{
			if (vp is null)
				return null;

			return new KotlinTypeParameter {
				Id = vp.Id,
				Name = resolver.GetString (vp.Name),
				Reified = vp.Reified,
				Variance = (KotlinVariance) vp.variance,
				UpperBounds = vp.UpperBounds.ToList (resolver, KotlinType.FromProtobuf),
				UpperBoundsIds = vp.UpperBoundIds
			};
		}
	}

	public class KotlinTypeTable
	{
		public List<KotlinType>? Types { get; set; }
		public int FirstNullable { get; set; }

		internal static KotlinTypeTable? FromProtobuf (TypeTable ta, JvmNameResolver resolver)
		{
			if (ta is null)
				return null;

			return new KotlinTypeTable {
				Types = ta.Types.ToList (resolver, KotlinType.FromProtobuf),
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

		internal static KotlinVersionRequirement? FromProtobuf (VersionRequirement vr, JvmNameResolver resolver)
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
		public List<KotlinVersionRequirement>? Requirements { get; set; }

		internal static KotlinVersionRequirementTable? FromProtobuf (VersionRequirementTable vrt, JvmNameResolver resolver)
		{
			if (vrt is null)
				return null;

			return new KotlinVersionRequirementTable {
				Requirements = vrt.Requirements.ToList (resolver, KotlinVersionRequirement.FromProtobuf),
			};
		}
	}

	public class KotlinValueParameter
	{
		public KotlinParameterFlags Flags { get; set; }
		public string? Name { get; set; }
		public KotlinType? Type { get; set; }
		public int TypeId { get; set; }
		public KotlinType? VarArgElementType { get; set; }
		public int VarArgElementTypeId { get; set; }

		internal static KotlinValueParameter? FromProtobuf (ValueParameter vp, JvmNameResolver resolver)
		{
			if (vp is null)
				return null;

			return new KotlinValueParameter {
				Flags = (KotlinParameterFlags)vp.Flags,
				Name = resolver.GetString (vp.Name),
				Type = KotlinType.FromProtobuf (vp.Type, resolver),
				TypeId = vp.TypeId,
				VarArgElementType = KotlinType.FromProtobuf (vp.VarargElementType, resolver),
				VarArgElementTypeId = vp.VarargElementTypeId
			};
		}

		public string? GetSignature () => Type?.GetSignature ();
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

		IsInner =		0b_00001_000_00_000_0,
		IsData =		0b_00010_000_00_000_0,
		IsExternalClass =	0b_00100_000_00_000_0,
		IsExpectClass =		0b_01000_000_00_000_0,
		IsInlineClass =		0b_10000_000_00_000_0
	}

	public enum KotlinClassVisibility
	{
		Internal = 0,
		Private = 1,
		Protected = 2,
		Public = 3,
		PrivateToThis = 4,
		Local = 5
	}

	public enum KotlinClassType
	{
		Class = 0,
		Interface = 1,
		EnumClass = 2,
		EnumEntry = 3,
		AnnotationClass = 4,
		Object = 5,
		CompanionObject = 6
	}

	public enum KotlinClassInheritability
	{
		Final = 0,
		Open = 1,
		Abstract = 2,
		Sealed = 3
	}

	[Flags]
	public enum KotlinConstructorFlags
	{
		HasAnnotations =	0b0_000_1,

		Internal =		0b0_000_0,
		Private =		0b0_001_0,
		Protected =		0b0_010_0,
		Public =		0b0_011_0,
		PrivateToThis =		0b0_100_0,
		Local =			0b0_101_0,

		IsSecondary =		0b1_000_0
	}

	[Flags]
	public enum KotlinFunctionFlags
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

		Declaration =		0b00_00_000_0,
		FakeOverride =		0b01_00_000_0,
		Delegation =		0b10_00_000_0,
		Synthesized =		0b11_00_000_0,

		IsOperator =		0b_0000001_00_00_000_0,
		IsInfix =		0b_0000010_00_00_000_0,
		IsInline =		0b_0000100_00_00_000_0,
		IsTailrec =		0b_0001000_00_00_000_0,
		IsExternalFunction =	0b_0010000_00_00_000_0,
		IsSuspend =		0b_0100000_00_00_000_0,
		IsExpectFunction =	0b_1000000_00_00_000_0
	}

	[Flags]
	public enum KotlinPropertyFlags
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

		Declaration =		0b00_00_000_0,
		FakeOverride =		0b01_00_000_0,
		Delegation =		0b10_00_000_0,
		Synthesized =		0b11_00_000_0,

		IsVar =			0b_000000001_000_00_000_0,
		HasGetter =		0b_000000010_000_00_000_0,
		HasSetter =		0b_000000100_000_00_000_0,
		IsConst =		0b_000001000_000_00_000_0,
		IsLateInit =		0b_000010000_000_00_000_0,
		HasConstant =		0b_000100000_000_00_000_0,
		IsExternalProperty =	0b_001000000_000_00_000_0,
		IsDelegated =		0b_010000000_000_00_000_0,
		IsExpectProperty =	0b_100000000_000_00_000_0
	}

	[Flags]
	public enum KotlinParameterFlags
	{
		HasAnnotations =	0b000_1,

		DeclaresDefaultValue =	0b001_0,
		IsCrossInline =		0b010_0,
		IsNoInline =		0b100_0
	}

	[Flags]
	public enum KotlinAccessorFlags
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

		IsNotDefault =		0b001_00_000_0,
		IsExternalAccessor =	0b010_00_000_0,
		IsInlineAccessor =	0b100_00_000_0
	}

	[Flags]
	public enum KotlinExpressionFlags
	{
		IsNegated =		0b01,
		IsNullCheckPredicate =	0b10
	}

	[Flags]
	public enum KotlinAnnotationFlags
	{
		IsUnsigned =		0b01
	}

	[Flags]
	public enum KotlinTypeFlags
	{
		SuspendType =		0b01
	}
}
