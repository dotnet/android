using System;
using System.Collections.Generic;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

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

				if (opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1) {
					foreach (var p in mappings) {
						sw.WriteLine ($"[assembly:global::Android.Runtime.NamespaceMapping (Java = \"{p.Key}\", Managed=\"{p.Value}\")]");
					}
					sw.WriteLine ();
				}

				// [UnmanagedFunctionPointer (CallingConvention.Winapi)]
				// delegate bool _JniMarshal_PPL_Z (IntPtr jnienv, IntPtr klass, IntPtr a);
				foreach (var jni in opt.GetJniMarshalDelegates ()) {
					sw.WriteLine ("[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]");
					sw.WriteLine ($"delegate {FromJniType (jni[jni.Length - 1])} {jni} (IntPtr jnienv, IntPtr klass{GetDelegateParameters (jni)});");
				}

				// [SupportedOSPlatform] only exists in .NET 5.0+, so we need to generate a
				// dummy one so earlier frameworks can compile.
				if (opt.CodeGenerationTarget == Xamarin.Android.Binder.CodeGenerationTarget.XAJavaInterop1) {
					sw.WriteLine ("#if !NET");
					sw.WriteLine ("namespace System.Runtime.Versioning {");
					sw.WriteLine ("    [System.Diagnostics.Conditional(\"NEVER\")]");
					sw.WriteLine ("    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]");
					sw.WriteLine ("    internal sealed class SupportedOSPlatformAttribute : Attribute {");
					sw.WriteLine ("        public SupportedOSPlatformAttribute (string platformName) { }");
					sw.WriteLine ("    }");
					sw.WriteLine ("}");
					sw.WriteLine ("#endif");
					sw.WriteLine ("");
				}
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

