using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Base class for all classes which implement architecture-specific code generators.
	/// </summary>
	abstract partial class LlvmIrGenerator
	{
		internal sealed class StructureBodyWriterOptions
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
			{ typeof (void), "void" },
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

		// https://llvm.org/docs/LangRef.html#linkage-types
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

		// https://llvm.org/docs/LangRef.html#runtime-preemption-specifiers
		static readonly Dictionary<LlvmIrRuntimePreemption, string> llvmRuntimePreemption = new Dictionary<LlvmIrRuntimePreemption, string> {
			{ LlvmIrRuntimePreemption.Default, String.Empty },
			{ LlvmIrRuntimePreemption.DSOPreemptable, "dso_preemptable" },
			{ LlvmIrRuntimePreemption.DSOLocal, "dso_local" },
		};

		// https://llvm.org/docs/LangRef.html#visibility-styles
		static readonly Dictionary<LlvmIrVisibility, string> llvmVisibility = new Dictionary<LlvmIrVisibility, string> {
			{ LlvmIrVisibility.Default, "default" },
			{ LlvmIrVisibility.Hidden, "hidden" },
			{ LlvmIrVisibility.Protected, "protected" },
		};

		// https://llvm.org/docs/LangRef.html#global-variables
		static readonly Dictionary<LlvmIrAddressSignificance, string> llvmAddressSignificance = new Dictionary<LlvmIrAddressSignificance, string> {
			{ LlvmIrAddressSignificance.Default, String.Empty },
			{ LlvmIrAddressSignificance.Unnamed, "unnamed_addr" },
			{ LlvmIrAddressSignificance.LocalUnnamed, "local_unnamed_addr" },
		};

		// https://llvm.org/docs/LangRef.html#global-variables
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
		Dictionary<string, StringSymbolInfo> stringSymbolCache = new Dictionary<string, StringSymbolInfo> (StringComparer.Ordinal);
		LlvmIrMetadataItem llvmModuleFlags;

		public const string Indent = "\t";

		protected abstract string DataLayout { get; }
		public abstract int PointerSize { get; }
		protected abstract string Triple { get; }

		public bool Is64Bit { get; }
		public TextWriter Output { get; }
		public AndroidTargetArch TargetArch { get; }

		protected LlvmIrMetadataManager MetadataManager { get; }
		protected LlvmIrStringManager StringManager { get; }

		protected LlvmIrGenerator (AndroidTargetArch arch, TextWriter output, string fileName)
		{
			Output = output;
			MetadataManager = new LlvmIrMetadataManager ();
			StringManager = new LlvmIrStringManager ();
			TargetArch = arch;
			Is64Bit = arch == AndroidTargetArch.X86_64 || arch == AndroidTargetArch.Arm64;
			this.fileName = fileName;
		}

		/// <summary>
		/// Create architecture-specific generator for the given <paramref name="arch"/>. Contents are written
		// to the <paramref name="output"/> stream and <paramref name="fileName"/> is used mostly for error
		// reporting.
		/// </summary>
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

		/// <summary>
		/// Map managed <paramref name="type"/> to its LLVM IR counterpart. Only primitive types,
		/// <c>string</c> and <c>IntPtr</c> are supported.
		/// </summary>
		public static string MapManagedTypeToIR (Type type)
		{
			return EnsureIrType (GetActualType (type));
		}

		/// <summary>
		/// Map managed type to its LLVM IR counterpart. Only primitive types, <c>string</c> and
		/// <c>IntPtr</c> are supported.  Additionally, return the native type size (in bytes) in
		/// <paramref name="size"/>
		/// </summary>
		public string MapManagedTypeToIR (Type type, out ulong size)
		{
			Type actualType = GetActualType (type);
			string irType = EnsureIrType (actualType);
			size = GetTypeSize (actualType);

			return irType;
		}

		ulong GetTypeSize (Type actualType)
		{
			if (!typeSizes.TryGetValue (actualType, out ulong size)) {
				if (actualType == typeof (string) || actualType == typeof (IntPtr) || actualType == typeof (LlvmNativeFunctionSignature)) {
					size = (ulong)PointerSize;
				} else {
					throw new InvalidOperationException ($"Unsupported managed type {actualType}");
				}
			}

			return size;
		}

		/// <summary>
		/// Map managed type <typeparamref name="T"/> to its LLVM IR counterpart. Only primitive types,
		/// <c>string</c> and <c>IntPtr</c> are supported.  Additionally, return the native type size
		/// (in bytes) in <paramref name="size"/>
		/// </summary>
		public string MapManagedTypeToIR<T> (out ulong size)
		{
			return MapManagedTypeToIR (typeof(T), out size);
		}

		/// <summary>
		/// Map a managed <paramref name="type"/> to its <c>C++</c> counterpart. Only primitive types,
		/// <c>string</c> and <c>IntPtr</c> are supported.
		/// </summary>
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

		public string GetIRType<T> (out ulong size, T? value = default)
		{
			if (typeof(T) == typeof(LlvmNativeFunctionSignature)) {
				if (value == null) {
					throw new ArgumentNullException (nameof (value));
				}

				size = (ulong)PointerSize;
				return RenderFunctionSignature ((LlvmNativeFunctionSignature)(object)value);
			}

			return MapManagedTypeToIR<T> (out size);
		}

		public string GetKnownIRType (Type type)
		{
			if (type == null) {
				throw new ArgumentNullException (nameof (type));
			}

			if (type.IsNativeClass ()) {
				IStructureInfo si = GetStructureInfo (type);
				return $"%{si.NativeTypeDesignator}.{si.Name}";
			}

			return MapManagedTypeToIR (type);
		}

		public string GetValue<T> (T value)
		{
			if (typeof(T) == typeof(LlvmNativeFunctionSignature)) {
				if (value == null) {
					throw new ArgumentNullException (nameof (value));
				}

				var v = (LlvmNativeFunctionSignature)(object)value;
				if (v.FieldValue != null) {
					return MonoAndroidHelper.CultureInvariantToString (v.FieldValue);
				}

				return MonoAndroidHelper.CultureInvariantToString (v);
			}

			return MonoAndroidHelper.CultureInvariantToString (value) ?? String.Empty;
		}

		/// <summary>
		/// Initialize the generator.  It involves adding required LLVM IR module metadata (such as data model specification,
		/// code generation flags etc)
		/// </summary>
		protected virtual void Init ()
		{
			llvmModuleFlags = MetadataManager.Add ("llvm.module.flags");
			LlvmIrMetadataItem ident = MetadataManager.Add ("llvm.ident");

			var flagsFields = new List<LlvmIrMetadataItem> ();
			AddModuleFlagsMetadata (flagsFields);

			foreach (LlvmIrMetadataItem item in flagsFields) {
				llvmModuleFlags.AddReferenceField (item.Name);
			}

			LlvmIrMetadataItem identValue = MetadataManager.AddNumbered ($"Xamarin.Android {XABuildConfig.XamarinAndroidBranch} @ {XABuildConfig.XamarinAndroidCommitHash}");
			ident.AddReferenceField (identValue.Name);
		}

		protected void AddLlvmModuleFlag (LlvmIrMetadataItem flag)
		{
			llvmModuleFlags.AddReferenceField (flag.Name);
		}

		/// <summary>
		/// Since LLVM IR is strongly typed, it requires each structure to be properly declared before it is
		/// used throughout the code.  This method uses reflection to scan the managed type <typeparamref name="T"/>
		/// and record the information for future use.  The returned <see cref="StructureInfo<T>"/> structure contains
		/// the description.  It is used later on not only to declare the structure in output code, but also to generate
		/// data from instances of <typeparamref name="T"/>.  This method is typically called from the <see cref="LlvmIrGenerator.MapStructures"/>
		/// method.
		/// </summary>
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

		internal IStructureInfo GetStructureInfo (Type type)
		{
			IStructureInfo? ret = null;

			foreach (IStructureInfo si in structures) {
				if (si.Type != type) {
					continue;
				}

				ret = si;
				break;
			}

			if (ret == null) {
				throw new InvalidOperationException ($"Unmapped structure {type}");
			}

			return ret;
		}

		TextWriter EnsureOutput (TextWriter? output)
		{
			return output ?? Output;
		}

		void WriteGlobalSymbolStart (string symbolName, LlvmIrVariableOptions options, TextWriter? output = null)
		{
			output = EnsureOutput (output);

			output.Write ('@');
			output.Write (symbolName);
			output.Write (" = ");

			string linkage = llvmLinkage [options.Linkage];
			if (!string.IsNullOrEmpty (linkage)) {
				output.Write (linkage);
				output.Write (' ');
			}
			if (options.AddressSignificance != LlvmIrAddressSignificance.Default) {
				output.Write (llvmAddressSignificance[options.AddressSignificance]);
				output.Write (' ');
			}

			output.Write (llvmWritability[options.Writability]);
			output.Write (' ');
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

			StringSymbolInfo stringSymbol = StringManager.Add (str, groupName: info.Name, symbolSuffix: smi.Info.Name);//WriteUniqueString ($"__{info.Name}_{smi.Info.Name}", str, ref structStringCounter);
			instance.AddPointerData (smi, stringSymbol.SymbolName, stringSymbol.Size);

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
			string variableName = $"__{info.Name}_{smi.Info.Name}_{structBufferCounter.ToString (CultureInfo.InvariantCulture)}";
			structBufferCounter++;

			WriteGlobalSymbolStart (variableName, preAllocatedBufferVariableOptions, output);
			ulong size = bufferSize * smi.BaseTypeSize;

			// WriteLine $"[{bufferSize} x {irType}] zeroinitializer, align {GetAggregateAlignment ((int)smi.BaseTypeSize, size)}"
			output.Write ('[');
			output.Write (bufferSize.ToString (CultureInfo.InvariantCulture));
			output.Write (" x ");
			output.Write (irType);
			output.Write ("] zeroinitializer, align ");
			output.WriteLine (GetAggregateAlignment ((int) smi.BaseTypeSize, size).ToString (CultureInfo.InvariantCulture));

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

		void WriteStructureArrayEnd<T> (StructureInfo<T> info, string? symbolName, ulong count, bool named, bool skipFinalComment = false, TextWriter? output = null, bool isArrayOfPointers = false)
		{
			output = EnsureOutput (output);

			int alignment = isArrayOfPointers ? PointerSize : GetAggregateAlignment (info.MaxFieldAlignment, info.Size * count);
			output.Write (", align ");
			output.Write (alignment.ToString (CultureInfo.InvariantCulture));
			if (named && !skipFinalComment) {
				WriteEOL ($"end of '{symbolName!}' array", output);
			} else {
				WriteEOL (output: output);
			}
		}

		/// <summary>
		/// Writes an array of <paramref name="count"/> zero-initialized entries.  <paramref name="options"/> specifies the symbol attributes (visibility, writeability etc)
		/// </summary>
		public void WriteStructureArray<T> (StructureInfo<T> info, ulong count, LlvmIrVariableOptions options, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null, bool isArrayOfPointers = false)
		{
			bool named = WriteStructureArrayStart<T> (info, null, options, symbolName, initialComment);

			// $"[{count} x %{info.NativeTypeDesignator}.{info.Name}{pointerAsterisk}] zeroinitializer"
			Output.Write ('[');
			Output.Write (count.ToString (CultureInfo.InvariantCulture));
			Output.Write (" x %");
			Output.Write (info.NativeTypeDesignator);
			Output.Write ('.');
			Output.Write (info.Name);
			if (isArrayOfPointers)
				Output.Write ('*');
			Output.Write ("] zeroinitializer");

			WriteStructureArrayEnd<T> (info, symbolName, (ulong)count, named, skipFinalComment: true, isArrayOfPointers: isArrayOfPointers);
		}

		/// <summary>
		/// Writes an array of <paramref name="count"/> zero-initialized entries. The array will be generated as a local, writable symbol.
		/// </summary>
		public void WriteStructureArray<T> (StructureInfo<T> info, ulong count, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null, bool isArrayOfPointers = false)
		{
			WriteStructureArray<T> (info, count, LlvmIrVariableOptions.Default, symbolName, writeFieldComment, initialComment, isArrayOfPointers);
		}

		/// <summary>
		/// Writes an array of managed type <typeparamref name="T"/>, with data optionally specified in <paramref name="instances"/> (if it's <c>null</c>, the array
		/// will be zero-initialized).  <paramref name="options"/> specifies the symbol attributes (visibility, writeability etc)
		/// </summary>
		public void WriteStructureArray<T> (StructureInfo<T> info, IList<StructureInstance<T>>? instances, LlvmIrVariableOptions options,
		                                    string? symbolName = null, bool writeFieldComment = true, string? initialComment = null,
		                                    Action<LlvmIrGenerator, StructureBodyWriterOptions, Type, object>? nestedStructureWriter = null)
		{
			var arrayOutput = new StringWriter ();
			bool named = WriteStructureArrayStart<T> (info, instances, options, symbolName, initialComment, arrayOutput);
			int count = instances != null ? instances.Count : 0;

			// $"[{count} x %{info.NativeTypeDesignator}.{info.Name}] "
			arrayOutput.Write ('[');
			arrayOutput.Write (count.ToString (CultureInfo.InvariantCulture));
			arrayOutput.Write (" x %");
			arrayOutput.Write (info.NativeTypeDesignator);
			arrayOutput.Write ('.');
			arrayOutput.Write (info.Name);
			arrayOutput.Write ("] ");

			if (instances != null) {
				var bodyWriterOptions = new StructureBodyWriterOptions (
					writeFieldComment: true,
					fieldIndent: $"{Indent}{Indent}",
					structIndent: Indent,
					structureOutput: arrayOutput,
					stringsOutput: info.HasStrings ? new StringWriter () : null,
					buffersOutput: info.HasPreAllocatedBuffers ? new StringWriter () : null
				);

				arrayOutput.WriteLine ('[');
				for (int i = 0; i < count; i++) {
					StructureInstance<T> instance = instances[i];

					arrayOutput.Write (Indent);
					arrayOutput.Write ("; ");
					arrayOutput.WriteLine (i.ToString (CultureInfo.InvariantCulture));
					WriteStructureBody (info, instance, bodyWriterOptions, nestedStructureWriter);
					if (i < count - 1) {
						arrayOutput.Write (", ");
					}
					WriteEOL (output: arrayOutput);
				}
				arrayOutput.Write (']');

				WriteBufferToOutput (bodyWriterOptions.StringsOutput);
				WriteBufferToOutput (bodyWriterOptions.BuffersOutput);
			} else {
				arrayOutput.Write ("zeroinitializer");
			}

			WriteStructureArrayEnd<T> (info, symbolName, (ulong)count, named, skipFinalComment: instances == null, output: arrayOutput);
			WriteBufferToOutput (arrayOutput);
		}

		/// <summary>
		/// Writes an array of managed type <typeparamref name="T"/>, with data optionally specified in <paramref name="instances"/> (if it's <c>null</c>, the array
		/// will be zero-initialized).  The array will be generated as a local, writable symbol.
		/// </summary>
		public void WriteStructureArray<T> (StructureInfo<T> info, IList<StructureInstance<T>>? instances, string? symbolName = null, bool writeFieldComment = true, string? initialComment = null)
		{
			WriteStructureArray<T> (info, instances, LlvmIrVariableOptions.Default, symbolName, writeFieldComment, initialComment);
		}

		public void WriteArray (IList<string> values, string symbolName, string? initialComment = null)
		{
			WriteEOL ();
			WriteEOL (initialComment ?? symbolName);

			var strings = new List<StringSymbolInfo> ();
			foreach (string s in values) {
				StringSymbolInfo symbol = StringManager.Add (s, groupName: symbolName);
				strings.Add (symbol);
			}

			if (strings.Count > 0) {
				Output.WriteLine ();
			}

			WriteStringArray (symbolName, LlvmIrVariableOptions.GlobalConstantStringPointer, strings);
		}

		public void WriteArray<T> (IList<T> values, LlvmIrVariableOptions options, string symbolName, Func<int, T, string?>? commentProvider = null) where T: struct
		{
			bool optimizeOutput = commentProvider == null;

			WriteGlobalSymbolStart (symbolName, options);
			string elementType = MapManagedTypeToIR (typeof (T), out ulong size);

			// WriteLine $"[{values.Count} x {elementType}] ["
			Output.Write ('[');
			Output.Write (values.Count.ToString (CultureInfo.InvariantCulture));
			Output.Write (" x ");
			Output.Write (elementType);
			Output.WriteLine ("] [");

			Output.Write (Indent);
			for (int i = 0; i < values.Count; i++) {
				if (i != 0) {
					if (optimizeOutput) {
						Output.Write (',');
						if (i % 8 == 0) {
							Output.Write (" ; ");
							Output.Write (i - 8);
							Output.Write ("..");
							Output.WriteLine (i - 1);

							Output.Write (Indent);
						} else {
							Output.Write (' ');
						}
					} else {
						Output.Write (Indent);
					}
				}

				Output.Write (elementType);
				Output.Write (' ');
				Output.Write (MonoAndroidHelper.CultureInvariantToString (values [i]));

				if (!optimizeOutput) {
					bool last = i == values.Count - 1;
					if (!last) {
						Output.Write (',');
					}

					string? comment = commentProvider (i, values[i]);
					if (!String.IsNullOrEmpty (comment)) {
						Output.Write (" ; ");
						Output.Write (comment);
					}

					if (!last) {
						Output.WriteLine ();
					}
				}
			}
			if (optimizeOutput && values.Count / 8 != 0) {
				int idx = values.Count - (values.Count % 8);
				Output.Write (" ; ");
				Output.Write (idx);
				Output.Write ("..");
				Output.Write (values.Count - 1);
			}

			Output.WriteLine ();
			Output.Write ("], align ");
			Output.WriteLine (GetAggregateAlignment ((int) size, size * (ulong) values.Count).ToString (CultureInfo.InvariantCulture));
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
			output.Write ('c');
			output.Write (QuoteString (bytes, bytes.Length, out _, nullTerminated: false));
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

		void WriteStructureField<T> (StructureInfo<T> info, StructureInstance<T> instance, StructureMemberInfo<T> smi, int fieldIndex,
		                             StructureBodyWriterOptions options, TextWriter output, object? valueOverride = null, ulong? expectedArraySize = null,
		                             Action<LlvmIrGenerator, StructureBodyWriterOptions, Type, object>? nestedStructureWriter = null)
		{
			object? value = null;

			if (smi.IsIRStruct ()) {
				if (nestedStructureWriter == null) {
					throw new InvalidOperationException ($"Nested structure found in type {typeof(T)}, field {smi.Info.Name} but no nested structure writer provided");
				}
				nestedStructureWriter (this, options, smi.MemberType, valueOverride ?? GetTypedMemberValue (info, smi, instance, smi.MemberType));
			} else if (smi.IsNativePointer) {
				output.Write (options.FieldIndent);
				WritePointer (info, smi, instance, output);
			} else if (smi.IsNativeArray) {
				if (!smi.IsInlineArray) {
					throw new InvalidOperationException ($"Out of line arrays aren't supported at this time (structure '{info.Name}', field '{smi.Info.Name}')");
				}

				output.Write (options.FieldIndent);
				output.Write (smi.IRType);
				output.Write (" ");
				value = valueOverride ?? GetTypedMemberValue (info, smi, instance, smi.MemberType);

				if (smi.MemberType == typeof(byte[])) {
					RenderArray (info, smi, (byte[])value, output, expectedArraySize);
				} else {
					throw new InvalidOperationException ($"Arrays of type '{smi.MemberType}' aren't supported at this point (structure '{info.Name}', field '{smi.Info.Name}')");
				}
			} else {
				value = valueOverride;
				output.Write (options.FieldIndent);
				WritePrimitiveField (info, smi, instance, output);
			}

			FinishStructureField (info, smi, instance, options, fieldIndex, value, output);
		}

		void WriteStructureBody<T> (StructureInfo<T> info, StructureInstance<T>? instance, StructureBodyWriterOptions options, Action<LlvmIrGenerator, StructureBodyWriterOptions, Type, object>? nestedStructureWriter = null)
		{
			TextWriter structureOutput = EnsureOutput (options.StructureOutput);

			// $"{options.StructIndent}%{info.NativeTypeDesignator}.{info.Name} "
			structureOutput.Write (options.StructIndent);
			structureOutput.Write ('%');
			structureOutput.Write (info.NativeTypeDesignator);
			structureOutput.Write ('.');
			structureOutput.Write (info.Name);
			structureOutput.Write (' ');

			if (instance != null) {
				structureOutput.WriteLine ('{');
				for (int i = 0; i < info.Members.Count; i++) {
					StructureMemberInfo<T> smi = info.Members[i];

					MaybeWriteStructureStringsAndBuffers (info, smi, instance, options);
					WriteStructureField (info, instance, smi, i, options, structureOutput, nestedStructureWriter: nestedStructureWriter);
				}

				structureOutput.Write (options.StructIndent);
				structureOutput.Write ('}');
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
					sb.Append (" (0x");
					sb.Append ($"{value:x}");
					sb.Append (')');
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
			output.Write (smi.IRType);
			output.Write (' ');
			output.Write (MonoAndroidHelper.CultureInvariantToString (value));
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

			throw new InvalidOperationException ($"While processing field '{smi.Info.Name}' of type '{info.Name}': non-null pointers to objects of managed type '{smi.MemberType}' (IR type '{smi.IRType}') currently not supported (value: {value})");
		}

		void WriteNullPointer<T> (StructureMemberInfo<T> smi, TextWriter output)
		{
			output.Write (smi.IRType);
			output.Write (" null");
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
					instanceType.Append ("\t%");
					instanceType.Append (info.NativeTypeDesignator);
					instanceType.Append ('.');
					instanceType.Append (info.Name);
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

						// $"<{{ {psm.ValueIRType}, {psm.PaddingIRType} }}>"
						instanceType.Append ("<{ ");
						instanceType.Append (psm.ValueIRType);
						instanceType.Append (", ");
						instanceType.Append (psm.PaddingIRType);
						instanceType.Append (" }>");
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

					// $"{bodyWriterOptions.FieldIndent}<{{ {psm.ValueIRType}, {psm.PaddingIRType} }}> <{{ {psm.ValueIRType} c{QuoteString ((byte[])psm.Value)}, {psm.PaddingIRType} zeroinitializer }}> "
					structureBodyOutput.Write (bodyWriterOptions.FieldIndent);
					structureBodyOutput.Write ("<{ ");
					structureBodyOutput.Write (psm.ValueIRType);
					structureBodyOutput.Write (", ");
					structureBodyOutput.Write (psm.PaddingIRType);
					structureBodyOutput.Write (" }> <{ ");
					structureBodyOutput.Write (psm.ValueIRType);
					structureBodyOutput.Write (" c");
					structureBodyOutput.Write (QuoteString ((byte []) psm.Value));
					structureBodyOutput.Write (", ");
					structureBodyOutput.Write (psm.PaddingIRType);
					structureBodyOutput.Write (" zeroinitializer }> ");

					MaybeWriteFieldComment (info, psm.MemberInfo, instance, bodyWriterOptions, value: null, output: structureBodyOutput);
					previousFieldWasPadded = true;
				}
				structureBodyOutput.WriteLine ();
				structureBodyOutput.Write (Indent);
				structureBodyOutput.Write ('}');
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
			bodyWriterOptions.StructureOutput.Write (", align ");
			bodyWriterOptions.StructureOutput.WriteLine (info.MaxFieldAlignment.ToString (CultureInfo.InvariantCulture));

			WriteBufferToOutput (bodyWriterOptions.StringsOutput);
			WriteBufferToOutput (bodyWriterOptions.BuffersOutput);
			WriteBufferToOutput (bodyWriterOptions.StructureOutput);
		}

		public void WriteStructure<T> (StructureInfo<T> info, StructureInstance<T>? instance, StructureBodyWriterOptions bodyWriterOptions, LlvmIrVariableOptions options, string? symbolName = null, bool writeFieldComment = true)
		{
			WriteStructureBody (info, instance, bodyWriterOptions);
			FinishStructureWrite (info, bodyWriterOptions);
		}

		public void WriteNestedStructure<T> (StructureInfo<T> info, StructureInstance<T> instance, StructureBodyWriterOptions bodyWriterOptions)
		{
			var options = new StructureBodyWriterOptions (
				bodyWriterOptions.WriteFieldComment,
				bodyWriterOptions.FieldIndent + Indent,
				bodyWriterOptions.FieldIndent, // structure indent should start at the original struct's field column
				bodyWriterOptions.StructureOutput,
				bodyWriterOptions.StringsOutput,
				bodyWriterOptions.BuffersOutput
			);
			WriteStructureBody (info, instance, options);
		}

		/// <summary>
		/// Write a structure represented by managed type <typeparamref name="T"/>, with optional data passed in <paramref name="instance"/> (if <c>null</c>, the structure
		/// is zero-initialized). <paramref name="options"/> specifies the symbol attributes (visibility, writeability etc)
		/// </summary>
		public void WriteStructure<T> (StructureInfo<T> info, StructureInstance<T>? instance, LlvmIrVariableOptions options, string? symbolName = null, bool writeFieldComment = true)
		{
			StructureBodyWriterOptions bodyWriterOptions = InitStructureWrite (info, options, symbolName, writeFieldComment);
			WriteStructure (info, instance, bodyWriterOptions, options, symbolName, writeFieldComment);
		}

		/// <summary>
		/// Write a structure represented by managed type <typeparamref name="T"/>, with optional data passed in <paramref name="instance"/> (if <c>null</c>, the structure
		/// is zero-initialized).  The structure will be generated as a local, writable symbol.
		/// </summary>
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

		void WriteGetStringPointer (string? variableName, ulong size, bool indent = true, TextWriter? output = null, bool detectBitness = false, bool skipPointerType = false)
		{
			WriteGetBufferPointer (variableName, "i8*", size, indent, output, detectBitness, skipPointerType);
		}

		void WriteGetBufferPointer (string? variableName, string irType, ulong size, bool indent = true, TextWriter? output = null, bool detectBitness = false, bool skipPointerType = false)
		{
			output = EnsureOutput (output);
			if (indent) {
				output.Write (Indent);
			}

			if (String.IsNullOrEmpty (variableName)) {
				output.Write (irType);
				output.Write (" null");
			} else {
				string irBaseType;
				if (irType[irType.Length - 1] == '*') {
					irBaseType = irType.Substring (0, irType.Length - 1);
				} else {
					irBaseType = irType;
				}

				string indexType = detectBitness && Is64Bit ? "i64" : "i32";
				// $"{irType} getelementptr inbounds ([{size} x {irBaseType}], [{size} x {irBaseType}]* @{variableName}, i32 0, i32 0)"
				if (!skipPointerType) {
					output.Write (irType);
				}

				string sizeStr = size.ToString (CultureInfo.InvariantCulture);
				output.Write (" getelementptr inbounds ([");
				output.Write (sizeStr);
				output.Write (" x ");
				output.Write (irBaseType);
				output.Write ("], [");
				output.Write (sizeStr);
				output.Write (" x ");
				output.Write (irBaseType);
				output.Write ("]* @");
				output.Write (variableName);
				output.Write (", ");
				output.Write (indexType);
				output.Write (" 0, ");
				output.Write (indexType);
				output.Write (" 0)");
			}
		}

		/// <summary>
		/// Write an array of name/value pairs.  The array symbol will be global and non-writable.
		/// </summary>
		public void WriteNameValueArray (string symbolName, IDictionary<string, string> arrayContents)
		{
			WriteEOL ();
			WriteEOL (symbolName);

			var strings = new List<StringSymbolInfo> ();
			long i = 0;

			foreach (var kvp in arrayContents) {
				string name = kvp.Key;
				string value = kvp.Value;
				string iStr = i.ToString (CultureInfo.InvariantCulture);

				WriteArrayString (name, $"n_{iStr}");
				WriteArrayString (value, $"v_{iStr}");
				i++;
			}

			if (strings.Count > 0) {
				Output.WriteLine ();
			}

			WriteStringArray (symbolName, LlvmIrVariableOptions.GlobalConstantStringPointer, strings);

			void WriteArrayString (string str, string symbolSuffix)
			{
				StringSymbolInfo symbol = StringManager.Add (str, groupName: symbolName, symbolSuffix: symbolSuffix);
				strings.Add (symbol);
			}
		}

		void WriteStringArray (string symbolName, LlvmIrVariableOptions options, List<StringSymbolInfo> strings)
		{
			WriteGlobalSymbolStart (symbolName, options);

			// $"[{strings.Count} x i8*]"
			Output.Write ('[');
			Output.Write (strings.Count.ToString (CultureInfo.InvariantCulture));
			Output.Write (" x i8*]");

			if (strings.Count > 0) {
				Output.WriteLine (" [");

				for (int j = 0; j < strings.Count; j++) {
					ulong size = strings[j].Size;
					string varName = strings[j].SymbolName;

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
						Output.WriteLine (',');
					}
				}
				WriteEOL ();
			} else {
				Output.Write (" zeroinitializer");
			}

			var arraySize = (ulong)(strings.Count * PointerSize);
			if (strings.Count > 0) {
				Output.Write (']');
			}
			Output.Write (", align ");
			Output.WriteLine (GetAggregateAlignment (PointerSize, arraySize).ToString (CultureInfo.InvariantCulture));
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
			string irType = GetIRType<T> (out ulong size, value);
			WriteGlobalSymbolStart (symbolName, options);

			Output.Write (irType);
			Output.Write (' ');
			Output.Write (GetValue (value));
			Output.Write (", align ");
			Output.WriteLine (size);
		}

		/// <summary>
		/// Writes a global, C++ constexpr style string
		/// </summary>
		public string WriteString (string symbolName, string value)
		{
			return WriteString (symbolName, value, LlvmIrVariableOptions.GlobalConstexprString);
		}

		/// <summary>
		/// Writes a string with symbol options (writeability, visibility) options specified in the <paramref name="options"/> parameter.
		/// </summary>
		public string WriteString (string symbolName, string value, LlvmIrVariableOptions options)
		{
			return WriteString (symbolName, value, options, out _);
		}

		/// <summary>
		/// Writes a string with specified <paramref name="symbolName"/>, and symbol options (writeability, visibility etc) specified in the <paramref name="options"/>
		/// parameter.  Returns string size (in bytes) in <paramref name="stringSize"/>
		/// </summary>
		public string WriteString (string symbolName, string value, LlvmIrVariableOptions options, out ulong stringSize)
		{
			StringSymbolInfo info = StringManager.Add (value, groupName: symbolName);
			stringSize = info.Size;
			if (!options.IsGlobal) {
				return symbolName;
			}

			string indexType = Is64Bit ? "i64" : "i32";
			WriteGlobalSymbolStart (symbolName, LlvmIrVariableOptions.GlobalConstantStringPointer);
			WriteGetStringPointer (info.SymbolName, info.Size, indent: false, detectBitness: true);
			Output.Write (", align ");
			Output.WriteLine (GetAggregateAlignment (PointerSize, stringSize).ToString (CultureInfo.InvariantCulture));

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
			StringManager.Flush (this);

			Output.WriteLine ();
			WriteAttributeSets ();

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

		public void WriteStructureDeclarationStart (string typeDesignator, string name, bool forOpaqueType = false)
		{
			WriteEOL ();

			// $"%{typeDesignator}.{name} = type "
			Output.Write ('%');
			Output.Write (typeDesignator);
			Output.Write ('.');
			Output.Write (name);
			Output.Write (" = type ");

			if (forOpaqueType) {
				Output.WriteLine ("opaque");
			} else {
				Output.WriteLine ("{");
			}
		}

		public void WriteStructureDeclarationEnd ()
		{
			Output.WriteLine ('}');
		}

		public void WriteStructureDeclarationField (string typeName, string comment, bool last)
		{
			Output.Write (Indent);
			Output.Write (typeName);
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
				writer.Write (" = ");
				writer.Write (value);
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
			return QuoteString (bytes, bytes.Length, out _, nullTerminated: false);
		}

		public static string QuoteString (string value, out ulong stringSize, bool nullTerminated = true)
		{
			var encoding = Encoding.UTF8;
			int byteCount = encoding.GetByteCount (value);
			var bytes = ArrayPool<byte>.Shared.Rent (byteCount);
			try {
				encoding.GetBytes (value, 0, value.Length, bytes, 0);
				return QuoteString (bytes, byteCount, out stringSize, nullTerminated);
			} finally {
				ArrayPool<byte>.Shared.Return (bytes);
			}
		}

		public static string QuoteString (byte[] bytes, int byteCount, out ulong stringSize, bool nullTerminated = true)
		{
			var sb = new StringBuilder (byteCount * 2); // rough estimate of capacity

			byte b;
			for (int i = 0; i < byteCount; i++) {
				b = bytes [i];
				if (b != '"' && b != '\\' && b >= 32 && b < 127) {
					sb.Append ((char)b);
					continue;
				}

				sb.Append ('\\');
				sb.Append ($"{b:X2}");
			}

			stringSize = (ulong) byteCount;
			if (nullTerminated) {
				stringSize++;
				sb.Append ("\\00");
			}

			return QuoteStringNoEscape (sb.ToString ());
		}
	}
}
