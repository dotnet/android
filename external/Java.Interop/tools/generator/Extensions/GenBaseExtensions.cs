#if GENERATOR
using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public static class GenBaseExtensions
	{
		public static IEnumerable<GenBase> FlattenTypes (this GenBase t)
		{
			yield return t;
			foreach (var x in t.NestedTypes.SelectMany (nt => nt.FlattenTypes ()))
				yield return x;
		}

		public static IEnumerable<GenBase> GetSelfAndAncestors (this GenBase t)
		{
			yield return t;
			if (t.BaseGen != null)
				foreach (var a in t.BaseGen.GetSelfAndAncestors ())
					yield return t;
		}

		public static IEnumerable<MethodBase> GetMethods (this GenBase t)
		{
			if (t is ClassGen)
				foreach (var m in ((ClassGen) t).Ctors)
					yield return m;
			foreach (var m in t.Methods)
				yield return m;
			foreach (var p in t.Properties) {
				if (p.Getter != null)
					yield return p.Getter;
				if (p.Setter != null)
					yield return p.Setter;
			}
			if (t.BaseGen != null)
				foreach (var m in t.BaseGen.GetMethods ())
					yield return m;
			if (t is InterfaceGen)
				foreach (var it in ((InterfaceGen) t).GetAllDerivedInterfaces ())
					foreach (var m in it.GetMethods ())
						yield return m;
		}

		public static IEnumerable<Field> GetFields (this GenBase t)
		{
			return t.BaseGen == null ? t.Fields : t.Fields.Concat (t.BaseGen.GetFields ());
		}

		#region conversion between general interfaces

		public static GenBase Value (this ManagedTypeFinder.IType t)
		{
			return ((ManagedTypeFinderGeneratorTypeSystem.TType) t)?.Value;
		}
		public static Field Value (this ManagedTypeFinder.IProperty p)
		{
			return ((ManagedTypeFinderGeneratorTypeSystem.TProperty) p)?.Value;
		}
		public static Field Value (this ManagedTypeFinder.IDefinition t)
		{
			return ((ManagedTypeFinderGeneratorTypeSystem.TDefinition) t)?.Value;
		}
		public static MethodBase Value (this ManagedTypeFinder.IMethodBase t)
		{
			return ((ManagedTypeFinderGeneratorTypeSystem.TMethodBase) t)?.Value;
		}

		public static ManagedTypeFinder.IType Wrap (this GenBase t)
		{
			return t == null ? null : new ManagedTypeFinderGeneratorTypeSystem.TType () { Value = t };
		}
		public static ManagedTypeFinder.IProperty WrapAsProperty (this Field t)
		{
			return t == null ? null : new ManagedTypeFinderGeneratorTypeSystem.TProperty () { Value = t };
		}
		public static ManagedTypeFinder.IDefinition WrapAsDefinition (this Field t)
		{
			return t == null ? null : new ManagedTypeFinderGeneratorTypeSystem.TDefinition () { Value = t };
		}
		public static ManagedTypeFinder.IMethodBase Wrap (this MethodBase t)
		{
			return t == null ? null : new ManagedTypeFinderGeneratorTypeSystem.TMethodBase () { Value = t };
		}

		#endregion
	}
}
#endif
