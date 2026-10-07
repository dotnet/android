#include <cstdint>
#include <stdlib.h>

#include <managed-interface.hh>
#include <xamarin-app.hh>

// This file MUST have "valid" values everywhere - the DSO it is compiled into is loaded by the
// designer on desktop.
const uint64_t format_tag = FORMAT_TAG;

static const JniRemappingIndexMethodEntry some_java_type_one_methods[] = {
	{
		.name = {
			.length = 15,
			.str = "old_method_name",
		},

		.signature = {
			.length = 0,
			.str = nullptr,
		},

		.replacement = {
			.target_type = "some/java/target_type_one",
			.target_name = "new_method_name",
			.target_signature = nullptr,
			.is_static = false,
		}
	},
};

static const JniRemappingIndexMethodEntry some_java_type_two_methods[] = {
	{
		.name = {
			.length = 15,
			.str = "old_method_name",
		},

		.signature = {
			.length = 28,
			.str = "(IILandroid/content/Intent;)",
		},

		.replacement = {
			.target_type = "some/java/target_type_two",
			.target_name = "new_method_name",
			.target_signature = nullptr,
			.is_static = true,
		}
	},
};

const JniRemappingIndexTypeEntry jni_remapping_method_replacement_index[] = {
	{
		.name = {
			.length = 18,
			.str = "some/java/type_one",
		},
		.method_count = 1,
		.methods = some_java_type_one_methods,
	},

	{
		.name = {
			.length = 18,
			.str = "some/java/type_two",
		},
		.method_count = 1,
		.methods = some_java_type_two_methods,
	},
};

const JniRemappingTypeReplacementEntry jni_remapping_type_replacements[] = {
	{
		.name = {
			.length = 14,
			.str = "some/java/type",
		},
		.replacement = "another/java/type",
	},

	{
		.name = {
			.length = 20,
			.str = "some/other/java/type",
		},
		.replacement = "another/replacement/java/type",
	},
};

extern "C" const xamarin::android::JniRemappingData jni_remapping_data {
	.type_replacements = jni_remapping_type_replacements,
	.reverse_type_replacements = nullptr,
	.method_replacement_index = jni_remapping_method_replacement_index,
	.field_replacement_index = nullptr,
	.type_replacement_count = 2,
	.reverse_type_replacement_count = 0,
	.method_replacement_index_count = 2,
	.field_replacement_index_count = 0,
};
