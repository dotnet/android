using System;
using System.Linq;
//using IKVM.Reflection;
//using Type = IKVM.Reflection.Type;
using Mono.Cecil;
using Type = Mono.Cecil.TypeDefinition;

namespace Android.Runtime
{
	public static class IKVMAttributeExtensions
	{
		public static string GetMemberType (this IMemberDefinition m)
		{
			var md = m as MethodDefinition;
			if (md != null)
				return md.IsConstructor ? "Constructor" : "Method";
			if (m is PropertyDefinition)
				return "Property";
			if (m is FieldDefinition)
				return "Field";
			if (m is EventDefinition)
				return "Event";
			if (m is TypeDefinition)
				return "Type";
			throw new NotImplementedException ();
		}

		public static T [] GetCustomAttributes<T> (this Mono.Cecil.IMemberDefinition m, bool dummy)
		{
			return m.CustomAttributes.Where (ca => ca.AttributeType.FullName == typeof(T).FullName)
				.Select (ca => (T) ca.CreateInstance (typeof (T))).ToArray ();
		}
		public static T CreateInstance<T> (this CustomAttribute attr)
		{
			return (T) CreateInstance (attr, typeof(T));
		}

		public static object CreateInstance (this CustomAttribute attr, System.Type type)
		{
			var obj = Activator.CreateInstance (type, attr.ConstructorArguments.Select (a => a.Value).ToArray ());
			foreach (var f in attr.Fields)
				type.GetField (f.Name).SetValue (obj, f.Argument.Value);
			foreach (var p in attr.Properties)
				type.GetProperty (p.Name).SetValue (obj, p.Argument.Value, null);
			return obj;
		}
	}
}

