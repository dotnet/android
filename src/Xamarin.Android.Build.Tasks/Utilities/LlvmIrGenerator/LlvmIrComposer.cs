using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
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
			Init ();

			LlvmIrGenerator generator = LlvmIrGenerator.Create (TargetArch, output, fileName);

			MapStructures (generator);
			generator.WriteFileTop ();
			generator.WriteStructureDeclarations ();
			Write (generator);
			generator.WriteFileEnd ();
		}

		/// <summary>
		/// Initialize the composer. It needs to allocate and populate all the structures that
		/// are used by the composer, before they can be mapped by the generator. Essentially,
		/// the implementation should prepare its full state for writing.
		/// </summary>
		protected abstract void Init ();

		/// <summary>
		/// Maps all the structures used to internal LLVM IR representation. Every structure MUST
		/// be mapped.
		/// </summary>
		protected abstract void MapStructures (LlvmIrGenerator generator);
		protected abstract void Write (LlvmIrGenerator generator);
	}
}
