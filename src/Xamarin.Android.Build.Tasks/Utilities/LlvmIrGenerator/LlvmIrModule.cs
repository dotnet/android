using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	class LlvmIrModule
	{
		public string FilePath           { get; }
		public string FileName           { get; }
		public LlvmIrModuleTarget Target { get; }

		Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet>? attributeSets;
		Dictionary<LlvmIrFunction, LlvmIrFunction>? externalFunctions;

		protected LlvmIrModule (string filePath, LlvmIrModuleTarget target)
		{
			FilePath = Path.GetFullPath (filePath);
			FileName = Path.GetFileName (filePath);
			Target = target;
		}

		public static LlvmIrModule Create (AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			return arch switch {
				AndroidTargetArch.Arm    => new LlvmIrModule (fileName, new LlvmIrModuleArmV7a ()),
				AndroidTargetArch.Arm64  => new LlvmIrModule (fileName, new LlvmIrModuleAArch64 ()),
				AndroidTargetArch.X86    => new LlvmIrModule (fileName, new LlvmIrModuleX86 ()),
				AndroidTargetArch.X86_64 => new LlvmIrModule (fileName, new LlvmIrModuleX64 ()),
				_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
			};
		}

		/// <summary>
		/// Add a new attribute set.  The caller MUST use the returned value to refer to the set, instead of the one passed
		/// as parameter, since this function de-duplicates sets and may return a previously added one that's identical to
		/// the new one.
		/// </summary>
		public LlvmIrFunctionAttributeSet AddAttributeSet (LlvmIrFunctionAttributeSet attrSet)
		{
			if (attributeSets == null) {
				attributeSets = new Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet> ();
			}

			if (attributeSets.TryGetValue (attrSet, out LlvmIrFunctionAttributeSet existingSet)) {
				return existingSet;
			}
			attributeSets.Add (attrSet, attrSet);

			return attrSet;
		}

		/// <summary>
		/// Add a new external function declaration.  The caller MUST use the returned value to refer to the function, instead
		/// of the one passed as parameter, since this function de-duplicates function declarations and may return a previously
		/// added one that's identical to the new one.
		/// </summary>
		public LlvmIrFunction DeclareExternalFunction (LlvmIrFunction func)
		{
			if (externalFunctions == null) {
				externalFunctions = new Dictionary<LlvmIrFunction, LlvmIrFunction> ();
			}

			if (externalFunctions.TryGetValue (func, out LlvmIrFunction existingFunc)) {
				return existingFunc;
			}
			externalFunctions.Add (func, func);

			return func;
		}

		public void Generate (TextWriter writer)
		{
			if (!String.IsNullOrEmpty (FilePath)) {
				WriteCommentLine (writer, $" ModuleID = '{FileName}'");
				writer.WriteLine ($"source_filename = \"{FileName}\"");
			}

			writer.WriteLine (Target.DataLayout.Render ());
			writer.WriteLine ($"target triple = \"{Target.Triple}\"");
			writer.WriteLine ();

			// Bottom of file
			WriteAttributeSets (writer);
		}

		void WriteExternalFunctionDeclarations (TextWriter writer)
		{
			if (externalFunctions == null || externalFunctions.Count == 0) {
				return;
			}
		}

		void WriteAttributeSets (TextWriter writer)
		{
			if (attributeSets == null || attributeSets.Count == 0) {
				return;
			}

			List<LlvmIrFunctionAttributeSet> list = attributeSets.Keys.ToList ();
			list.Sort ((LlvmIrFunctionAttributeSet a, LlvmIrFunctionAttributeSet b) => a.Number.CompareTo (b.Number));

			foreach (LlvmIrFunctionAttributeSet attrSet in list) {
				writer.WriteLine ($"attributes #{attrSet.Number} {{ {attrSet.Render ()} }}");
			}
		}

		void WriteComment (TextWriter writer, string comment)
		{
			writer.Write (';');
			writer.Write (comment);
		}

		void WriteCommentLine (TextWriter writer, string comment)
		{
			WriteComment (writer, comment);
			writer.WriteLine ();
		}
	}
}
