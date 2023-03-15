using System;
using System.IO;

using ELFSharp.ELF.Sections;

using Xamarin.Android.Application.Utilities;

namespace Xamarin.Android.Application;

#pragma warning disable 0649

abstract class ApplicationConfigCommon
{
	protected bool Is64Bit { get; }

	protected Stream GetStream (byte[] data) => new MemoryStream (data);

	protected ApplicationConfigCommon (bool is64Bit)
	{
		Is64Bit = is64Bit;
	}

	protected ulong ReadField (BinaryReader reader, ref bool field, ulong sizeSoFar)
	{
		field = reader.ReadBoolean ();
		return CalculateAdjustedSize<bool> (reader, sizeSoFar);
	}

	protected ulong ReadField (BinaryReader reader, ref byte field, ulong sizeSoFar)
	{
		field = reader.ReadByte ();
		return CalculateAdjustedSize<byte> (reader, sizeSoFar);
	}

	protected ulong ReadField (BinaryReader reader, ref uint field, ulong sizeSoFar)
	{
		field = reader.ReadUInt32 ();
		return CalculateAdjustedSize<uint> (reader, sizeSoFar);
	}

	ulong CalculateAdjustedSize <T> (BinaryReader reader, ulong sizeSoFar)
	{
		ulong typeSize = Util.GetNativeTypeSize<T> (Is64Bit);
		ulong paddedSize = Util.GetPaddedSize<T> (sizeSoFar, Is64Bit);

		if (paddedSize == 0) {
			throw new InvalidOperationException ("Padded size must not be 0");
		}

		if (paddedSize < typeSize) {
			throw new InvalidOperationException ("Padded size must not be smaller than type size");
		}

		if (paddedSize == typeSize) {
			return typeSize;
		}

		ulong seekOffset = paddedSize - typeSize;
		reader.BaseStream.Seek ((long)seekOffset, SeekOrigin.Current);

		return paddedSize;
	}
}

sealed class ApplicationConfig_V1 : ApplicationConfigCommon
{
	public ApplicationConfig_V1 (byte[] data, AnELF elf, ISymbolEntry symbolEntry)
		: base (elf.Is64Bit)
	{}
}

sealed class ApplicationConfig_V2 : ApplicationConfigCommon
{
	public readonly bool   uses_mono_llvm;
	public readonly bool   uses_mono_aot;
	public readonly bool   aot_lazy_load;
	public readonly bool   uses_assembly_preload;
	public readonly bool   broken_exception_transitions;
	public readonly bool   instant_run_enabled ;
	public readonly bool   jni_add_native_method_registration_attribute_present;
	public readonly bool   have_runtime_config_blob;
	public readonly bool   have_assemblies_blob;
	public readonly bool   marshal_methods_enabled;
	public readonly byte   bound_stream_io_exception_type;
	public readonly uint   package_naming_policy;
	public readonly uint   environment_variable_count;
	public readonly uint   system_property_count;
	public readonly uint   number_of_assemblies_in_apk;
	public readonly uint   bundled_assembly_name_width;
	public readonly uint   number_of_assembly_store_files;
	public readonly uint   number_of_dso_cache_entries;
	public readonly uint   android_runtime_jnienv_class_token;
	public readonly uint   jnienv_initialize_method_token;
	public readonly uint   jnienv_registerjninatives_method_token;
	public readonly uint   jni_remapping_replacement_type_count;
	public readonly uint   jni_remapping_replacement_method_index_entry_count;
	public readonly uint   mono_components_mask;
	public readonly string android_package_name = String.Empty;

	public ApplicationConfig_V2 (byte[] data, AnELF elf, ISymbolEntry symbolEntry)
		: base (elf.Is64Bit)
	{
		using Stream stream = GetStream (data);
		using var reader = new BinaryReader (stream);

		ulong sizeSoFar = 0;

		sizeSoFar += ReadField (reader, ref uses_mono_llvm, sizeSoFar);
		sizeSoFar += ReadField (reader, ref uses_mono_aot, sizeSoFar);
		sizeSoFar += ReadField (reader, ref aot_lazy_load, sizeSoFar);
		sizeSoFar += ReadField (reader, ref uses_assembly_preload, sizeSoFar);
		sizeSoFar += ReadField (reader, ref broken_exception_transitions, sizeSoFar);
		sizeSoFar += ReadField (reader, ref instant_run_enabled, sizeSoFar);
		sizeSoFar += ReadField (reader, ref jni_add_native_method_registration_attribute_present, sizeSoFar);
		sizeSoFar += ReadField (reader, ref have_runtime_config_blob, sizeSoFar);
		sizeSoFar += ReadField (reader, ref have_assemblies_blob, sizeSoFar);
		sizeSoFar += ReadField (reader, ref marshal_methods_enabled, sizeSoFar);
		sizeSoFar += ReadField (reader, ref bound_stream_io_exception_type, sizeSoFar);
		sizeSoFar += ReadField (reader, ref package_naming_policy, sizeSoFar);
		sizeSoFar += ReadField (reader, ref environment_variable_count, sizeSoFar);
		sizeSoFar += ReadField (reader, ref system_property_count, sizeSoFar);
		sizeSoFar += ReadField (reader, ref number_of_assemblies_in_apk, sizeSoFar);
		sizeSoFar += ReadField (reader, ref bundled_assembly_name_width, sizeSoFar);
		sizeSoFar += ReadField (reader, ref number_of_assembly_store_files, sizeSoFar);
		sizeSoFar += ReadField (reader, ref number_of_dso_cache_entries, sizeSoFar);
		sizeSoFar += ReadField (reader, ref android_runtime_jnienv_class_token, sizeSoFar);
		sizeSoFar += ReadField (reader, ref jnienv_initialize_method_token, sizeSoFar);
		sizeSoFar += ReadField (reader, ref jnienv_registerjninatives_method_token, sizeSoFar);
		sizeSoFar += ReadField (reader, ref jni_remapping_replacement_type_count, sizeSoFar);
		sizeSoFar += ReadField (reader, ref jni_remapping_replacement_method_index_entry_count, sizeSoFar);
		sizeSoFar += ReadField (reader, ref mono_components_mask, sizeSoFar);

		// TODO: pointers require relocations, to be fixed up at load time. We need to simulate the loading process in order to read pointers.
		ulong pointerValue = Is64Bit ? reader.ReadUInt64 () : (ulong)reader.ReadUInt32 ();
		android_package_name = $"READING NOT IMPLEMENTED YET (pointer field value 0x{pointerValue:x})";
	}
}

#pragma warning restore 0649
