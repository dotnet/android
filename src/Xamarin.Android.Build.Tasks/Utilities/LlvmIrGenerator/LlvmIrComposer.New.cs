using System;
using System.IO;
using System.IO.Hashing;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	abstract class LlvmIrComposer
	{
		bool constructed;

		protected abstract void Construct (LlvmIrModule module);

		public LlvmIrModule Construct ()
		{
			var module = new LlvmIrModule ();
			Construct (module);
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
		}
	}
}
