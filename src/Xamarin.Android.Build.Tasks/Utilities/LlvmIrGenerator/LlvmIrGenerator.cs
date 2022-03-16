using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	abstract class LlvmIrGenerator
	{
		ref struct StructureBodyWriterOptions
		{
			public readonly bool WriteFieldComment;
			public readonly string FieldIndent;
			public readonly string StructIndent;
			public readonly TextWriter? StructureOutput;
			public readonly TextWriter? StringsOutput;
			public readonly TextWriter? BuffersOutput;

			public StructureBodyWriterOptions (bool writeFieldComment, string fieldIndent = "", string structIndent = "",
			                                   TextWriter? structureOutput = null, TextWriter? stringsOutput = null, TextWriter? buffersOutput = null)
			{
				WriteFieldComment = writeFieldComment;
				FieldIndent = fieldIndent;
				StructIndent = structIndent;
				StructureOutput = structureOutput;
				StringsOutput = stringsOutput;
				BuffersOutput = buffersOutput;
			}
		}

		sealed class PackedStructureMember<T>
		{
			public readonly string ValueIRType;
			public readonly string? PaddingIRType;
			public readonly object? Value;
			public readonly bool IsPadded;
			public readonly StructureMemberInfo<T> MemberInfo;

			public PackedStructureMember (StructureMemberInfo<T> memberInfo, object? value, string? valueIRType = null, string? paddingIRType = null)
			{
				ValueIRType = valueIRType ?? memberInfo.IRType;
				Value = value;
				MemberInfo = memberInfo;
				PaddingIRType = paddingIRType;
				IsPadded = !String.IsNullOrEmpty (paddingIRType);
			}
		}

		static readonly Dictionary<Type, string> typeMap = new Dictionary<Type, string> {
			{ typeof (bool), "i8" },
			{ typeof (byte), "i8" },
			{ typeof (char), "i8" },
			{ typeof (sbyte), "i8" },
			{ typeof (short), "i16" },
			{ typeof (ushort), "i16" },
			{ typeof (int), "i32" },
			{ typeof (uint), "i32" },
			{ typeof (long), "i64" },
			{ typeof (ulong), "i64" },
			{ typeof (float), "float" },
			{ typeof (double), "double" },
			{ typeof (string), "i8*" },
			{ typeof (IntPtr), "i8*" },
		};

		// https://llvm.org/docs/LangRef.html#single-value-types
		static readonly Dictionary<Type, ulong> typeSizes = new Dictionary<Type, ulong> {
			{ typeof (bool), 1 },
			{ typeof (byte), 1 },
			{ typeof (char), 1 },
			{ typeof (sbyte), 1 },
			{ typeof (short), 2 },
			{ typeof (ushort), 2 },
			{ typeof (int), 4 },
			{ typeof (uint), 4 },
			{ typeof (long), 8 },
			{ typeof (ulong), 8 },
			{ typeof (float), 4 }, // floats are 32-bit
			{ typeof (double), 8 }, // doubles are 64-bit
		};

		static readonly Dictionary<LlvmIrLinkage, string> llvmLinkage = new Dictionary<LlvmIrLinkage, string> {
			{ LlvmIrLinkage.Default, String.Empty },
			{ LlvmIrLinkage.Private, "private" },
			{ LlvmIrLinkage.Internal, "internal" },
			{ LlvmIrLinkage.AvailableExternally, "available_externally" },
			{ LlvmIrLinkage.LinkOnce, "linkonce" },
			{ LlvmIrLinkage.Weak, "weak" },
			{ LlvmIrLinkage.Common, "common" },
			{ LlvmIrLinkage.Appending, "appending" },
			{ LlvmIrLinkage.ExternWeak, "extern_weak" },
			{ LlvmIrLinkage.LinkOnceODR, "linkonce_odr" },
			{ LlvmIrLinkage.External, "external" },
		};

		static readonly Dictionary<LlvmIrRuntimePreemption, string> llvmRuntimePreemption = new Dictionary<LlvmIrRuntimePreemption, string> {
			{ LlvmIrRuntimePreemption.Default, String.Empty },
			{ LlvmIrRuntimePreemption.DSOPreemptable, "dso_preemptable" },
			{ LlvmIrRuntimePreemption.DSOLocal, "dso_local" },
		};

		static readonly Dictionary<LlvmIrVisibility, string> llvmVisibility = new Dictionary<LlvmIrVisibility, string> {
			{ LlvmIrVisibility.Default, "default" },
			{ LlvmIrVisibility.Hidden, "hidden" },
			{ LlvmIrVisibility.Protected, "protected" },
		};

		static readonly Dictionary<LlvmIrAddressSignificance, string> llvmAddressSignificance = new Dictionary<LlvmIrAddressSignificance, string> {
			{ LlvmIrAddressSignificance.Default, String.Empty },
			{ LlvmIrAddressSignificance.Unnamed, "unnamed_addr" },
			{ LlvmIrAddressSignificance.LocalUnnamed, "local_unnamed_addr" },
		};

		static readonly Dictionary<LlvmIrWritability, string> llvmWritability = new Dictionary<LlvmIrWritability, string> {
			{ LlvmIrWritability.Constant, "constant" },
			{ LlvmIrWritability.Writable, "global" },
		};

		static readonly LlvmIrVariableOptions preAllocatedBufferVariableOptions = new LlvmIrVariableOptions {
			Writability = LlvmIrWritability.Writable,
			Linkage = LlvmIrLinkage.Internal,
		};

		string fileName;
		ulong stringCounter = 0;
		ulong structStringCounter = 0;
		ulong structBufferCounter = 0;

		List<IStructureInfo> structures = new List<IStructureInfo> ();

		protected abstract string DataLayout { get; }
		public abstract int PointerSize { get; }
		protected abstract string Triple { get; }

		public bool Is64Bit { get; }
		public TextWriter Output { get; }
		public AndroidTargetArch TargetArch { get; }

		protected string Indent => "\t";
		protected LlvmIrMetadataManager MetadataManager { get; }

		protected LlvmIrGenerator (AndroidTargetArch arch, TextWriter output, string fileName)
		{
			Output = output;
			MetadataManager = new LlvmIrMetadataManager ();
			TargetArch = arch;
			Is64Bit = arch == AndroidTargetArch.X86_64 || arch == AndroidTargetArch.Arm64;
			this.fileName = fileName;
		}

		public static LlvmIrGenerator Create (AndroidTargetArch arch, StreamWriter output, string fileName)
		{
			LlvmIrGenerator ret = Instantiate ();
			ret.Init ();
			return ret;

			LlvmIrGenerator Instantiate ()
			{
				return arch switch {
					AndroidTargetArch.Arm => new Arm32LlvmIrGenerator (arch, output, fileName),
					AndroidTargetArch.Arm64 => new Arm64LlvmIrGenerator (arch, output, fileName),
					AndroidTargetArch.X86 => new X86LlvmIrGenerator (arch, output, fileName),
					AndroidTargetArch.X86_64 => new X64LlvmIrGenerator (arch, output, fileName),
					_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
				};
			}
		}

		static string EnsureIrType (Type type)
		{
			if (!typeMap.TryGetValue (type, out string irType)) {
				throw new InvalidOperationException ($"Unsupported managed type {type}");
			}

			return irType;
		}

		static Type GetActualType (Type type)
		{
			// Arrays of types are handled elsewhere, so we obtain the array base type here
			if (type.IsArray) {
				return type.GetElementType ();
			}

			return type;
		}

		public static string MapManagedTypeToIR (Type type)
		{
			return EnsureIrType (GetActualType (type));
		}

		public string MapManagedTypeToIR (Type type, out ulong size)
		{
			Type actualType = GetActualType (type);
			string irType = EnsureIrType (actualType);
			if (!typeSizes.TryGetValue (actualType, out size)) {
				if (actualType == typeof (string) || actualType == typeof (IntPtr)) {
					size = (ulong)PointerSize;
				} else {
					throw new InvalidOperationException ($"Unsupported managed type {type}");
				}
			}

			return irType;
		}

		public string MapManagedTypeToIR<T> (out ulong size)
		{
			return MapManagedTypeToIR (typeof(T), out size);
		}

		public static string MapManagedTypeToNative (Type type)
		{
			Type baseType = GetActualType (type);

			if (baseType == typeof (bool)) return "bool";
			if (baseType == typeof (byte)) return "uint8_t";
			if (baseType == typeof (char)) return "char";
			if (baseType == typeof (sbyte)) return "int8_t";
			if (baseType == typeof (short)) return "int16_t";
			if (baseType == typeof (ushort)) return "uint16_t";
			if (baseType == typeof (int)) return "int32_t";
			if (baseType == typeof (uint)) return "uint32_t";
			if (baseType == typeof (long)) return "int64_t";
			if (baseType == typeof (ulong)) return "uint64_t";
			if (baseType == typeof (float)) return "float";
			if (baseType == typeof (double)) return "double";
			if (baseType == typeof (string)) return "char*";
			if (baseType == typeof (IntPtr)) return "void*";

			return type.GetShortName ();
		}

		public virtual void Init ()
		{
			LlvmIrMetadataItem flags = MetadataManager.Add ("llvm.module.flags");
			LlvmIrMetadataItem ident = MetadataManager.Add ("llvm.ident");

			var flagsFields = new List<LlvmIrMetadataItem> ();
			AddModuleFlagsMetadata (flagsFields);

			foreach (LlvmIrMetadataItem item in flagsFields) {
				flags.AddReferenceField (item.Name);
			}

			LlvmIrMetadataItem identValue = MetadataManager.AddNumbered ($"Xamarin.Android {XABuildConfig.XamarinAndroidBranch} @ {XABuildConfig.XamarinAndroidCommitHash}");
			ident.AddReferenceField (identValue.Name);
		}

		public StructureInfo<T> MapStructure<T> ()
		{
			Type t = typeof(T);
			if (!t.IsClass && !t.IsValueType) {
				throw new InvalidOperationException ($"{t} must be a class or a struct");
			}

			var ret = new StructureInfo<T> (this);
			structures.Add (ret);

			return ret;
		}

		TextWriter EnsureOutput (TextWriter? output)
		{
			return output ?? Output;
		}

		void WriteGlobalSymbolStart (string symbolName, LlvmIrVariableOptions options, TextWriter? output = null)
		{
			output = EnsureOutput (output);

			var sb = new StringBuilder (llvmLinkage[options.Linkage]);
			if (options.AddressSignificance != LlvmIrAddressSignificance.Default) {
				if (sb.Length > 0) {
					sb.Append (' ');
				}

				sb.Append (llvmAddressSignificance[options.AddressSignificance]);
			}

			if (sb.Length > 0) {
				sb.Append (' ');
			}

			sb.Append (llvmWritability[options.Writability]);

			output.Write ($"@{symbolName} = {sb.ToString ()} ");
		}

		object? GetTypedMemberValue<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, Type expectedType, object? defaultValue = null)
		{
			object? value = smi.GetValue (instance.Obj);
			if (value == null) {
				return defaultValue;
			}

			if (value.GetType () != expectedType) {
				throw new InvalidOperationException ($"Field '{smi.Info.Name}' of structure '{info.Name}' should have a value of '{expectedType}' type, instead it had a '{value.GetType ()}'");
			}

			if (expectedType == typeof(bool)) {
				return (bool)value ? 1 : 0;
			}

			return value;
		}

		bool MaybeWriteStructureString<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, TextWriter? output = null)
		{
			if (smi.MemberType != typeof(string)) {
				return false;
			}

			output = EnsureOutput (output);
			string? str = (string?)GetTypedMemberValue (info, smi, instance, typeof(string), null);
			if (str == null) {
				instance.AddPointerData (smi, null, 0);
				return false;
			}
			string variableName = WriteString ($"__{info.Name}_{smi.Info.Name}_{structStringCounter++}", str, out ulong size);
			instance.AddPointerData (smi, variableName, size);

			return true;
		}

		bool MaybeWritePreAllocatedBuffer<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, TextWriter? output = null)
		{
			if (!smi.Info.IsNativePointerToPreallocatedBuffer (out ulong bufferSize)) {
				return false;
			}

			if (smi.Info.UsesDataProvider ()) {
				bufferSize = info.GetBufferSizeFromProvider (smi, instance);
			}

			output = EnsureOutput (output);
			string irType = MapManagedTypeToIR (smi.MemberType);
			string variableName = $"__{info.Name}_{smi.Info.Name}_{structBufferCounter++}";

			WriteGlobalSymbolStart (variableName, preAllocatedBufferVariableOptions, output);
			ulong size = bufferSize * smi.BaseTypeSize;
			output.WriteLine ($"[{bufferSize} x {irType}] zeroinitializer, align {GetAggregateAlignment ((int)smi.BaseTypeSize, size)}");
			instance.AddPointerData (smi, variableName, size);
			return true;
		}

		bool WriteStructureArrayStart<T> (StructureInfo<T> info, IList<StructureInstance<T>>? instances, LlvmIrVariableOptions options, string? symbolName = null, string? initialComment = null, TextWriter? output = null)
		{
			if (options.IsGlobal && String.IsNullOrEmpty (symbolName)) {
				throw new ArgumentException ("must not be null or empty for global symbols", nameof (symbolName));
			}

			bool named = !String.IsNullOrEmpty (symbolName);
			if (named || !String.IsNullOrEmpty (initialComment)) {
				WriteEOL (output: output);
				WriteEOL (initialComment ?? symbolName, output);
			}

			if (named) {
				WriteGlobalSymbolStart (symbolName, options, output);
			}

			return named;
		}

		void WriteStructureArrayEnd<T> (StructureInfo<T> info, string? symbolName, ulong count, bool named, bool skipFinalComment = false, TextWriter? output = null)
		{
			output = EnsureOutput (output);

			output.Write ($", align {GetAggregateAlignment (info.MaxFieldAlignment, info.Size * count)}");
			if (named && !skipFinalComment) {
				WriteEOL ($"end of '{symbolName!}' array", output);
			} else {
				WriteEOL (output: output);
			}
		}

		/// <summary>
		/// Writes an array of <paramref name="count"/> zero-initialized entries
		/// </summary>
		public void WriteStructureArray<T> (StructureInfo<T> info, ulong count, LlvmIrVariableOptions options, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null)
		{
			bool named = WriteStructureArrayStart<T> (info, null, options, symbolName, initialComment);

			Output.Write ($"[{count} x %struct.{info.Name}] zeroinitializer");

			WriteStructureArrayEnd<T> (info, symbolName, (ulong)count, named, skipFinalComment: true);
		}

		public void WriteStructureArray<T> (StructureInfo<T> info, ulong count, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null)
		{
			WriteStructureArray<T> (info, count, LlvmIrVariableOptions.Default, symbolName, writeFieldComment, initialComment);
		}

		public void WriteStructureArray<T> (StructureInfo<T> info, IList<StructureInstance<T>>? instances, LlvmIrVariableOptions options, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null)
		{
			var arrayOutput = new StringWriter ();
			bool named = WriteStructureArrayStart<T> (info, instances, options, symbolName, initialComment, arrayOutput);
			int count = instances != null ? instances.Count : 0;

			arrayOutput.Write ($"[{count} x %struct.{info.Name}] ");
			if (instances != null) {
				var bodyWriterOptions = new StructureBodyWriterOptions (
					writeFieldComment: true,
					fieldIndent: $"{Indent}{Indent}",
					structIndent: Indent,
					structureOutput: arrayOutput,
					stringsOutput: info.HasStrings ? new StringWriter () : null,
					buffersOutput: info.HasPreAllocatedBuffers ? new StringWriter () : null
				);

				arrayOutput.WriteLine ("[");
				string fieldIndent = $"{Indent}{Indent}";
				for (int i = 0; i < count; i++) {
					StructureInstance<T> instance = instances[i];

					WriteStructureBody (info, instance, bodyWriterOptions);
					if (i < count - 1) {
						arrayOutput.Write (", ");
					}
					WriteEOL (output: arrayOutput);
				}
				arrayOutput.Write ("]");

				WriteBufferToOutput (bodyWriterOptions.StringsOutput);
				WriteBufferToOutput (bodyWriterOptions.BuffersOutput);
			} else {
				arrayOutput.Write ("zeroinitializer");
			}

			WriteStructureArrayEnd<T> (info, symbolName, (ulong)count, named, skipFinalComment: instances == null, output: arrayOutput);
			WriteBufferToOutput (arrayOutput);
		}

		public void WriteStructureArray<T> (StructureInfo<T> info, IList<StructureInstance<T>>? instances, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null)
		{
			WriteStructureArray<T> (info, instances, LlvmIrVariableOptions.Default, symbolName, writeFieldComment, initialComment);
		}

		void AssertArraySize<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, ulong length, ulong expectedLength)
		{
			if (length == expectedLength) {
				return;
			}

			throw new InvalidOperationException ($"Invalid array size in field '{smi.Info.Name}' of structure '{info.Name}', expected {expectedLength}, found {length}");
		}

		void RenderArray<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, byte[] bytes, TextWriter output, ulong? expectedArraySize = null)
		{
			// Byte arrays are represented in the same way as strings, without the explicit NUL termination byte
			AssertArraySize (info, smi, expectedArraySize ?? (ulong)bytes.Length, smi.ArrayElements);
			output.Write ($"c{QuoteString (bytes, out _, nullTerminated: false)}");
		}

		void MaybeWriteStructureStringsAndBuffers<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, StructureBodyWriterOptions options)
		{
			if (options.StringsOutput != null) {
				MaybeWriteStructureString<T> (info, smi, instance, options.StringsOutput);
			}

			if (options.BuffersOutput != null) {
				MaybeWritePreAllocatedBuffer<T> (info, smi, instance, options.BuffersOutput);
			}
		}

		void WriteStructureField<T> (StructureInfo<T> info, StructureInstance<T> instance, StructureMemberInfo<T> smi, int fieldIndex, StructureBodyWriterOptions options, TextWriter output, object? valueOverride = null, ulong? expectedArraySize = null)
		{
			output.Write (options.FieldIndent);

			object? value = null;
			if (smi.IsNativePointer) {
				WritePointer (info, smi, instance, output);
			} else if (smi.IsNativeArray) {
				if (!smi.IsInlineArray) {
					throw new InvalidOperationException ($"Out of line arrays aren't supported at this time (structure '{info.Name}', field '{smi.Info.Name}')");
				}

				output.Write ($"{smi.IRType} ");
				value = valueOverride ?? GetTypedMemberValue (info, smi, instance, smi.MemberType);

				if (smi.MemberType == typeof(byte[])) {
					RenderArray (info, smi, (byte[])value, output, expectedArraySize);
				} else {
					throw new InvalidOperationException ($"Arrays of type '{smi.MemberType}' aren't supported at this point (structure '{info.Name}', field '{smi.Info.Name}')");
				}
			} else {
				value = valueOverride;
				WritePrimitiveField (info, smi, instance, output);
			}

			FinishStructureField (info, smi, instance, options, fieldIndex, value, output);
		}

		void WriteStructureBody<T> (StructureInfo<T> info, StructureInstance<T>? instance, StructureBodyWriterOptions options)
		{
			TextWriter structureOutput = EnsureOutput (options.StructureOutput);
			structureOutput.Write ($"{options.StructIndent}%struct.{info.Name} ");

			if (instance != null) {
				structureOutput.WriteLine ("{");
				for (int i = 0; i < info.Members.Count; i++) {
					StructureMemberInfo<T> smi = info.Members[i];

					MaybeWriteStructureStringsAndBuffers (info, smi, instance, options);
					WriteStructureField (info, instance, smi, i, options, structureOutput);
				}

				structureOutput.Write ($"{options.StructIndent}}}");
			} else {
				structureOutput.Write ("zeroinitializer");
			}
		}

		void MaybeWriteFieldComment<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, StructureBodyWriterOptions options, object? value, TextWriter output)
		{
			if (!options.WriteFieldComment) {
				return;
			}

			string? comment = info.GetCommentFromProvider (smi, instance);
			if (String.IsNullOrEmpty (comment)) {
				var sb = new StringBuilder (smi.Info.Name);
				if (value != null && smi.MemberType.IsPrimitive && smi.MemberType != typeof(bool)) {
					sb.Append (" (");
					sb.Append ($"0x{value:x})");
				}
				comment = sb.ToString ();
			}
			WriteComment (output, comment);
		}

		void FinishStructureField<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, StructureBodyWriterOptions options, int fieldIndex, object? value, TextWriter output)
		{
			if (fieldIndex < info.Members.Count - 1) {
				output.Write (", ");
			}
			MaybeWriteFieldComment (info, smi, instance, options, value, output);
			WriteEOL (output);
		}

		void WritePrimitiveField<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, TextWriter output, object? overrideValue = null)
		{
			object? value = overrideValue ?? GetTypedMemberValue (info, smi, instance, smi.MemberType);
			output.Write ($"{smi.IRType} {value}");
		}

		void WritePointer<T> (StructureInfo<T> info, StructureMemberInfo<T> smi, StructureInstance<T> instance, TextWriter output, object? overrideValue = null)
		{
			if (info.HasStrings) {
				StructurePointerData? spd = instance.GetPointerData (smi);
				if (spd != null) {
					WriteGetStringPointer (spd.VariableName, spd.Size, indent: false, output: output);
					return;
				}
			}

			if (info.HasPreAllocatedBuffers) {
				StructurePointerData? spd = instance.GetPointerData (smi);
				if (spd != null) {
					WriteGetBufferPointer (spd.VariableName, smi.IRType, spd.Size, indent: false, output: output);
					return;
				}
			}

			if (smi.Info.PointsToSymbol (out string? symbolName)) {
				if (String.IsNullOrEmpty (symbolName) && smi.Info.UsesDataProvider ()) {
					if (info.DataProvider == null) {
						throw new InvalidOperationException ($"Field '{smi.Info.Name}' of structure '{info.Name}' points to a symbol, but symbol name wasn't provided and there's no configured data context provider");
					}
					symbolName = info.DataProvider.GetPointedToSymbolName (instance.Obj, smi.Info.Name);
				}

				if (String.IsNullOrEmpty (symbolName)) {
					WriteNullPointer (smi, output);
					return;
				}

				ulong bufferSize = info.GetBufferSizeFromProvider (smi, instance);
				WriteGetBufferPointer (symbolName, smi.IRType, bufferSize, indent: false, output: output);
				return;
			}

			object? value = overrideValue ?? smi.GetValue (instance.Obj);
			if (value == null || ((value is IntPtr) && (IntPtr)value == IntPtr.Zero)) {
				WriteNullPointer (smi, output);
				return;
			}

			if (value.GetType ().IsPrimitive) {
				ulong v = Convert.ToUInt64 (value);
				if (v == 0) {
					WriteNullPointer (smi, output);
					return;
				}
			}

			throw new InvalidOperationException ($"Non-null pointers to objects of managed type '{smi.Info.MemberType}' (IR type '{smi.IRType}') currently not supported (value: {value})");
		}

		void WriteNullPointer<T> (StructureMemberInfo<T> smi, TextWriter output)
		{
			output.Write ($"{smi.IRType} null");
		}

		// In theory, functionality implemented here should be folded into WriteStructureArray, but in practice it would slow processing for most of the structures we
		// write, thus we'll keep this one separate, even at the cost of some code duplication
		//
		// This code is extremely ugly, one day it should be made look nicer (right... :D)
		//
		public void WritePackedStructureArray<T> (StructureInfo<T> info, IList<StructureInstance<T>> instances, LlvmIrVariableOptions options, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null)
		{
			StructureBodyWriterOptions bodyWriterOptions = InitStructureWrite (info, options, symbolName, writeFieldComment, fieldIndent: $"{Indent}{Indent}");
			TextWriter structureOutput = EnsureOutput (bodyWriterOptions.StructureOutput);
			var structureBodyOutput = new StringWriter ();
			var structureTypeOutput = new StringWriter ();

			bool firstInstance = true;
			var members = new List<PackedStructureMember<T>> ();
			var instanceType = new StringBuilder ();
			foreach (StructureInstance<T> instance in instances) {
				members.Clear ();
				bool hasPaddedFields = false;

				if (!firstInstance) {
					structureTypeOutput.WriteLine (',');
					structureBodyOutput.WriteLine (',');
				} else {
					firstInstance = false;
				}

				foreach (StructureMemberInfo<T> smi in info.Members) {
					object? value = GetTypedMemberValue (info, smi, instance, smi.MemberType);

					if (!smi.NeedsPadding) {
						members.Add (new PackedStructureMember<T> (smi, value));
						continue;
					}

					if (smi.MemberType != typeof(byte[])) {
						throw new InvalidOperationException ($"Only byte arrays are supported currently (field '{smi.Info.Name}' of structure '{info.Name}')");
					}

					var array = (byte[])value;
					var arrayLength = (ulong)array.Length;

					if (arrayLength > smi.ArrayElements) {
						throw new InvalidOperationException ($"Field '{smi.Info.Name}' of structure '{info.Name}' should not have more than {smi.ArrayElements} elements");
					}

					ulong padding = smi.ArrayElements - arrayLength;
					if (padding == 0) {
						members.Add (new PackedStructureMember<T> (smi, value));
						continue;
					}

					if (padding < 8) {
						var paddedValue = new byte[arrayLength + padding];
						Array.Copy (array, paddedValue, array.Length);
						for (int i = (int)arrayLength; i < paddedValue.Length; i++) {
							paddedValue[i] = 0;
						}
						members.Add (new PackedStructureMember<T> (smi, paddedValue));
						continue;
					}

					members.Add (new PackedStructureMember<T> (smi, value, valueIRType: $"[{arrayLength} x i8]", paddingIRType: $"[{padding} x i8]"));
					hasPaddedFields = true;
				}

				bool firstField;
				instanceType.Clear ();
				if (!hasPaddedFields) {
					instanceType.Append ($"\t%struct.{info.Name}");
				} else {
					instanceType.Append ("\t{ ");

					firstField = true;
					foreach (PackedStructureMember<T> psm in members) {
						if (!firstField) {
							instanceType.Append (", ");
						} else {
							firstField = false;
						}

						if (!psm.IsPadded) {
							instanceType.Append (psm.ValueIRType);
							continue;
						}

						instanceType.Append ($"<{{ {psm.ValueIRType}, {psm.PaddingIRType} }}>");
					}

					instanceType.Append (" }");
				}
				structureTypeOutput.Write (instanceType.ToString ());

				structureBodyOutput.Write (instanceType.ToString ());
				structureBodyOutput.WriteLine (" {");

				firstField = true;
				bool previousFieldWasPadded = false;
				for (int i = 0; i < members.Count; i++) {
					PackedStructureMember<T> psm = members[i];

					if (firstField) {
						firstField = false;
					}

					if (!psm.IsPadded) {
						previousFieldWasPadded = false;
						ulong? expectedArraySize = psm.MemberInfo.IsNativeArray ? (ulong)((byte[])psm.Value).Length : null;
						WriteStructureField (info, instance, psm.MemberInfo, i, bodyWriterOptions, structureBodyOutput, valueOverride: psm.Value, expectedArraySize: expectedArraySize);
						continue;
					}

					if (!firstField && previousFieldWasPadded) {
						structureBodyOutput.Write (", ");
					}
					structureBodyOutput.Write ($"{bodyWriterOptions.FieldIndent}<{{ {psm.ValueIRType}, {psm.PaddingIRType} }}> <{{ {psm.ValueIRType} c{QuoteString ((byte[])psm.Value)}, {psm.PaddingIRType} zeroinitializer }}> ");
					MaybeWriteFieldComment (info, psm.MemberInfo, instance, bodyWriterOptions, value: null, output: structureBodyOutput);
					previousFieldWasPadded = true;
				}
				structureBodyOutput.WriteLine ();
				structureBodyOutput.Write ($"{Indent}}}");
			}

			structureOutput.WriteLine ("<{");
			structureOutput.Write (structureTypeOutput);
			structureOutput.WriteLine ();
			structureOutput.WriteLine ("}>");

			structureOutput.WriteLine ("<{");
			structureOutput.Write (structureBodyOutput);
			structureOutput.WriteLine ();
			structureOutput.Write ("}>");

			FinishStructureWrite (info, bodyWriterOptions);
		}

		StructureBodyWriterOptions InitStructureWrite<T> (StructureInfo<T> info, LlvmIrVariableOptions options, string? symbolName, bool writeFieldComment, string? fieldIndent = null)
		{
			if (options.IsGlobal && String.IsNullOrEmpty (symbolName)) {
				throw new ArgumentException ("must not be null or empty for global symbols", nameof (symbolName));
			}

			var structureOutput = new StringWriter ();
			bool named = !String.IsNullOrEmpty (symbolName);
			if (named) {
				WriteEOL (output: structureOutput);
				WriteEOL (symbolName, structureOutput);

				WriteGlobalSymbolStart (symbolName, options, structureOutput);
			}

			return new StructureBodyWriterOptions (
				writeFieldComment: writeFieldComment,
				fieldIndent: fieldIndent ?? Indent,
				structureOutput: structureOutput,
				stringsOutput: info.HasStrings ? new StringWriter () : null,
				buffersOutput: info.HasPreAllocatedBuffers ? new StringWriter () : null
			);
		}

		void FinishStructureWrite<T> (StructureInfo<T> info, StructureBodyWriterOptions bodyWriterOptions)
		{
			bodyWriterOptions.StructureOutput.WriteLine ($", align {info.MaxFieldAlignment}");

			WriteBufferToOutput (bodyWriterOptions.StringsOutput);
			WriteBufferToOutput (bodyWriterOptions.BuffersOutput);
			WriteBufferToOutput (bodyWriterOptions.StructureOutput);
		}

		public void WriteStructure<T> (StructureInfo<T> info, StructureInstance<T>? instance, LlvmIrVariableOptions options, string? symbolName = null, bool writeFieldComment = true)
		{
			StructureBodyWriterOptions bodyWriterOptions = InitStructureWrite (info, options, symbolName, writeFieldComment);

			WriteStructureBody (info, instance, bodyWriterOptions);

			FinishStructureWrite (info, bodyWriterOptions);
		}

		public void WriteStructure<T> (StructureInfo<T> info, StructureInstance<T>? instance, string? symbolName = null, bool writeFieldComment = true)
		{
			WriteStructure<T> (info, instance, LlvmIrVariableOptions.Default, symbolName, writeFieldComment);
		}

		void WriteBufferToOutput (TextWriter? writer)
		{
			if (writer == null) {
				return;
			}

			writer.Flush ();
			string text = writer.ToString ();
			if (text.Length > 0) {
				Output.WriteLine (text);
			}
		}

		void WriteGetStringPointer (string? variableName, ulong size, bool indent = true, TextWriter? output = null)
		{
			WriteGetBufferPointer (variableName, "i8*", size, indent, output);
		}

		void WriteGetBufferPointer (string? variableName, string irType, ulong size, bool indent = true, TextWriter? output = null)
		{
			output = EnsureOutput (output);
			if (indent) {
				output.Write (Indent);
			}

			if (String.IsNullOrEmpty (variableName)) {
				output.Write ($"{irType} null");
			} else {
				string irBaseType;
				if (irType[irType.Length - 1] == '*') {
					irBaseType = irType.Substring (0, irType.Length - 1);
				} else {
					irBaseType = irType;
				}

				output.Write ($"{irType} getelementptr inbounds ([{size} x {irBaseType}], [{size} x {irBaseType}]* @{variableName}, i32 0, i32 0)");
			}
		}

		public void WriteNameValueArray (string symbolName, IDictionary<string, string> arrayContents)
		{
			WriteEOL ();
			WriteEOL (symbolName);

			var strings = new List<(ulong stringSize, string varName)> ();
			long i = 0;
			foreach (var kvp in arrayContents) {
				string name = kvp.Key;
				string value = kvp.Value;

				WriteArrayString (name, $"n_{i}");
				WriteArrayString (value, $"v_{i}");
				i++;
			}
			if (strings.Count > 0) {
				Output.WriteLine ();
			}

			WriteGlobalSymbolStart (symbolName, LlvmIrVariableOptions.GlobalConstantStringPointer);
			Output.Write ($"[{strings.Count} x i8*]");

			if (strings.Count > 0) {
				Output.WriteLine (" [");

				for (int j = 0; j < strings.Count; j++) {
					ulong size = strings[j].stringSize;
					string varName = strings[j].varName;

					//
					// Syntax: https://llvm.org/docs/LangRef.html#getelementptr-instruction
					// the two indices following {varName} have the following meanings:
					//
					//  - The first index is into the **pointer** itself
					//  - The second index is into the **pointed to** value
					//
					// Better explained here: https://llvm.org/docs/GetElementPtr.html#id4
					//
					WriteGetStringPointer (varName, size);
					if (j < strings.Count - 1) {
						Output.WriteLine (",");
					}
				}
				WriteEOL ();
			} else {
				Output.Write (" zeroinitializer");
			}

			var arraySize = (ulong)(strings.Count * PointerSize);
			if (strings.Count > 0) {
				Output.Write ("]");
			}
			Output.WriteLine ($", align {GetAggregateAlignment (PointerSize, arraySize)}");

			void WriteArrayString (string str, string symbolSuffix)
			{
				string name = WriteString ($"__{symbolName}_{symbolSuffix}", str, LlvmIrVariableOptions.LocalConstexprString, out ulong size);
				strings.Add (new (size, name));
			}
		}

		/// <summary>
		/// Wries a global, constant variable
		/// </summary>
		public void WriteVariable<T> (string symbolName, T value)
		{
			WriteVariable (symbolName, value, LlvmIrVariableOptions.GlobalConstant);
		}

		public void WriteVariable<T> (string symbolName, T value, LlvmIrVariableOptions options)
		{
			if (typeof(T) == typeof(string)) {
				WriteString (symbolName, (string)(object)value, options);
				return;
			}

			WriteEOL ();
			string irType = MapManagedTypeToIR<T> (out ulong size);
			WriteGlobalSymbolStart (symbolName, options);

			Output.WriteLine ($"{irType} {value}, align {size}");
		}

		/// <summary>
		/// Writes a private string. Strings without symbol names aren't exported, but they may be referenced by other
		/// symbols
		/// </summary>
		public string WriteString (string value)
		{
			return WriteString (value, LlvmIrVariableOptions.LocalString);
		}

		public string WriteString (string value, LlvmIrVariableOptions options)
		{
			string name = $"@.str";
			if (stringCounter > 0) {
				name += $".{stringCounter}";
			}
			stringCounter++;
			return WriteString (name, value, options);
		}

		/// <summary>
		/// Writes a local, C++ constexpr style string
		/// </summary>
		public string WriteString (string symbolName, string value)
		{
			return WriteString (symbolName, value, LlvmIrVariableOptions.GlobalConstexprString);
		}

		public string WriteString (string symbolName, string value, LlvmIrVariableOptions options)
		{
			return WriteString (symbolName, value, options, out _);
		}

		/// <summary>
		/// Writes a local, constexpr style string and returns its size in <paramref name="stringSize"/>
		/// </summary>
		public string WriteString (string symbolName, string value, out ulong stringSize)
		{
			return WriteString (symbolName, value, LlvmIrVariableOptions.LocalConstexprString, out stringSize);
		}

		public string WriteString (string symbolName, string value, LlvmIrVariableOptions options, out ulong stringSize)
		{
			string strSymbolName;
			bool global = options.IsGlobal;
			if (global) {
				strSymbolName = $"__{symbolName}";
			} else {
				strSymbolName = symbolName;
			}

			string quotedString = QuoteString (value, out stringSize);

			// It might seem counter-intuitive that when we're requested to write a global string, here we generate a **local** one,
			// but global strings are actually pointers to local storage.
			WriteGlobalSymbolStart (strSymbolName, global ? LlvmIrVariableOptions.LocalConstexprString : options);
			Output.WriteLine ($"[{stringSize} x i8] c{quotedString}, align {GetAggregateAlignment (1, stringSize)}");
			if (!global) {
				return symbolName;
			}

			string indexType = Is64Bit ? "i64" : "i32";
			WriteGlobalSymbolStart (symbolName, LlvmIrVariableOptions.GlobalConstantStringPointer);
			Output.WriteLine ($"i8* getelementptr inbounds ([{stringSize} x i8], [{stringSize} x i8]* @{strSymbolName}, {indexType} 0, {indexType} 0), align {GetAggregateAlignment (PointerSize, stringSize)}");

			return symbolName;
		}

		public virtual void WriteFileTop ()
		{
			WriteCommentLine ($"ModuleID = '{fileName}'");
			WriteDirective ("source_filename", QuoteStringNoEscape (fileName));
			WriteDirective ("target datalayout", QuoteStringNoEscape (DataLayout));
			WriteDirective ("target triple", QuoteStringNoEscape (Triple));
		}

		public virtual void WriteFileEnd ()
		{
			Output.WriteLine ();

			foreach (LlvmIrMetadataItem metadata in MetadataManager.Items) {
				Output.WriteLine (metadata.Render ());
			}
		}

		public void WriteStructureDeclarations ()
		{
			if (structures.Count == 0) {
				return;
			}

			Output.WriteLine ();
			foreach (IStructureInfo si in structures) {
				si.RenderDeclaration (this);
			}
		}

		public void WriteStructureDeclarationStart (string name, bool forOpaqueType = false)
		{
			WriteEOL ();
			Output.Write ($"%struct.{name} = type ");
			if (forOpaqueType) {
				Output.WriteLine ("opaque");
			} else {
				Output.WriteLine ("{");
			}
		}

		public void WriteStructureDeclarationEnd ()
		{
			Output.WriteLine ("}");
		}

		public void WriteStructureDeclarationField (string typeName, string comment, bool last)
		{
			Output.Write ($"{Indent}{typeName}");
			if (!last) {
				Output.Write (",");
			}

			if (!String.IsNullOrEmpty (comment)) {
				WriteCommentLine (comment);
			} else {
				WriteEOL ();
			}
		}

		protected virtual void AddModuleFlagsMetadata (List<LlvmIrMetadataItem> flagsFields)
		{
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "wchar_size", 4));
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Max, "PIC Level", 2));
		}

		// Alignment for arrays, structures and unions
		protected virtual int GetAggregateAlignment (int maxFieldAlignment, ulong dataSize)
		{
			return maxFieldAlignment;
		}

		public void WriteCommentLine (string? comment = null, bool indent = false)
		{
			WriteCommentLine (Output, comment, indent);
		}

		public void WriteComment (TextWriter writer, string? comment = null, bool indent = false)
		{
			if (indent) {
				writer.Write (Indent);
			}

			writer.Write (';');

			if (!String.IsNullOrEmpty (comment)) {
				writer.Write (' ');
				writer.Write (comment);
			}
		}

		public void WriteCommentLine (TextWriter writer, string? comment = null, bool indent = false)
		{
			WriteComment (writer, comment, indent);
			writer.WriteLine ();
		}

		public void WriteEOL (string? comment = null, TextWriter? output = null)
		{
			WriteEOL (EnsureOutput (output), comment);
		}

		public void WriteEOL (TextWriter writer, string? comment = null)
		{
			if (!String.IsNullOrEmpty (comment)) {
				WriteCommentLine (writer, comment);
				return;
			}
			writer.WriteLine ();
		}

		public void WriteDirectiveWithComment (TextWriter writer, string name, string? comment, string? value)
		{
			writer.Write (name);

			if (!String.IsNullOrEmpty (value)) {
				writer.Write ($" = {value}");
			}

			WriteEOL (writer, comment);
		}

		public void WriteDirectiveWithComment (string name, string? comment, string? value)
		{
			WriteDirectiveWithComment (name, comment, value);
		}

		public void WriteDirective (TextWriter writer, string name, string? value)
		{
			WriteDirectiveWithComment (writer, name, comment: null, value: value);
		}

		public void WriteDirective (string name, string value)
		{
			WriteDirective (Output, name, value);
		}

		public static string QuoteStringNoEscape (string s)
		{
			return $"\"{s}\"";
		}

		public static string QuoteString (string value, bool nullTerminated = true)
		{
			return QuoteString (value, out _, nullTerminated);
		}

		public static string QuoteString (byte[] bytes)
		{
			return QuoteString (bytes, out _, nullTerminated: false);
		}

		public static string QuoteString (string value, out ulong stringSize, bool nullTerminated = true)
		{
			return QuoteString (Encoding.UTF8.GetBytes (value), out stringSize, nullTerminated);
		}

		public static string QuoteString (byte[] bytes, out ulong stringSize, bool nullTerminated = true)
		{
			var sb = new StringBuilder ();

			foreach (byte b in bytes) {
				if (b != '"' && b >= 32 && b < 127) {
					sb.Append ((char)b);
					continue;
				}

				sb.Append ($"\\{b:X2}");
			}

			stringSize = (ulong)bytes.Length;
			if (nullTerminated) {
				stringSize++;
				sb.Append ("\\00");
			}

			return QuoteStringNoEscape (sb.ToString ());
		}
	}
}
