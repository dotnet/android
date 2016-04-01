using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiOverrideMarkerExtensions
	{
		
		public static void MarkOverrides (this JavaApi api)
		{
			foreach (var kls in api.Packages.SelectMany (p => p.Types).OfType<JavaClass> ())
				kls.MarkOverrides ();
		}
		
		static void MarkOverrides (this JavaClass cls)
		{
			var baseClass = cls.ResolvedExtends == null ? null :cls.ResolvedExtends.ReferencedType as JavaClass;
			if (baseClass != null)
				baseClass.MarkOverrides ();
			
			foreach (var method in cls.Members.OfType<JavaMethod> ())
				cls.MarkBaseMethod (method);
/*
foreach (var m in cls.Members.OfType<JavaMethod> ().Where (_ => _.BaseMethod != null)) {
	var b = m.BaseMethod.Method;
	if (m.ExtendedJniSignature != b.ExtendedJniSignature)
		Console.WriteLine ("Method      {0}.{1}#{2}({3}) | {8}\n  overrides {4}.{5}#{6}({7}) | {9}",
			m.Parent.Parent.Name, m.Parent.Name, m.Name, string.Join (", ", m.Parameters.Select (p => p.Type)),
			b.Parent.Parent.Name, b.Parent.Name, b.Name, string.Join (", ", b.Parameters.Select (p => p.Type)),
			m.ExtendedJniSignature, b.ExtendedJniSignature);
}
foreach (var m in cls.Members.OfType<JavaMethod> ())
	foreach (var para in m.Parameters.Where (p => p.InstantiatedGenericArgumentName != null))
		Console.WriteLine ("Method {0}.{1}#{2}({3}) has generics-instantiated parameter {4} ({5} -> {6})",
			m.Parent.Parent.Name, m.Parent.Name, m.Name, string.Join (", ", m.Parameters.Select (p => p.Type)),
			para.Name, para.InstantiatedGenericArgumentName, para.Type);
*/			
		}
		
		static void MarkBaseMethod (this JavaClass cls, JavaMethod method)
		{
			JavaClass k = cls;
			while (true) {
				k = k.ResolvedExtends != null ? (JavaClass) k.ResolvedExtends.ReferencedType : null;
				if (k == null)
					break;
				
				// first we collect base method candidates by name (which is absolutely required!)
				var candidates = k.Members.OfType<JavaMethod> ().Where (_ => _.Name == method.Name);
				// Then we find exact parameter type matches.
				// No need to check returns. We only care about Java.
				var candidate = candidates.FirstOrDefault (c => method.IsImplementing (c, cls.GenericInheritanceMapping));
				if (candidate != null) {
					method.BaseMethod = new JavaMethodReference (candidate);
					
					for (int i = 0; i < candidate.Parameters.Count; i++)
						if (candidate.Parameters [i].ResolvedType.ReferencedTypeParameter != null &&
						    method.Parameters [i].ResolvedType.ReferencedTypeParameter == null)
							method.Parameters [i].InstantiatedGenericArgumentName = candidate.Parameters [i].ResolvedType.ReferencedTypeParameter.Name;
					break;
				}
			}
		}
	}
}
