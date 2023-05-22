using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	abstract class LlvmIrModule
	{
		public abstract LlvmIrDataLayout DataLayout  { get; }
		public abstract string TargetTriple          { get; }
		public abstract AndroidTargetArch TargetArch { get; }

		public string FilePath                       { get; }
		public string FileName                       { get; }

		HashSet<LlvmFunctionAttributeSet>? attributeSets;

		protected LlvmIrModule (string filePath)
		{
			FilePath = Path.GetFullPath (filePath);
			FileName = Path.GetFileName (filePath);
		}

		public static LlvmIrModule Create (AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			return arch switch {
				AndroidTargetArch.Arm => new LlvmIrModuleArmV7a (fileName),
				AndroidTargetArch.Arm64 => new LlvmIrModuleAArch64 (fileName),
				AndroidTargetArch.X86 => new LlvmIrModuleX86 (fileName),
				AndroidTargetArch.X86_64 => new LlvmIrModuleX64 (fileName),
				_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
			};
		}

		public void Generate (TextWriter writer)
		{
			if (!String.IsNullOrEmpty (FilePath)) {
				WriteCommentLine (writer, $" ModuleID = '{FileName}'");
				writer.WriteLine ($"source_filename = \"{FileName}\"");
			}

			writer.WriteLine (DataLayout.Render ());
			writer.WriteLine ($"target triple = \"{TargetTriple}\"");
			writer.WriteLine ();


			WriteAttributeSets (writer);
		}

		void WriteAttributeSets (TextWriter writer)
		{
			if (attributeSets == null || attributeSets.Count == 0) {
				return;
			}

			List<LlvmFunctionAttributeSet> list = attributeSets.ToList ();
			list.Sort ((LlvmFunctionAttributeSet a, LlvmFunctionAttributeSet b) => a.Number.CompareTo (b.Number));

			foreach (LlvmFunctionAttributeSet attrSet in attributeSets) {
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
