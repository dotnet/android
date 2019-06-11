using System;
using System.Collections.Generic;

namespace MonoDroid.Generation
{
	public class NamespaceMapping
	{
		Dictionary<string,string> mappings = new Dictionary<string,string> ();
		
		public NamespaceMapping (IEnumerable<GenBase> gens)
		{
			foreach (GenBase gen in gens)
				if (!mappings.ContainsKey (gen.PackageName))
					mappings [gen.PackageName] = gen.Namespace;
		}

		bool IsGeneratable { get { return true; } }
		
		public void Generate (CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			using (var sw = gen_info.OpenStream (opt.GetFileName ("__NamespaceMapping__"))) {
				foreach (var p in mappings)
					sw.WriteLine ("[assembly:global::Android.Runtime.NamespaceMapping (Java = \"{0}\", Managed=\"{1}\")]",
					              p.Key, p.Value);
			}
		}
	}
}

