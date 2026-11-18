using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	class JavaTypeResolutionException : Exception
	{
		public JavaTypeResolutionException (string message) : base (message)
		{
		}
	}
	
	public static class JavaApiTypeResolverExtensions
	{
		public static JavaTypeReference Parse (this JavaApi api, string name, params JavaTypeParameters?[] contextTypeParameters)
		{
			var tn = JavaTypeName.Parse (name);
			return JavaTypeNameToReference (api, tn, contextTypeParameters);
		}

		static JavaTypeReference JavaTypeNameToReference (this JavaApi api, JavaTypeName tn, params JavaTypeParameters?[] contextTypeParameters)
		{
			var tp = contextTypeParameters.Where (tps => tps != null)
				.SelectMany (tps => tps!.TypeParameters)
				.FirstOrDefault (xp => xp.Name == tn.DottedName);
			if (tp != null)
				return new JavaTypeReference (tp, tn.ArrayPart);
			if (tn.DottedName == JavaTypeReference.GenericWildcard.SpecialName)
				return new JavaTypeReference (tn.BoundsType, tn.GenericConstraints?.Select (gc => JavaTypeNameToReference (api, gc, contextTypeParameters)), tn.ArrayPart);
			var primitive = JavaTypeReference.GetSpecialType (tn.DottedName);
			if (primitive != null)
				return tn.ArrayPart == null && tn.GenericConstraints == null ? primitive : new JavaTypeReference (primitive, tn.ArrayPart, tn.BoundsType, tn.GenericConstraints?.Select (gc => JavaTypeNameToReference (api, gc, contextTypeParameters)));
			var type = api.FindNonGenericType (tn.FullNameNonGeneric);
			return new JavaTypeReference (type,
				tn.GenericArguments != null ? tn.GenericArguments.Select (_ => api.JavaTypeNameToReference (_, contextTypeParameters)).ToArray () : null,
				tn.ArrayPart);
		}
		
		public static JavaType FindNonGenericType (this JavaApi api, string? name)
		{
			// Given a type name like 'android.graphics.BitmapFactory.Options'
			// We're going to search for:
			// - Pkg: android.graphics.BitmapFactory  Type: Options
			// - Pkg: android.graphics                Type: BitmapFactory.Options
			// - Pkg: android                         Type: graphics.BitmapFactory.Options
			// etc.  We will short-circuit as soon as we find a match
			var index = name?.LastIndexOf ('.') ?? -1;

			while (index > 0) {
				var ns = name!.Substring (0, index);
				var type_name = name.Substring (index + 1);

				if (api.Packages.TryGetValue (ns, out var pkg)) {
					if (pkg.Types.TryGetValue (type_name, out var type))
						return type.First ();
				}

				index = name.LastIndexOf ('.', index - 1);
			}

			// See if it's purely a C# type
			var ret = ManagedType.DummyManagedPackages
				.SelectMany (p => p.AllTypes)
				.FirstOrDefault (t => t.FullName == name);

			if (ret == null) {
				// We moved this type to "mono.android.app.IntentService" which makes this
				// type resolution fail if a user tries to reference it in Java.
				if (name == "android.app.IntentService")
					return FindNonGenericType (api, "mono.android.app.IntentService");

				throw new JavaTypeResolutionException (string.Format ("Type '{0}' was not found.", name));
			}

			return ret;
		}

		public static void Resolve (this JavaApi api)
		{
			while (true) {
				bool errors = false;
				foreach (var t in api.AllPackages.SelectMany (p => p.AllTypes).OfType<JavaClass> ().ToArray ())
					try {
						t.Resolve ();
					}
					catch (JavaTypeResolutionException ex) {
						Log.LogError ("Error while processing type '{0}': {1}", t, ex.Message);
						errors = true;
						t.Parent?.RemoveType (t);
					}
				foreach (var t in api.AllPackages.SelectMany (p => p.AllTypes).OfType<JavaInterface> ().ToArray ())
					try {
						t.Resolve ();
					} catch (JavaTypeResolutionException ex) {
						Log.LogError ("Error while processing type '{0}': {1}", t, ex.Message);
						errors = true;
						t.Parent?.RemoveType (t);
					}
				if (!errors)
					break;
			}
		}
		
		static void ResolveType (this JavaType type)
		{
			if (type.TypeParameters != null)
				type.TypeParameters.Resolve (type.GetApi (), type.TypeParameters);
			foreach (var t in type.Implements) {
				if (t.NameGeneric == null)
					continue;
				t.ResolvedName = type.GetApi ().Parse (t.NameGeneric, type.TypeParameters);
			}
			
			foreach (var m in type.Members.OfType<JavaField> ().ToArray ())
				ResolveWithTryCatch (m.Resolve, m);
			foreach (var m in type.Members.OfType<JavaMethod> ().ToArray ())
				ResolveWithTryCatch (m.Resolve, m);
		}
		
		public static void Resolve (this JavaClass c)
		{
			if (c.ExtendsGeneric != null)
				c.ResolvedExtends = c.GetApi ().Parse (c.ExtendsGeneric, c.TypeParameters);
			c.ResolveType ();
			foreach (var m in c.Members.OfType<JavaConstructor> ().ToArray ())
				ResolveWithTryCatch (() => m.Resolve (), m);
		}
		
		static void ResolveWithTryCatch (Action resolve, JavaMember m)
		{
			try {
				resolve ();
			} catch (JavaTypeResolutionException ex) {
				Log.LogError ("Error while processing '{0}' in '{1}': {2}", m, m.Parent, ex.Message);
				m.Parent?.Members.Remove (m);
			}
		}
		
		public static void Resolve (this JavaInterface i)
		{
			i.ResolveType ();
		}
		
		public static void Resolve (this JavaField f)
		{
			if (f.TypeGeneric == null)
				return;
			f.ResolvedType = f.GetApi ().Parse (f.TypeGeneric, f.Parent?.TypeParameters);
		}
		
		static void ResolveMethodBase (this JavaMethodBase m)
		{
			if (m.TypeParameters != null)
				m.TypeParameters.Resolve (m.GetApi (), m.TypeParameters);
			foreach (var p in m.Parameters) {
				if (p.Type == null)
					continue;
				p.ResolvedType = m.GetApi ().Parse (p.Type, m.Parent?.TypeParameters, m.TypeParameters);
			}
		}
		
		public static void Resolve (this JavaMethod m)
		{
			m.ResolveMethodBase ();
			if (m.Return == null)
				return;
			m.ResolvedReturnType = m.GetApi ().Parse (m.Return, m.Parent?.TypeParameters, m.TypeParameters);
		}
		
		public static void Resolve (this JavaConstructor c)
		{
			c.ResolveMethodBase ();
		}
		
		static void Resolve (this JavaTypeParameters tp, JavaApi api, params JavaTypeParameters?[] additionalTypeParameters)
		{
			foreach (var t in tp.TypeParameters) {
				if (t.GenericConstraints == null || t.GenericConstraints.GenericConstraints == null)
					continue;
				foreach (var g in t.GenericConstraints.GenericConstraints) {
					if (g.Type == null)
						 continue;
					try {
						g.ResolvedType = api.Parse (g.Type, additionalTypeParameters);
					}
					catch (JavaTypeResolutionException ex) {
						Log.LogDebug ("Warning: failed to resolve generic constraint: '{0}': {1}", g.Type, ex.Message);
					}
				}
			}
		}
	}
}
