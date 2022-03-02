using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	abstract class LlvmIrComposer
	{
		protected AndroidTargetArch TargetArch { get; }

		protected LlvmIrComposer (AndroidTargetArch arch)
		{
			TargetArch = arch;
		}

		public void Write (StreamWriter output, string fileName)
		{
			LlvmIrGenerator generator = LlvmIrGenerator.Create (TargetArch, output, fileName);

			generator.WriteFileTop ();
			Write (generator);
			generator.WriteFileEnd ();
		}

		protected abstract void Write (LlvmIrGenerator generator);
	}
}
