using System;
using System.IO;
using System.IO.Hashing;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	abstract class LlvmIrComposer
	{
		bool constructed;

		protected readonly Action<string> logger;

		protected LlvmIrComposer (Action<string> logger)
		{
			this.logger = logger;
		}

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
		}

		public static ulong GetXxHash (string str, bool is64Bit)
		{
			byte[] stringBytes = Encoding.UTF8.GetBytes (str);
			if (is64Bit) {
				return XxHash64.HashToUInt64 (stringBytes);
			}

			return (ulong)XxHash32.HashToUInt32 (stringBytes);
		}

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
