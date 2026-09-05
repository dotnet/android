#include <algorithm>
#include <cstring>

#include <runtime-base/jni-remapping.hh>

#include "xamarin-app.hh"

using namespace xamarin::android;

namespace {
	//
	// `memcmp` ordering over the UTF-8 bytes of the name. `JniRemappingAssemblyGenerator` sorts the
	// tables with exactly the same ordering, which is what makes the binary searches below valid.
	//
	[[gnu::always_inline]]
	auto compare (JniRemappingString const& left, const char *right, size_t right_len) noexcept -> int
	{
		size_t left_len = static_cast<size_t>(left.length);
		size_t min_len = std::min (left_len, right_len);

		if (min_len > 0uz) {
			int ret = memcmp (left.str, right, min_len);
			if (ret != 0) {
				return ret;
			}
		}

		if (left_len == right_len) {
			return 0;
		}

		return left_len < right_len ? -1 : 1;
	}

	template<typename TEntry>
	[[gnu::always_inline]]
	auto lower_bound_by_name (const TEntry *entries, size_t count, const char *name, size_t name_len) noexcept -> size_t
	{
		size_t lo = 0uz;
		size_t hi = count;

		while (lo < hi) {
			size_t mid = lo + ((hi - lo) / 2uz);
			if (compare (entries[mid].name, name, name_len) < 0) {
				lo = mid + 1uz;
			} else {
				hi = mid;
			}
		}

		return lo;
	}

	// Returns the half-open range of entries whose name equals `name`. Overloads share a name, so
	// callers scan the (short) returned range instead of searching the whole table.
	template<typename TEntry>
	auto equal_name_range (const TEntry *entries, size_t count, const char *name, size_t name_len, size_t &first, size_t &last) noexcept -> bool
	{
		first = lower_bound_by_name (entries, count, name, name_len);
		last = first;

		while (last < count && compare (entries[last].name, name, name_len) == 0) {
			last++;
		}

		return first != last;
	}

	auto lookup_type (const JniRemappingTypeReplacementEntry *entries, uint32_t count, const char *jniSimpleReference) noexcept -> const char*
	{
		if (count == 0 || jniSimpleReference == nullptr || *jniSimpleReference == '\0') {
			return nullptr;
		}

		size_t ref_len = strlen (jniSimpleReference);
		size_t idx = lower_bound_by_name (entries, static_cast<size_t>(count), jniSimpleReference, ref_len);

		if (idx >= static_cast<size_t>(count) || compare (entries[idx].name, jniSimpleReference, ref_len) != 0) {
			return nullptr;
		}

		return entries[idx].replacement;
	}
}

auto JniRemapping::is_in_use () noexcept -> bool
{
	return jni_remapping_type_replacement_count > 0 ||
		jni_remapping_reverse_type_replacement_count > 0 ||
		jni_remapping_method_replacement_index_count > 0 ||
		jni_remapping_field_replacement_index_count > 0;
}

auto JniRemapping::lookup_replacement_type (const char *jniSimpleReference) noexcept -> const char*
{
	return lookup_type (jni_remapping_type_replacements, jni_remapping_type_replacement_count, jniSimpleReference);
}

auto JniRemapping::lookup_reverse_type (const char *jniSimpleReference) noexcept -> const char*
{
	return lookup_type (jni_remapping_reverse_type_replacements, jni_remapping_reverse_type_replacement_count, jniSimpleReference);
}

auto JniRemapping::lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature) noexcept -> const JniRemappingReplacementMethod*
{
	if (jni_remapping_method_replacement_index_count == 0 ||
	    jniSourceType == nullptr || *jniSourceType == '\0' ||
	    jniMethodName == nullptr || *jniMethodName == '\0') {
		return nullptr;
	}

	size_t source_type_len = strlen (jniSourceType);
	size_t type_idx = lower_bound_by_name (
		jni_remapping_method_replacement_index,
		static_cast<size_t>(jni_remapping_method_replacement_index_count),
		jniSourceType,
		source_type_len
	);

	if (type_idx >= static_cast<size_t>(jni_remapping_method_replacement_index_count) ||
	    compare (jni_remapping_method_replacement_index[type_idx].name, jniSourceType, source_type_len) != 0) {
		return nullptr;
	}

	JniRemappingIndexTypeEntry const& type = jni_remapping_method_replacement_index[type_idx];
	if (type.method_count == 0 || type.methods == nullptr) {
		return nullptr;
	}

	size_t method_name_len = strlen (jniMethodName);
	size_t first, last;
	if (!equal_name_range (type.methods, static_cast<size_t>(type.method_count), jniMethodName, method_name_len, first, last)) {
		return nullptr;
	}

	size_t signature_len = jniMethodSignature == nullptr ? 0uz : strlen (jniMethodSignature);

	// Most specific first: the full descriptor...
	if (signature_len > 0uz) {
		for (size_t i = first; i < last; i++) {
			JniRemappingIndexMethodEntry const& entry = type.methods[i];
			if (entry.signature.length != 0 && compare (entry.signature, jniMethodSignature, signature_len) == 0) {
				return &entry.replacement;
			}
		}

		// ...then the parameter list only, e.g. an entry of `(I)` matching a call of `(I)V`. This
		// is how the Intune/MAM mapping describes methods whose return type it does not pin.
		const char *sig_end = jniMethodSignature + signature_len;
		while (sig_end != jniMethodSignature && *sig_end != ')') {
			sig_end--;
		}

		if (*sig_end == ')') {
			size_t prefix_len = static_cast<size_t>(sig_end - jniMethodSignature) + 1uz;
			if (prefix_len != signature_len) {
				for (size_t i = first; i < last; i++) {
					JniRemappingIndexMethodEntry const& entry = type.methods[i];
					if (entry.signature.length != 0 && compare (entry.signature, jniMethodSignature, prefix_len) == 0) {
						return &entry.replacement;
					}
				}
			}
		}
	}

	// ...and finally an entry with no signature at all, which matches every overload.
	for (size_t i = first; i < last; i++) {
		JniRemappingIndexMethodEntry const& entry = type.methods[i];
		if (entry.signature.length == 0) {
			return &entry.replacement;
		}
	}

	return nullptr;
}

auto JniRemapping::lookup_replacement_field_info (const char *jniSourceType, const char *jniFieldName, const char *jniFieldSignature) noexcept -> const JniRemappingReplacementField*
{
	if (jni_remapping_field_replacement_index_count == 0 ||
	    jniSourceType == nullptr || *jniSourceType == '\0' ||
	    jniFieldName == nullptr || *jniFieldName == '\0') {
		return nullptr;
	}

	size_t source_type_len = strlen (jniSourceType);
	size_t type_idx = lower_bound_by_name (
		jni_remapping_field_replacement_index,
		static_cast<size_t>(jni_remapping_field_replacement_index_count),
		jniSourceType,
		source_type_len
	);

	if (type_idx >= static_cast<size_t>(jni_remapping_field_replacement_index_count) ||
	    compare (jni_remapping_field_replacement_index[type_idx].name, jniSourceType, source_type_len) != 0) {
		return nullptr;
	}

	JniRemappingIndexFieldTypeEntry const& type = jni_remapping_field_replacement_index[type_idx];
	if (type.field_count == 0 || type.fields == nullptr) {
		return nullptr;
	}

	size_t field_name_len = strlen (jniFieldName);
	size_t first, last;
	if (!equal_name_range (type.fields, static_cast<size_t>(type.field_count), jniFieldName, field_name_len, first, last)) {
		return nullptr;
	}

	size_t signature_len = jniFieldSignature == nullptr ? 0uz : strlen (jniFieldSignature);

	if (signature_len > 0uz) {
		for (size_t i = first; i < last; i++) {
			JniRemappingIndexFieldEntry const& entry = type.fields[i];
			if (entry.signature.length != 0 && compare (entry.signature, jniFieldSignature, signature_len) == 0) {
				return &entry.replacement;
			}
		}
	}

	for (size_t i = first; i < last; i++) {
		JniRemappingIndexFieldEntry const& entry = type.fields[i];
		if (entry.signature.length == 0) {
			return &entry.replacement;
		}
	}

	return nullptr;
}
