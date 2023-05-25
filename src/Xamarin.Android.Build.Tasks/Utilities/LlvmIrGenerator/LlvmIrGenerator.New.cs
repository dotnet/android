using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once everything is migrated to the LLVM.IR namespace
	using LlvmIrAddressSignificance = LLVMIR.LlvmIrAddressSignificance;
	using LlvmIrLinkage = LLVMIR.LlvmIrLinkage;
	using LlvmIrRuntimePreemption = LLVMIR.LlvmIrRuntimePreemption;
	using LlvmIrVisibility = LLVMIR.LlvmIrVisibility;

	partial class LlvmIrGenerator
	{
		sealed class BasicType
		{
			public readonly string Name;
			public readonly ulong Size;

			public BasicType (string name, ulong size)
			{
				Name = name;
				Size = size;
			}
		}

		const string IRPointerType = "ptr";

		static readonly Dictionary<Type, BasicType> basicTypeMap = new Dictionary<Type, BasicType> {
			{ typeof (bool),   new ("i8",     1) },
			{ typeof (byte),   new ("i8",     1) },
			{ typeof (char),   new ("i16",    2) },
			{ typeof (sbyte),  new ("i8",     1) },
			{ typeof (short),  new ("i16",    2) },
			{ typeof (ushort), new ("i16",    2) },
			{ typeof (int),    new ("i32",    4) },
			{ typeof (uint),   new ("i32",    4) },
			{ typeof (long),   new ("i64",    8) },
			{ typeof (ulong),  new ("i64",    8) },
			{ typeof (float),  new ("float",  4) },
			{ typeof (double), new ("double", 8) },
			{ typeof (void),   new ("void",   0) },
		};

		public string FilePath           { get; }
		public string FileName           { get; }

		LlvmIrModuleTarget target;

		protected LlvmIrGenerator (string filePath, LlvmIrModuleTarget target)
		{
			FilePath = Path.GetFullPath (filePath);
			FileName = Path.GetFileName (filePath);
			this.target = target;
		}

		public static LlvmIrGenerator Create (AndroidTargetArch arch, string fileName)
		{
			return arch switch {
				AndroidTargetArch.Arm    => new LlvmIrGenerator (fileName, new LlvmIrModuleArmV7a ()),
				AndroidTargetArch.Arm64  => new LlvmIrGenerator (fileName, new LlvmIrModuleAArch64 ()),
				AndroidTargetArch.X86    => new LlvmIrGenerator (fileName, new LlvmIrModuleX86 ()),
				AndroidTargetArch.X86_64 => new LlvmIrGenerator (fileName, new LlvmIrModuleX64 ()),
				_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
			};
		}

		public void Generate (TextWriter writer, LlvmIrModule module)
		{
			if (!String.IsNullOrEmpty (FilePath)) {
				WriteCommentLine (writer, $" ModuleID = '{FileName}'");
				writer.WriteLine ($"source_filename = \"{FileName}\"");
			}

			writer.WriteLine (target.DataLayout.Render ());
			writer.WriteLine ($"target triple = \"{target.Triple}\"");
			writer.WriteLine ();
			WriteExternalFunctionDeclarations (writer, module);

			// Bottom of file
			WriteAttributeSets (writer, module);
		}

		//
		// Functions syntax: https://llvm.org/docs/LangRef.html#functions
		//
		void WriteExternalFunctionDeclarations (TextWriter writer, LlvmIrModule module)
		{
			if (module.ExternalFunctions == null || module.ExternalFunctions.Count == 0) {
				return;
			}

			module.ExternalFunctions.Sort ((LlvmIrFunction a, LlvmIrFunction b) => a.Signature.Name.CompareTo (b.Signature.Name));

			foreach (LlvmIrFunction func in module.ExternalFunctions) {
				// Must preserve state between calls, different targets may modify function state differently (e.g. set different parameter flags)
				ILlvmIrFunctionState funcState = func.SaveState ();

				foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
					target.SetParameterFlags (parameter);
				}

				WriteFunctionAttributesComment (writer, func);
				writer.Write ("declare ");
				WriteFunctionDeclarationLeadingDecorations (writer, func);
				WriteFunctionSignature (writer, func, writeParameterNames: false);
				WriteFunctionDeclarationTrailingDecorations (writer, func);
				writer.WriteLine ();

				func.RestoreState (funcState);
			}
		}

		void WriteFunctionAttributesComment (TextWriter writer, LlvmIrFunction func)
		{
			if (func.AttributeSet == null) {
				return;
			}

			writer.WriteLine ();
			WriteCommentLine (writer, $"Function attributes: {func.AttributeSet.Render ()}");
		}

		void WriteFunctionDeclarationLeadingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (writer, func, declaration: true);
		}

		void WriteFunctionDefinitionLeadingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionLeadingDecorations (writer, func, declaration: false);
		}

		void WriteFunctionLeadingDecorations (TextWriter writer, LlvmIrFunction func, bool declaration)
		{
			if (func.Linkage != LlvmIrLinkage.Default) {
				writer.Write (llvmLinkage[func.Linkage]);
				writer.Write (' ');
			}

			if (!declaration && func.RuntimePreemption != LlvmIrRuntimePreemption.Default) {
				writer.Write (llvmRuntimePreemption[func.RuntimePreemption]);
				writer.Write (' ');
			}

			if (func.Visibility != LlvmIrVisibility.Default) {
				writer.Write (llvmVisibility[func.Visibility]);
				writer.Write (' ');
			}
		}

		void WriteFunctionDeclarationTrailingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (writer, func, declaration: true);
		}

		void WriteFunctionDefinitionTrailingDecorations (TextWriter writer, LlvmIrFunction func)
		{
			WriteFunctionTrailingDecorations (writer, func, declaration: false);
		}

		void WriteFunctionTrailingDecorations (TextWriter writer, LlvmIrFunction func, bool declaration)
		{
			if (func.AddressSignificance != LlvmIrAddressSignificance.Default) {
				writer.Write ($" {llvmAddressSignificance[func.AddressSignificance]}");
			}

			if (func.AttributeSet != null) {
				writer.Write ($" #{func.AttributeSet.Number}");
			}
		}

		void WriteFunctionSignature (TextWriter writer, LlvmIrFunction func, bool writeParameterNames)
		{
			writer.Write (MapToIRType (func.Signature.ReturnType));
			writer.Write (" @");
			writer.Write (func.Signature.Name);
			writer.Write ('(');

			bool first = true;
			foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
				if (!first) {
					writer.Write (", ");
				} else {
					first = false;
				}

				writer.Write (MapToIRType (parameter.Type));
				WriteParameterAttributes (writer, parameter);
				if (writeParameterNames) {
					if (String.IsNullOrEmpty (parameter.Name)) {
						throw new InvalidOperationException ($"Internal error: parameter must have a name");
					}
					writer.Write (" %"); // Function arguments are always local variables
					writer.Write (parameter.Name);
				}
			}

			writer.Write (')');
		}

		void WriteParameterAttributes (TextWriter writer, LlvmIrFunctionParameter parameter)
		{
			var attributes = new List<string> ();
			if (AttributeIsSet (parameter.ImmArg)) {
				attributes.Add ("immarg");
			}

			if (AttributeIsSet (parameter.AllocPtr)) {
				attributes.Add ("allocptr");
			}

			if (AttributeIsSet (parameter.NoCapture)) {
				attributes.Add ("nocapture");
			}

			if (AttributeIsSet (parameter.NonNull)) {
				attributes.Add ("nonnull");
			}

			if (AttributeIsSet (parameter.NoUndef)) {
				attributes.Add ("noundef");
			}

			if (AttributeIsSet (parameter.ReadNone)) {
				attributes.Add ("readnone");
			}

			if (AttributeIsSet (parameter.SignExt)) {
				attributes.Add ("signext");
			}

			if (AttributeIsSet (parameter.ZeroExt)) {
				attributes.Add ("zeroext");
			}

			if (parameter.Align.HasValue) {
				attributes.Add ($"align({parameter.Align.Value})");
			}

			if (parameter.Dereferenceable.HasValue) {
				attributes.Add ($"dereferenceable({parameter.Dereferenceable.Value})");
			}

			if (attributes.Count == 0) {
				return;
			}

			writer.Write (' ');
			writer.Write (String.Join (" ", attributes));

			bool AttributeIsSet (bool? attr) => attr.HasValue && attr.Value;
		}

		void WriteAttributeSets (TextWriter writer, LlvmIrModule module)
		{
			if (module.AttributeSets == null || module.AttributeSets.Count == 0) {
				return;
			}

			writer.WriteLine ();
			module.AttributeSets.Sort ((LlvmIrFunctionAttributeSet a, LlvmIrFunctionAttributeSet b) => a.Number.CompareTo (b.Number));

			foreach (LlvmIrFunctionAttributeSet attrSet in module.AttributeSets) {
				// Must not modify the original set, it is shared with other targets.
				var targetSet = new LlvmIrFunctionAttributeSet (attrSet);
				target.AddTargetSpecificAttributes (targetSet);

				IList<LlvmIrFunctionAttribute>? privateTargetSet = attrSet.GetPrivateTargetAttributes (target.TargetArch);
				if (privateTargetSet != null) {
					targetSet.Add (privateTargetSet);
				}

				writer.WriteLine ($"attributes #{targetSet.Number} {{ {targetSet.Render ()} }}");
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

		string MapToIRType (Type type)
		{
			if (basicTypeMap.TryGetValue (type, out BasicType typeDesc)) {
				return typeDesc.Name;
			}

			// if it's not a basic type, then it's an opaque pointer
			return IRPointerType;
		}
	}
}
