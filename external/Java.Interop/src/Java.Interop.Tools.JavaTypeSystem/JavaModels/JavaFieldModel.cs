using System;
using System.Collections.Generic;
using System.Linq;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaFieldModel : JavaMemberModel
	{
		public string Type { get; }
		public string TypeGeneric { get; }
		public string? Value { get; }
		public bool IsTransient { get; }
		public bool IsVolatile { get; }
		public bool IsNotNull { get; }

		public JavaTypeReference? TypeModel { get; private set; }

		public JavaFieldModel (string name, string visibility, string type, string typeGeneric, string? value, bool isStatic, JavaTypeModel declaringType, bool isFinal, string deprecated, string jniSignature, bool isTransient, bool isVolatile, bool isNotNull, string? annotatedVisibility)
			: base (name, isStatic, isFinal, visibility, declaringType, deprecated, jniSignature, annotatedVisibility)
		{
			Type = type;
			TypeGeneric = typeGeneric;
			Value = value;
			IsTransient = isTransient;
			IsVolatile = isVolatile;
			IsNotNull = isNotNull;
		}

		public override void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables)
		{
			if (Name.Contains ('$')) {
				unresolvables.Add (new JavaUnresolvableModel (this, "$", UnresolvableType.DollarSign));
				return;
			}

			var type_parameters = DeclaringType.GetApplicableTypeParameters ().ToArray ();

			try {
				TypeModel = types.ResolveTypeReference (TypeGeneric, type_parameters);
			} catch (JavaTypeResolutionException) {
				unresolvables.Add (new JavaUnresolvableModel (this, TypeGeneric, UnresolvableType.FieldType));

				return;
			}
		}
	}
}
