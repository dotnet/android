using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once everything is migrated to the LLVM.IR namespace
	using LlvmIrAddressSignificance = LLVMIR.LlvmIrAddressSignificance;
	using LlvmIrLinkage = LLVMIR.LlvmIrLinkage;
	using LlvmIrRuntimePreemption = LLVMIR.LlvmIrRuntimePreemption;
	using LlvmIrVisibility = LLVMIR.LlvmIrVisibility;

	partial class LlvmIrModule
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
			attrSet.Number = (uint)attributeSets.Count;
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

			foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
				Target.SetParameterFlags (parameter);
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
			WriteExternalFunctionDeclarations (writer);

			// Bottom of file
			WriteAttributeSets (writer);
		}

		//
		// Functions syntax: https://llvm.org/docs/LangRef.html#functions
		//
		void WriteExternalFunctionDeclarations (TextWriter writer)
		{
			if (externalFunctions == null || externalFunctions.Count == 0) {
				return;
			}

			List<LlvmIrFunction> list = externalFunctions.Values.ToList ();
			list.Sort ((LlvmIrFunction a, LlvmIrFunction b) => a.Signature.Name.CompareTo (b.Signature.Name));

			foreach (LlvmIrFunction func in list) {
				WriteFunctionAttributesComment (writer, func);
				writer.Write ("declare ");
				WriteFunctionDeclarationLeadingDecorations (writer, func);
				WriteFunctionSignature (writer, func, writeParameterNames: false);
				WriteFunctionDeclarationTrailingDecorations (writer, func);
				writer.WriteLine ();
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

		void WriteAttributeSets (TextWriter writer)
		{
			if (attributeSets == null || attributeSets.Count == 0) {
				return;
			}

			writer.WriteLine ();
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
