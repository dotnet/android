using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Xamarin.Android.Tools.Aidl
{
	public class BindingDatabase
	{
		Dictionary<string,string> nsmap;
		Dictionary<string,string> regs;
		List<AssemblyDefinition> assems = new List<AssemblyDefinition> ();
		
		public BindingDatabase (IEnumerable<string> assemblies, Func<string,AssemblyDefinition> resolveAssembly)
		{
			foreach (var assemfile in assemblies) {
				var assem = resolveAssembly (assemfile);
				if (assem == null)
					throw new InvalidOperationException ("Failed to resolve specified assembly");
				assems.Add (assem);
			}
			Initialize (assemblies, resolveAssembly);
		}
		
		public BindingDatabase (IEnumerable<AssemblyDefinition> assems)
		{
			this.assems.AddRange (assems);
			Initialize (null, null);
		}
		
		public IDictionary<string,string> NamespaceMappings {
			get { return nsmap; }
		}
		
		public IDictionary<string,string> RegisteredTypes {
			get { return regs; }
		}
		
		void Initialize (IEnumerable<string> assemblies, Func<string,AssemblyDefinition> resolveAssembly)
		{
			var d = new Dictionary<string,string> ();
			nsmap = d;
			var r = new Dictionary<string,string> ();
			regs = r;

			foreach (var assem in assems) {
				if (!assem.CustomAttributes.Any (a => a.AttributeType.FullName != "Android.Runtime.NamespaceMappingAttribute"))
					continue; // irrelevant assembly.
				foreach (var att in assem.CustomAttributes) {
					if (att.AttributeType.FullName != "Android.Runtime.NamespaceMappingAttribute")
						continue;
					string java = (string) att.Properties.First (p => p.Name == "Java").Argument.Value;
					string cs = (string) att.Properties.First (p => p.Name == "Managed").Argument.Value;
					d [java] = cs;
				}
				foreach (var md in assem.Modules)
					foreach (var td in md.Types.Where (t => t.IsPublic || t.IsNestedPublic))
						foreach (var att in td.CustomAttributes.Where (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute"))
							r [((string) att.ConstructorArguments [0].Value).Replace ('/', '.').Replace ('$', '.')] = td.FullName;
			}
		}
	}
}

