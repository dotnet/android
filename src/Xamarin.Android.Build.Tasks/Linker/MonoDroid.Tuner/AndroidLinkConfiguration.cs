using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using System;
using System.Linq;
using Xamarin.Android.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mono.Cecil.Cil;

namespace MonoDroid.Tuner
{
	public class AndroidLinkConfiguration {
		public List<AssemblyDefinition> Assemblies { get; private set; } = new List<AssemblyDefinition> ();

		static ConditionalWeakTable<LinkContext, AndroidLinkConfiguration> configurations = new ConditionalWeakTable<LinkContext, AndroidLinkConfiguration> ();

		public static AndroidLinkConfiguration GetInstance (LinkContext context)
		{
			if (!configurations.TryGetValue (context, out AndroidLinkConfiguration config)) {
				config = new AndroidLinkConfiguration ();
				configurations.Add (context, config);
			}
			return config;
		}
	}
}
