using System;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaUnresolvableModel
	{
		public IJavaResolvable Unresolvable { get; }
		public string MissingType { get; }
		public UnresolvableType Type { get; }
		public bool RemovedEntireType { get; set; }

		public JavaUnresolvableModel (IJavaResolvable unresolvable, string missingType, UnresolvableType type)
		{
			Unresolvable = unresolvable;
			MissingType = missingType;
			Type = type;
		}

		public string GetDisplayMessage ()
		{
			var member = Unresolvable as JavaMemberModel;

			if (Type == UnresolvableType.DollarSign)
				return RemovedEntireType ?
					$"The type '{member?.DeclaringType.FullName}' was removed because the required {GetUnresolvableType ()} '{GetUnresolvable ()}' was removed because its name contains a dollar sign." :
					$"The {GetUnresolvableType ()} '{GetUnresolvable ()}' was removed because its name contains a dollar sign.";

			if (Type == UnresolvableType.InvalidBaseType)
				return $"The {GetUnresolvableType ()} '{GetUnresolvable ()}' was removed because the base type '{MissingType}' is invalid.";

			if (Unresolvable is JavaTypeModel)
				return $"The {GetUnresolvableType ()} '{GetUnresolvable ()}' was removed because the Java {GetReason ()} '{MissingType}' could not be found.";

			return RemovedEntireType ?
				$"The type '{member?.DeclaringType.FullName}' was removed because the required {GetUnresolvableType ()} '{GetUnresolvable ()}' was removed because the Java {GetReason ()} '{MissingType}' could not be found." :
				$"The {GetUnresolvableType ()} '{GetUnresolvable ()}' was removed because the Java {GetReason ()} '{MissingType}' could not be found.";
		}

		string GetUnresolvableType ()
		{
			if (Unresolvable is JavaFieldModel)
				return "field";
			if (Unresolvable is JavaConstructorModel || (Unresolvable is JavaParameterModel p && p.DeclaringMethod is JavaConstructorModel))
				return "constructor";
			if (Unresolvable is JavaMethodModel || (Unresolvable is JavaParameterModel p2 && p2.DeclaringMethod is JavaMethodModel))
				return "method";
			if (Unresolvable is JavaClassModel)
				return "class";
			if (Unresolvable is JavaInterfaceModel)
				return "interface";

			return string.Empty;
		}

		string GetUnresolvable ()
		{
			if (Unresolvable is JavaParameterModel p)
				return p.DeclaringMethod.ToString ();

			return Unresolvable.ToString ();
		}

		string GetReason ()
		{
			return Type switch {
				UnresolvableType.FieldType => "field type",
				UnresolvableType.ReturnType => "return type",
				UnresolvableType.ParameterType => "parameter type",
				UnresolvableType.BaseType => "base type",
				UnresolvableType.ImplementsType => "implemented interface type",
				_ => throw new NotImplementedException (),
			};
		}
	}

	public enum UnresolvableType
	{
		DollarSign,
		FieldType,
		ReturnType,
		ParameterType,
		BaseType,
		ImplementsType,
		InvalidBaseType,
	}
}
