#include <xamarin-app.hh>

// Apps without remapping data use these empty tables. The post-ILC remapping object supplies
// strong definitions when needed. Keep the defaults separate from the lookup code so that its
// references are resolved by the final application link rather than folded to these empty tables.
extern "C" {
	[[gnu::weak]] extern const JniRemappingIndexTypeEntry jni_remapping_method_replacement_index[1] {};
	[[gnu::weak]] extern const JniRemappingIndexFieldTypeEntry jni_remapping_field_replacement_index[1] {};
	[[gnu::weak]] extern const JniRemappingTypeReplacementEntry jni_remapping_type_replacements[1] {};
	[[gnu::weak]] extern const JniRemappingTypeReplacementEntry jni_remapping_reverse_type_replacements[1] {};

	[[gnu::weak]] extern const uint32_t jni_remapping_type_replacement_count = 0;
	[[gnu::weak]] extern const uint32_t jni_remapping_reverse_type_replacement_count = 0;
	[[gnu::weak]] extern const uint32_t jni_remapping_method_replacement_index_count = 0;
	[[gnu::weak]] extern const uint32_t jni_remapping_field_replacement_index_count = 0;
}
