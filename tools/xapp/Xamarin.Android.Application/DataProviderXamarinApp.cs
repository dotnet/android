using System;
using System.IO;

using Xamarin.Android.Application.Utilities;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Application;

class DataProviderXamarinApp : DataProvider
{
	const string ApplicationConfigSymbolName = "application_config";

	AnELF elf;

	public DataProviderXamarinApp (Stream inputStream, string? inputPath, ILogger log)
		: base (inputStream, inputPath, log)
	{
		string filePath = inputPath ?? "[memory]";
		if (!AnELF.TryLoad (Log, inputStream, filePath, out AnELF? maybeELF) || maybeELF == null) {
			throw new InvalidOperationException ($"Failed to load ELF image from '{filePath}'");
		}

		elf = maybeELF;
	}

	public ApplicationConfig? GetApplicationConfig ()
	{
		if (!elf.HasSymbol (ApplicationConfigSymbolName)) {
			return null;
		}

		var applicationConfig = new ApplicationConfig ();
		ulong size = 0;

		size += elf.GetPaddedSize (size, applicationConfig.uses_mono_llvm);
		size += elf.GetPaddedSize (size, applicationConfig.uses_mono_aot);
		size += elf.GetPaddedSize (size, applicationConfig.aot_lazy_load);
		size += elf.GetPaddedSize (size, applicationConfig.uses_assembly_preload);
		size += elf.GetPaddedSize (size, applicationConfig.broken_exception_transitions);
		size += elf.GetPaddedSize (size, applicationConfig.instant_run_enabled);
		size += elf.GetPaddedSize (size, applicationConfig.jni_add_native_method_registration_attribute_present);
		size += elf.GetPaddedSize (size, applicationConfig.have_runtime_config_blob);
		size += elf.GetPaddedSize (size, applicationConfig.have_assemblies_blob);
		size += elf.GetPaddedSize (size, applicationConfig.marshal_methods_enabled);
		size += elf.GetPaddedSize (size, applicationConfig.bound_stream_io_exception_type);
		size += elf.GetPaddedSize (size, applicationConfig.package_naming_policy);
		size += elf.GetPaddedSize (size, applicationConfig.environment_variable_count);
		size += elf.GetPaddedSize (size, applicationConfig.system_property_count);
		size += elf.GetPaddedSize (size, applicationConfig.number_of_assemblies_in_apk);
		size += elf.GetPaddedSize (size, applicationConfig.bundled_assembly_name_width);
		size += elf.GetPaddedSize (size, applicationConfig.number_of_assembly_store_files);
		size += elf.GetPaddedSize (size, applicationConfig.number_of_dso_cache_entries);
		size += elf.GetPaddedSize (size, applicationConfig.android_runtime_jnienv_class_token);
		size += elf.GetPaddedSize (size, applicationConfig.jnienv_initialize_method_token);
		size += elf.GetPaddedSize (size, applicationConfig.jnienv_registerjninatives_method_token);
		size += elf.GetPaddedSize (size, applicationConfig.jni_remapping_replacement_type_count);
		size += elf.GetPaddedSize (size, applicationConfig.jni_remapping_replacement_method_index_entry_count);
		size += elf.GetPaddedSize (size, applicationConfig.mono_components_mask);
		size += elf.GetPaddedSize (size, applicationConfig.android_package_name);

		byte[] data = elf.GetData (ApplicationConfigSymbolName);
		if (data.Length != (int)size) {
			Log.WarningLine ($"Failed to read '{ApplicationConfigSymbolName}' data from {InputPath} (expected {size}, got {data.Length})");
			return null;
		}

		return applicationConfig;
	}
}
