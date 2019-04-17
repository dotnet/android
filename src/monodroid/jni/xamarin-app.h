// Dear Emacs, this is a -*- C++ -*- header
#ifndef __XAMARIN_ANDROID_TYPEMAP_H
#define __XAMARIN_ANDROID_TYPEMAP_H

#include <stdint.h>

#include "monodroid.h"

struct TypeMapHeader
{
	uint32_t version;
	uint32_t entry_count;
	uint32_t entry_length;
	uint32_t value_offset;
};

struct ApplicationConfig
{
	bool uses_mono_llvm;
	bool uses_mono_aot;
	bool uses_embedded_dsos;
	bool uses_assembly_preload;
	bool is_a_bundled_app;
	uint32_t environment_variable_count;
	uint32_t system_property_count;
	const char *android_package_name;
};

MONO_API TypeMapHeader jm_typemap_header;
MONO_API uint8_t jm_typemap[];

MONO_API TypeMapHeader mj_typemap_header;
MONO_API uint8_t mj_typemap[];

MONO_API ApplicationConfig application_config;
MONO_API const char* app_environment_variables[];
MONO_API const char* app_system_properties[];

MONO_API const char* mono_aot_mode_name;
#endif // __XAMARIN_ANDROID_TYPEMAP_H
