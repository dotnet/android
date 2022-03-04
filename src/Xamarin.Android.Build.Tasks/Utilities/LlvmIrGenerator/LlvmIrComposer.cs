using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	abstract class LlvmIrComposer
	{
		protected AndroidTargetArch TargetArch { get; }

		protected LlvmIrComposer ()
		{}

		public void Write (AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			LlvmIrGenerator generator = LlvmIrGenerator.Create (arch, output, fileName);

			MapStructures (generator);
			generator.WriteFileTop ();
			generator.WriteStructureDeclarations ();
			Write (generator);
			generator.WriteFileEnd ();
		}

		/// <summary>
		/// Initialize the composer. It needs to allocate and populate all the structures that
		/// are used by the composer, before they can be mapped by the generator. The code here
		/// should initialize only the architecture-independent fields of structures etc to
		/// write. The composer is reused between architectures, and only the Write method is
		/// aware of which architecture is targetted.
		/// </summary>
		public abstract void Init ();

		/// <summary>
		/// Maps all the structures used to internal LLVM IR representation. Every structure MUST
		/// be mapped.
		/// </summary>
		protected abstract void MapStructures (LlvmIrGenerator generator);
		protected abstract void Write (LlvmIrGenerator generator);
	}
}
