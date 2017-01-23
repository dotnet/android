using System;
using System.Collections;
using System.Linq;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Mono.Linker;
using Mono.Linker.Steps;

namespace MonoDroid.Tuner
{
	public class MonoDroidSweepStep : SweepStep
	{
		protected override void Process ()
		{
			base.Process ();

			var assemblies = Context.GetAssemblies ();
			foreach (var assembly in assemblies) {
				AssemblyAction currentAction = Annotations.GetAction (assembly);

				if ((currentAction == AssemblyAction.Link) || (currentAction == AssemblyAction.Save)) {
					// if we save (only or by linking) then unmarked exports (e.g. forwarders) must be cleaned
					// or they can point to nothing which will break later (e.g. when re-loading for stripping IL)
					// reference: https://bugzilla.xamarin.com/show_bug.cgi?id=36577
					if (assembly.MainModule.HasExportedTypes)
						SweepUnmarked (assembly.MainModule.ExportedTypes);
				}
			}
		}

		void SweepUnmarked (IList list)
		{
			for (int i = 0; i < list.Count; i++)
				if (!Annotations.IsMarked ((IMetadataTokenProvider) list [i]))
					list.RemoveAt (i--);
		}
	}
}
