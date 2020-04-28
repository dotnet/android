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
				sw.WriteLine ("using System;");
				sw.WriteLine ();

				foreach (var p in mappings)
					sw.WriteLine ("[assembly:global::Android.Runtime.NamespaceMapping (Java = \"{0}\", Managed=\"{1}\")]",
					              p.Key, p.Value);

				sw.WriteLine ();

				// delegate bool _JniMarshal_PPL_Z (IntPtr jnienv, IntPtr klass, IntPtr a);
				foreach (var jni in opt.GetJniMarshalDelegates ())
					sw.WriteLine ($"delegate {FromJniType (jni[jni.Length - 1])} {jni} (IntPtr jnienv, IntPtr klass{GetDelegateParameters (jni)});");
			}
		}

		string GetDelegateParameters (string jni)
		{
			var parameters = new List<string> ();

			jni = jni.Substring ("_JniMarshal_PP".Length);

			var index = 0;

			while (jni[index] != '_') {
				parameters.Add ($"{FromJniType (jni [index])} p{index}");
				index++;
			}

			if (parameters.Count == 0)
				return string.Empty;

			return ", " + string.Join (", ", parameters);
		}

		string FromJniType (char c)
		{
			switch (c) {
				case 'B': return "sbyte";
				case 'b': return "byte";
				case 'C': return "char";
				case 'D': return "double";
				case 'F': return "float";
				case 'I': return "int";
				case 'i': return "uint";
				case 'J': return "long";
				case 'j': return "ulong";
				case 'S': return "short";
				case 's': return "ushort";
				case 'Z': return "bool";
				case 'V': return "void";
				default:
					return "IntPtr"; ;
			}
		}
	}
}

