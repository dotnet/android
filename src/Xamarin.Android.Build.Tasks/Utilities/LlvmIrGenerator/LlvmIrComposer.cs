using System;
using System.IO;
using System.IO.Hashing;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Base class for all classes which "compose" LLVM IR assembly.
	/// <summary>
	abstract class LlvmIrComposer
	{
		protected AndroidTargetArch TargetArch { get; }

		protected LlvmIrComposer ()
		{}

		public void Write (AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			LlvmIrGenerator generator = LlvmIrGenerator.Create (arch, output, fileName);

			InitGenerator (generator);
			MapStructures (generator);
			generator.WriteFileTop ();
			generator.WriteStructureDeclarations ();
			Write (generator);
			generator.WriteFunctionDeclarations ();
			generator.WriteFileEnd ();
		}

		protected static string GetAbiName (AndroidTargetArch arch)
		{
			return arch switch {
				AndroidTargetArch.Arm => "armeabi-v7a",
				AndroidTargetArch.Arm64 => "arm64-v8a",
				AndroidTargetArch.X86 => "x86",
				AndroidTargetArch.X86_64 => "x86_64",
				_ => throw new InvalidOperationException ($"Unsupported Android architecture: {arch}"),
			};
		}

		protected ulong HashName (string name, bool is64Bit)
		{
			byte[] nameBytes = Encoding.UTF8.GetBytes (name);
			if (is64Bit) {
				return XxHash64.HashToUInt64 (nameBytes);
			}

			return (ulong)XxHash32.HashToUInt32 (nameBytes);
		}

		protected virtual void InitGenerator (LlvmIrGenerator generator)
		{}

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

		/// <summary>
		/// Generate LLVM IR code from data structures initialized by <see cref="Init"/>.  This is
		/// called once per ABI, with the appropriate <paramref name="generator"/> for the target
		/// ABI.  If any ABI-specific initialization must be performed on the data structures to
		/// be written, it has to be done here (applies to e.g. constructs that require to know the
		/// native pointer size).
		/// </summary>
		protected abstract void Write (LlvmIrGenerator generator);
	}
}
