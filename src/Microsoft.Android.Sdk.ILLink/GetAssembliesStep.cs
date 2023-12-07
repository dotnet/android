using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using System;
using System.Linq;
using Xamarin.Android.Tasks;
using System.Collections.Generic;
using Mono.Cecil.Cil;

namespace MonoDroid.Tuner
{
	public class GetAssembliesStep : BaseStep
	{
		AndroidLinkConfiguration config = null;

		protected override void Process ()
		{
			config = AndroidLinkConfiguration.GetInstance (Context);
		}

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			if (config == null)
				return;
			config.Assemblies.Add (assembly);
		}
	}
}
