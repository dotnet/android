using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Cecil;

namespace Mono.Tuner {

	public class PreserveDynamicTypes : BaseSubStep {
		
		public override SubStepTargets Targets {
			get { return SubStepTargets.Assembly ; }
		}
		
		bool preserve_dynamic;
		List<AssemblyDefinition> saved = new List<AssemblyDefinition> ();

		void PreserveLibrary (AssemblyDefinition assembly)
		{
			var action = Annotations.GetAction (assembly);
			ResolveFromAssemblyStep.ProcessLibrary (context, assembly);
			if (action == AssemblyAction.Link)
				Annotations.SetAction (assembly, action);
		}

		public override void ProcessAssembly (AssemblyDefinition assembly)
		{
			// Preserve dynamic dependencies only when Microsoft.CSharp is referenced.
			switch (assembly.Name.Name) {
			case "Microsoft.CSharp":
				preserve_dynamic = true;
				foreach (var savedAssembly in saved)
					PreserveLibrary (savedAssembly);
				PreserveLibrary (assembly);
				break;
			case "Mono.CSharp":
			case "System.Core":
				if (preserve_dynamic)
					PreserveLibrary (assembly);
				else
					saved.Add (assembly);
				break;
			}
		}
	}
}
