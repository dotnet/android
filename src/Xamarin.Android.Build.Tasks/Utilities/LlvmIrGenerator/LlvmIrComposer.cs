using System;
using System.IO;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Abstract base class for composing LLVM IR modules. Provides a framework for constructing and generating LLVM IR code.
	/// </summary>
	abstract class LlvmIrComposer
	{
		bool constructed;
		readonly LlvmIrTypeCache cache = new();

		protected readonly TaskLoggingHelper Log;

		/// <summary>
		/// Initializes a new instance of the <see cref="LlvmIrComposer"/> class.
		/// </summary>
		/// <param name="log">The task logging helper for logging messages.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="log"/> is null.</exception>
		protected LlvmIrComposer (TaskLoggingHelper log)
		{
			this.Log = log ?? throw new ArgumentNullException (nameof (log));
		}

		/// <summary>
		/// When overridden in a derived class, constructs the LLVM IR module by adding the necessary structures, functions, and variables.
		/// </summary>
		/// <param name="module">The LLVM IR module to construct.</param>
		protected abstract void Construct (LlvmIrModule module);

		/// <summary>
		/// Constructs the LLVM IR module by calling the derived class implementation and performing necessary finalization.
		/// </summary>
		/// <returns>The constructed LLVM IR module.</returns>
		public LlvmIrModule Construct ()
		{
			var module = new LlvmIrModule (cache, Log);
			Construct (module);
			module.AfterConstruction ();
			constructed = true;

			return module;
		}

		/// <summary>
		/// Generates LLVM IR code for the specified target architecture and writes it to the output stream.
		/// </summary>
		/// <param name="module">The LLVM IR module to generate code from.</param>
		/// <param name="arch">The target Android architecture.</param>
		/// <param name="output">The stream writer to write the generated LLVM IR code to.</param>
		/// <param name="fileName">The name of the file being generated.</param>
		/// <exception cref="InvalidOperationException">Thrown when the module has not been constructed yet.</exception>
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

		/// <summary>
		/// Performs any necessary cleanup after code generation for the specified architecture.
		/// </summary>
		/// <param name="arch">The target Android architecture that was used for generation.</param>
		protected virtual void CleanupAfterGeneration (AndroidTargetArch arch)
		{}

		/// <summary>
		/// Ensures that the specified variable is a global variable and returns it as such.
		/// </summary>
		/// <param name="variable">The variable to check and cast.</param>
		/// <returns>The variable cast as a global variable.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the variable is not a global variable.</exception>
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
