using System;
using System.IO;

namespace Xamarin.Android.Tasks
{
	class ARMNativeAssemblerTargetProvider : NativeAssemblerTargetProvider
	{
		public override bool Is64Bit { get; }
		public override string PointerFieldType { get; }
		public override string TypePrefix { get; }

		public ARMNativeAssemblerTargetProvider (bool is64Bit)
		{
			Is64Bit = is64Bit;
			PointerFieldType = is64Bit ? ".xword" : ".long";
			TypePrefix = is64Bit ? "@" : "%";
		}

		public override string MapType <T> ()
		{
			if (typeof(T) == typeof(Int32) || typeof(T) == typeof(UInt32))
				return Is64Bit ? ".word" : ".long";
			return base.MapType <T> ();
		}

		public override void WriteFileHeader (StreamWriter output, string indent)
		{
			output.Write ($"{indent}.arch{indent}");
			output.WriteLine (Is64Bit ? "armv8-a" : "armv7-a");

			if (!Is64Bit) {
				output.WriteLine ($"{indent}.syntax unified");
				output.WriteLine ($"{indent}.eabi_attribute 67, \"2.09\"{indent}@ Tag_conformance");
				output.WriteLine ($"{indent}.eabi_attribute 6, 10{indent}@ Tag_CPU_arch");
				output.WriteLine ($"{indent}.eabi_attribute 7, 65{indent}@ Tag_CPU_arch_profile");
				output.WriteLine ($"{indent}.eabi_attribute 8, 1{indent}@ Tag_ARM_ISA_use");
				output.WriteLine ($"{indent}.eabi_attribute 9, 2{indent}@ Tag_THUMB_ISA_use");
				output.WriteLine ($"{indent}.fpu{indent}vfpv3-d16");
				output.WriteLine ($"{indent}.eabi_attribute 34, 1{indent}@ Tag_CPU_unaligned_access");
				output.WriteLine ($"{indent}.eabi_attribute 15, 1{indent}@ Tag_ABI_PCS_RW_data");
				output.WriteLine ($"{indent}.eabi_attribute 16, 1{indent}@ Tag_ABI_PCS_RO_data");
				output.WriteLine ($"{indent}.eabi_attribute 17, 2{indent}@ Tag_ABI_PCS_GOT_use");
				output.WriteLine ($"{indent}.eabi_attribute 20, 2{indent}@ Tag_ABI_FP_denormal");
				output.WriteLine ($"{indent}.eabi_attribute 21, 0{indent}@ Tag_ABI_FP_exceptions");
				output.WriteLine ($"{indent}.eabi_attribute 23, 3{indent}@ Tag_ABI_FP_number_model");
				output.WriteLine ($"{indent}.eabi_attribute 24, 1{indent}@ Tag_ABI_align_needed");
				output.WriteLine ($"{indent}.eabi_attribute 25, 1{indent}@ Tag_ABI_align_preserved");
				output.WriteLine ($"{indent}.eabi_attribute 38, 1{indent}@ Tag_ABI_FP_16bit_format");
				output.WriteLine ($"{indent}.eabi_attribute 18, 4{indent}@ Tag_ABI_PCS_wchar_t");
				output.WriteLine ($"{indent}.eabi_attribute 26, 2{indent}@ Tag_ABI_enum_size");
				output.WriteLine ($"{indent}.eabi_attribute 14, 0{indent}@ Tag_ABI_PCS_R9_use");
			}

			base.WriteFileHeader (output, indent);
		}
	}
}
