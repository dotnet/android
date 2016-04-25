using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class RemoveLibraryResourceZip : BaseStep {
		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			foreach (var mod in assembly.Modules) {
				var lres = mod.Resources.FirstOrDefault (r => r.Name == "__AndroidLibraryProjects__.zip");
				if (lres != null)
					mod.Resources.Remove (lres);
			}
		}
	}
}
