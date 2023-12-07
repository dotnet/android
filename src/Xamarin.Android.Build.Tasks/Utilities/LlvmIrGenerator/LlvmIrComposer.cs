using System;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	abstract class LlvmIrComposer
	{
		bool constructed;

		protected abstract void Construct (LlvmIrModule module);

		public LlvmIrModule Construct ()
		{
			var module = new LlvmIrModule ();
			Construct (module);
			module.AfterConstruction ();
			constructed = true;

			return module;
		}

		public void Generate (LlvmIrModule module, AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			if (!constructed) {
				throw new InvalidOperationException ($"Internal error: module not constructed yet. Was Constrict () called?");
			}

			LlvmIrGenerator generator = LlvmIrGenerator.Create (arch, fileName);
			generator.Generate (output, module);
			output.Flush ();

			CleanupAfterGeneration (arch);
		}

		protected virtual void CleanupAfterGeneration (AndroidTargetArch arch)
		{}

		protected LlvmIrGlobalVariable EnsureGlobalVariable (LlvmIrVariable variable)
		{
			var gv = variable as LlvmIrGlobalVariable;
			if (gv == null) {
				throw new InvalidOperationException ("Internal error: global variable expected");
			}

			return gv;
		}
	}
}
