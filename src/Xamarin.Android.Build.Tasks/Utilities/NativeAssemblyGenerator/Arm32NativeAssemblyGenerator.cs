using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	class Arm32NativeAssemblyGenerator : ArmNativeAssemblyGenerator
	{
		static readonly NativeType pointer = new NativeType {
			Size = 4,
			Alignment = 4,
			Name = ".long",
		};

		protected override string ArchName => "armv7-a";
		protected override string LineCommentStart => "@";
		protected override string TypeLead => "%";
		public override bool Is64Bit => false;

		public Arm32NativeAssemblyGenerator (StreamWriter output, string fileName)
			: base (output, fileName)
		{}

		protected override NativeType GetPointerType () => pointer;

		public override uint WriteData<T> (TextWriter writer, T value, bool hex = true, string? comment = null, bool useBlockComment = false) where T: struct
		{
			Type type = typeof(T);
			if (type != typeof(long) && type != typeof(ulong)) {
				return base.WriteData<T> (writer, value, hex, comment, useBlockComment);
			}

			string nativeTypeName = GetNativeTypeName<T> ();
			ulong high, low;

			if (type == typeof(long)) {
				high = (ulong)((((ulong)(long)(object)value) & 0xFFFFFFFF00000000UL) >> 32);
				low = (ulong)(((ulong)(long)(object)value) & 0xFFFFFFFFUL);
			} else {
				high = (((ulong)(object)value) & 0xFFFFFFFF00000000UL) >> 32;
				low = ((ulong)(object)value) & 0xFFFFFFFFUL;
			}

			WriteData (writer, nativeTypeName, ToString (low, hex), comment);
			WriteData (writer, nativeTypeName, ToString (high, hex));

			return GetNativeTypeSize<T> ();
		}

		protected override void ConfigureTypeMappings (Dictionary<Type, NativeType?> mapping)
		{
			base.ConfigureTypeMappings (mapping);

			// Alignments and sizes as per https://github.com/ARM-software/abi-aa/blob/320a56971fdcba282b7001cf4b84abb4fd993131/aapcs32/aapcs32.rst#fundamental-data-types
			// Assembler type directives are described in https://sourceware.org/binutils/docs-2.37/as/index.html
			ConfigureTypeMapping<short>  (".short", size: 2, alignment: 2);
			ConfigureTypeMapping<ushort> (".short", size: 2, alignment: 2);
			ConfigureTypeMapping<int>    (".long",  size: 4, alignment: 4);
			ConfigureTypeMapping<uint>   (".long",  size: 4, alignment: 4);
			ConfigureTypeMapping<long>   (".long",  size: 8, alignment: 8);
			ConfigureTypeMapping<ulong>  (".long",  size: 8, alignment: 8);
			ConfigureTypeMapping<float>  (".long",  size: 4, alignment: 4);
			ConfigureTypeMapping<double> (".long",  size: 8, alignment: 8);
			ConfigureTypeMapping<nint>   (".long",  size: 4, alignment: 4);
			ConfigureTypeMapping<nuint>  (".long",  size: 4, alignment: 4);
			ConfigureTypeMapping<IntPtr> (".long",  size: 4, alignment: 4);
		}

		public override void WriteFileTop ()
		{
			base.WriteFileTop ();

			WriteDirective (".syntax", "unified");
			WriteDirectiveWithComment (".eabi_attribute", "Tag_conformance", 67, QuoteString ("2.09"));
			WriteDirectiveWithComment (".eabi_attribute", "Tag_CPU_arch", 6, 10);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_CPU_arch_profile", 7, 65);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ARM_ISA_use", 8, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_THUMB_ISA_use", 9, 2);
			WriteDirective (".fpu", "neon");
			WriteDirectiveWithComment (".eabi_attribute", "Tag_CPU_unaligned_access", 34, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_PCS_RW_data", 15, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_PCS_RO_data", 16, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_PCS_GOT_use", 17, 2);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_FP_denormal", 20, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_FP_exceptions", 21, 0);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_FP_number_model", 23, 3);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_align_needed", 24, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_align_preserved", 25, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_FP_16bit_format", 38, 1);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_PCS_wchar_t", 18, 4);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_enum_size", 26, 2);
			WriteDirectiveWithComment (".eabi_attribute", "Tag_ABI_PCS_R9_use", 14, 0);
		}
	}
}
