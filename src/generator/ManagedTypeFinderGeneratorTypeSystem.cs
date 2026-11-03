#if GENERATOR
using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class ManagedTypeFinderGeneratorTypeSystem : ManagedTypeFinderDefault
	{
		public ManagedTypeFinderGeneratorTypeSystem (GenBase [] types)
		{
			this.types = types;
		}

		GenBase [] types;

		protected override void OnAnnotationsParsed (IEnumerable<AnnotatedItem> itemsToBeBound)
		{
			LoadManagedMappings (itemsToBeBound, types.Select (t => t.Wrap ()).ToArray ());
		}

		#region ManagedTypeFinder implementation

		public abstract class Wrapper<T>
		{
			public T Value { get; set; }
		}

		public class TType : Wrapper<GenBase>, IType { }
		public class TDefinition : Wrapper<Field>, IDefinition { }
		public class TProperty : Wrapper<Field>, IProperty { }
		public class TMethodBase : Wrapper<MethodBase>, IMethodBase { }

		public override string GetManagedName (ManagedTypeFinder.IType t)
		{
			return t.Value ().FullName;
		}

		public override string GetJavaName (IDefinition f)
		{
			return f.Value ().JavaName;
		}

		public override string GetJavaName (IType t)
		{
			return t.Value ().JavaName;
		}

		public override IProperty GetAnnotatedField (IType t, string fieldName)
		{
			return GetField (t, fieldName).WrapAsProperty ();
		}

		public override IDefinition GetDefinitionField (IType t, string fieldName)
		{
			return GetField (t, fieldName).WrapAsDefinition ();
		}

		Field GetField (IType t, string fieldName)
		{
			return t.Value ().Fields.FirstOrDefault (f => f.JavaName == fieldName);
		}

		public override TypeName [] GetParameterTypes (IMethodBase method)
		{
			return method.Value ().Parameters.Select (p => new TypeName () { Name = p.Type }).ToArray ();
		}

		public override string GetPropertyName (IProperty m)
		{
			return m.Value ().Name;
		}

		public override string GetDefinitionName (IDefinition m)
		{
			return m.Value ().Name;
		}

		public override void SetName (ManagedMemberInfo destination, IType iType)
		{
			var type = iType.Value ();
			destination.Type.Namespace = type.Namespace;
			destination.Type.Name = type.Name.Replace ('/', '.');
		}

		public override IEnumerable<IDefinition> GetFields (IType t)
		{
			return t.Value ().Fields.Select (f => f.WrapAsDefinition ());
		}

		public override string GetMethodName (IMethodBase m)
		{
			return m.Value ()?.Name;
		}

		public override string GetFieldManagedTypeName (ManagedTypeFinder.IProperty property)
		{
			return property.Value ()?.TypeName;
		}

		public override string GetMethodReturnManagedTypeName (IMethodBase method)
		{
			var m = method.Value ();
			return m is Ctor ? null : ((Method) m)?.ManagedReturn;
		}

		public override string GetParameterManagedTypeName (IMethodBase m, int index)
		{
			return m.Value ()?.Parameters [index]?.Type;
		}

		public override IMethodBase GetMethod (IType iType, AnnotatedItem item)
		{
			var t = iType.Value ();
			var list = t.GetMethods ().Where (m => item.MemberName == "#ctor" ? m is Ctor : m is Method);
			var methods = list.Where (m =>
				(item.MemberName == "#ctor" && m is Ctor || m is Method && ((Method) m).JavaName == item.MemberName) && 
				m.Parameters.Count == item.Arguments.Length)
				.ToArray ();

			var argTypeLists = methods.Select (m => new { Method = m, Jni = m is Ctor ? ((Ctor) m).JniSignature : ((Method) m).JniSignature})
			                          .Select (p => new { Method = p.Method, Arguments = p.Jni == null ? null : ParseJniMethodArgumentsSignature (p.Jni)})
			                          .ToArray ();

			// this .Replace() is needed so that T[] can match generic arguments
			Func<string, string> stripParamSuffix = s => s.Replace ("...", "").Replace ("[]", "");
			Func<string, string> stripGenParams = s => s == null ? s : (s.Contains ('<') ? s.Substring (0, s.IndexOf ('<')) : s).Replace ("...", "[]");
			Func<string, string, bool> cmp = (v, w) => stripGenParams (v) == stripGenParams (w);

			for (int i = 0; i < argTypeLists.Length; i++) {
				var argTypeListPair = argTypeLists [i];
				var argTypeList = argTypeListPair.Arguments;
				if (argTypeList == null || item.Arguments.Length != argTypeList.Length)
					continue;

				bool mismatch = false;
				for (int a = 0; a < argTypeList.Length; a++) {
					if (cmp (argTypeList [a], item.Arguments [a])
						|| t.TypeParameters != null && t.TypeParameters.Any (ga => cmp (ga.Name, stripParamSuffix (item.Arguments [a])))
						|| argTypeListPair.Method.GenericArguments != null && argTypeListPair.Method.GenericArguments.Any (ga => cmp (ga.Name, stripParamSuffix (item.Arguments [a]))))
						continue;
					mismatch = true;
					break;
				}
				if (mismatch)
					continue;
				return methods [i].Wrap ();
			}
			Errors.Add ("warning: method overload not found: " + t.FullName + " member: " + item.FormatMember ());
			return null;
		}

		#endregion
	}
}

#endif
