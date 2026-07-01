using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	// It is not for use within generator; it is an isolated implementation model for
	// Java annotation - managed type binder.
	public class ManagedTypeFinderCecil : ManagedTypeFinderDefault
	{
		public ManagedTypeFinderCecil (string assemblyFileName)
		{
			assembly = assemblyFileName;
		}

		string assembly;

		#region AnnotationParserExtension

		protected override void OnAnnotationsParsed (IEnumerable<AnnotatedItem> itemsToBeBound)
		{
			var allTypes = AssemblyDefinition.ReadAssembly (assembly).Modules
				.SelectMany (m => m.Types)
				.SelectMany (t => t.FlattenTypes ())
				.ToArray ();
			var types = allTypes.Where (t => !t.FullName.StartsWith ("Android.Runtime.", StringComparison.Ordinal))
				// This condition is added for kind of hacky reason: interface consts are
				// saved in a static class (because C# interface cannot have consts) and
				// they have identical [Register]-ed Java names as that of the corresponding interface types.
				// In this java-C# matcher we don't need those consts-only classes, so filter them out.
				.Where (t => (t.IsInterface || t.BaseType == null || t.BaseType.FullName != "System.Object") && (t.Methods.Where (m => !m.IsConstructor).Any () || t.Properties.Any ()))
				// This condition would also look weird, but some managed types have manual binding with
				// [Register] attribute, namely ArrayAdapter<T>. They don't come up with
				// JavaTypeParametersAttribute, so we have to exclude them.
				.Where (t => !t.GenericParameters.Any ())
				.Select (t => t.Wrap ())
				.ToArray ();

			LoadManagedMappings (itemsToBeBound, types);
		}

		#endregion

		#region ManagedTypeFinder implementation
		public abstract class Wrapper<T>
		{
			public T Value { get; set; }
		}

		public class TType : Wrapper<TypeDefinition>, IType { }
		public class TDefinition : Wrapper<FieldDefinition>, IDefinition { }
		public class TProperty : Wrapper<PropertyDefinition>, IProperty { }
		public class TMethodBase : Wrapper<MethodDefinition>, IMethodBase { }

		internal Func<ICustomAttributeProvider,CustomAttribute> getRegisterAtt = t => t.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
		internal Func<ICustomAttributeProvider,CustomAttribute> getJavaTypesAtt = t => t.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Java.Interop.JavaTypeParametersAttribute");

		public override string GetManagedName (ManagedTypeFinder.IType t)
		{
			return t.Value ().GetCorrectName ();
		}

		public override string GetJavaName (IType t)
		{
			return GetJavaNameFromCustomAttribute (t.Value ());
		}

		public override string GetJavaName (IDefinition f)
		{
			return GetJavaNameFromCustomAttribute (f.Value ());
		}

		public override IProperty GetAnnotatedField (IType t, string fieldName)
		{
			return t.Value ().GetProperties ()
				.Select (f => new { Property = f, Value = GetJavaNameFromCustomAttribute (f) })
				.FirstOrDefault (p => p.Value == fieldName)
				?.Property.WrapAsProperty ();
		}

		public override IDefinition GetDefinitionField (IType iType, string fieldName)
		{
			// Since a managed interface cannot have fields, we put them into a class.
			// Retrieve the field from the class, if applicable.
			var t = iType.Value ();
			t = t.IsInterface ? t.Module.Types.FirstOrDefault (_ => _.GetCorrectNamespace () == t.GetCorrectNamespace () && _.GetCorrectName () == t.GetCorrectName ().Substring (1)) ?? t : t;
			return t.Fields.FirstOrDefault (f => GetJavaNameFromCustomAttribute (f) == fieldName).WrapAsDefinition ();
		}

		string GetJavaNameFromCustomAttribute (ICustomAttributeProvider t)
		{
			var ca = getRegisterAtt (t);
			return ca == null ? null : ca.ConstructorArguments.First ().Value as string;
		}

		public override TypeName [] GetParameterTypes (IMethodBase method)
		{
			return method.Value ().Parameters.Select (p => new TypeName () { Namespace = p.ParameterType.GetCorrectNamespace (), Name = p.ParameterType.GetCorrectName () }).ToArray ();
		}

		public override void SetName (ManagedMemberInfo destination, IType iType)
		{
			var type = iType.Value ();
			destination.Type.Namespace = type.GetCorrectNamespace ();
			destination.Type.Name = type.GetCorrectName ();
		}

		public override string GetPropertyName (IProperty m)
		{
			return m.Value ().Name;
		}

		public override string GetDefinitionName (IDefinition m)
		{
			return m.Value ().Name;
		}

		public override IEnumerable<IDefinition> GetFields (IType t)
		{
			return t.Value ().Fields.Select (f => f.WrapAsDefinition ());
		}

		public override string GetMethodName (IMethodBase m)
		{
			return m.Value ().Name;
		}

		public override string GetFieldManagedTypeName (ManagedTypeFinder.IProperty property)
		{
			return property.Value ().PropertyType.FullName;
		}

		public override string GetMethodReturnManagedTypeName (IMethodBase method)
		{
			var m = method.Value ();
			return m.IsConstructor ? null : m.ReturnType.FullName;
		}

		public override string GetParameterManagedTypeName (IMethodBase m, int index)
		{
			return m.Value ().Parameters [index].ParameterType.FullName;
		}

		public override IMethodBase GetMethod (IType iType, AnnotatedItem item)
		{
			var t = iType.Value ();
			Func<ICustomAttributeProvider, string> getRegisterAttName = type => {
				var ca = getRegisterAtt (type);
				return ca == null ? null : ca.ConstructorArguments [0].Value as string;
			};
			Func<ICustomAttributeProvider, string> getRegisterAttJni = type => {
				var ca = getRegisterAtt (type);
				return ca == null ? null : ca.ConstructorArguments [1].Value as string;
			};

			var methods = t.GetMethods ()
			               .Where (m => (item.MemberName == "#ctor" && m.IsConstructor || getRegisterAttName (m) == item.MemberName) && m.Parameters.Count == item.Arguments.Length)
			               .ToArray ();

			// First, try loose match just by checking argument count.

			MethodDefinition candidate = null;
			bool overloaded = false;
			foreach (var m in methods) {
				if (overloaded)
					break;
				if (candidate != null) {
					overloaded = true;
					break;
				} else
					candidate = m;
			}
			if (!overloaded) {
				if (candidate == null)
					Errors.Add ("warning: method with matching argument count not found: " + t + " member: " + item.FormatMember ());
				return candidate.Wrap ();
			}

			// Second, try strict match.

			Func<ICustomAttributeProvider, string []> getJavaGenTypesValue = td => {
				var a = getJavaTypesAtt (td);
				var arr = a == null ? new string [0] : ((CustomAttributeArgument []) a.ConstructorArguments [0].Value).Select (ca => ca.Value as string);
				return arr.Select (s => s.Contains (' ') ? s.Substring (0, s.IndexOf (' ')) : s).ToArray ();
			};
			var typeGenArgs = t.GetSelfAndAncestors ().SelectMany (getJavaGenTypesValue).ToArray ();

			var argTypeLists = methods.Select (m => new { Method = m, Jni = getRegisterAttJni (m)})
			                    .Select (p => new { Method = p.Method, Arguments = p.Jni == null ? null : ParseJniMethodArgumentsSignature (p.Jni)})
			                    .ToArray ();

			for (int i = 0; i < argTypeLists.Length; i++) {
				var argTypeListPair = argTypeLists [i];
				var argTypeList = argTypeListPair.Arguments;
				var methodGenArgs = getJavaGenTypesValue (argTypeListPair.Method);
				if (!AreArgumentsEqualLax (argTypeList, item.Arguments, typeGenArgs.Concat (methodGenArgs)))
					continue;
				return methods [i].Wrap ();
			}
			Errors.Add ("warning: method overload not found: " + t + " member: " + item.FormatMember ());
			return null;
		}
		#endregion
	}
}

