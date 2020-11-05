using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiOverrideMarkerExtensions
	{

		public static void MarkOverrides (this JavaApi api)
		{
			api.MarkOverrides (new HashSet<JavaClass> ());
		}

		public static void MarkOverrides (this JavaApi api, HashSet<JavaClass> doneList)
		{
			foreach (var kls in api.Packages.SelectMany (p => p.Types).OfType<JavaClass> ())
				kls.MarkOverrides (doneList);
		}
		
		static void MarkOverrides (this JavaClass cls, HashSet<JavaClass> doneList)
		{
			if (doneList.Contains (cls))
				return;
			doneList.Add (cls);

			var baseClass = cls.ResolvedExtends == null ? null :cls.ResolvedExtends.ReferencedType as JavaClass;
			if (baseClass != null)
				baseClass.MarkOverrides (doneList);
			
			foreach (var method in cls.Members.OfType<JavaMethod> ())
				cls.MarkBaseMethod (method);
		}
		
		static void MarkBaseMethod (this JavaClass cls, JavaMethod method)
		{
			JavaClass? k = cls;
			while (true) {
				k = k.ResolvedExtends != null ? k.ResolvedExtends.ReferencedType as JavaClass : null;
				if (k == null)
					break;
				
				// first we collect base method candidates by name (which is absolutely required!)
				var candidates = k.Members.OfType<JavaMethod> ().Where (_ => _.Name == method.Name);
				// Then we find exact parameter type matches.
				// No need to check returns. We only care about Java.
				var candidate = candidates.FirstOrDefault (c => method.IsImplementing (c, cls.GenericInheritanceMapping ?? throw new InvalidOperationException ($"missing {nameof(cls.GenericInheritanceMapping)}!")));
				if (candidate != null) {
					method.BaseMethod = new JavaMethodReference (candidate);
					
					for (int i = 0; i < candidate.Parameters.Count; i++)
						if (candidate.Parameters [i].ResolvedType?.ReferencedTypeParameter != null &&
							    method.Parameters [i].ResolvedType?.ReferencedTypeParameter == null)
							method.Parameters [i].InstantiatedGenericArgumentName = candidate.Parameters [i].ResolvedType?.ReferencedTypeParameter?.Name;
					break;
				}
			}
		}
	}
}
