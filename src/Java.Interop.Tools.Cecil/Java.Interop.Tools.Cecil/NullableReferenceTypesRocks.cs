using System;
using System.Linq;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace Java.Interop.Tools.Cecil
{
	// Partial support for determining NRT status of method and field types.
	// Reference: https://github.com/dotnet/roslyn/blob/main/docs/features/nullable-metadata.md
	// The basics are supported, but advanced annotations like array elements,
	// type parameters, and tuples are not supported.  Our use case doesn't really need them.
	public static class NullableReferenceTypesRocks
	{
		public static Nullability GetTypeNullability (this FieldDefinition field)
		{
			if (field.FieldType.FullName == "System.Void")
				return Nullability.NotNull;

			// Look for explicit annotation on field
			var metadata = NullableMetadata.FromAttributeCollection (field.CustomAttributes);

			if (metadata != null)
				return (Nullability) metadata.Data [0];

			// Default nullability status for type
			return GetNullableContext (field.DeclaringType.CustomAttributes);
		}

		public static Nullability GetReturnTypeNullability (this MethodDefinition method)
		{
			if (method.MethodReturnType.ReturnType.FullName == "System.Void")
				return Nullability.NotNull;

			// Look for explicit annotation on return type
			var metadata = NullableMetadata.FromAttributeCollection (method.MethodReturnType.CustomAttributes);

			if (metadata != null)
				return (Nullability) metadata.Data [0];

			// Default nullability status for method
			var nullable = GetNullableContext (method.CustomAttributes);

			if (nullable != Nullability.Oblivous)
				return nullable;

			// Default nullability status for type
			return GetNullableContext (method.DeclaringType.CustomAttributes);
		}

		public static Nullability GetTypeNullability (this ParameterDefinition parameter, MethodDefinition method)
		{
			if (parameter.ParameterType.FullName == "System.Void")
				return Nullability.NotNull;

			// Look for explicit annotation on parameter
			var metadata = NullableMetadata.FromAttributeCollection (parameter.CustomAttributes);

			if (metadata != null)
				return (Nullability) metadata.Data [0];

			// Default nullability status for method
			var nullable = GetNullableContext (method.CustomAttributes);

			if (nullable != Nullability.Oblivous)
				return nullable;

			// Default nullability status for type
			return GetNullableContext (method.DeclaringType.CustomAttributes);
		}

		static Nullability GetNullableContext (Collection<CustomAttribute> attrs)
		{
			var attribute = attrs.FirstOrDefault (t => t.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");

			if (attribute != null)
				return (Nullability) (byte) attribute.ConstructorArguments.First ().Value;

			return Nullability.Oblivous;
		}
	}

	public enum Nullability
	{
		Oblivous,
		NotNull,
		Nullable
	}

	class NullableMetadata
	{
		public byte [] Data { get; private set; }

		NullableMetadata (byte [] data) => Data = data;

		NullableMetadata (byte data) => Data = new [] { data };

		public static NullableMetadata? FromAttributeCollection (Collection<CustomAttribute> attrs)
		{
			if (attrs is null)
				return null;

			var attribute = attrs.FirstOrDefault (t => t.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");

			if (attribute is null)
				return null;

			var ctor_arg = attribute.ConstructorArguments.First ();

			if (ctor_arg.Value is CustomAttributeArgument [] caa)
				ctor_arg = caa [0];

			if (ctor_arg.Value is byte b)
				return new NullableMetadata (b);

			if (ctor_arg.Value is byte [] b2)
				return new NullableMetadata (b2);

			return null;
		}
	}
}
