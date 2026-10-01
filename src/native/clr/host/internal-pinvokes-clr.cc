#include <host/host.hh>
#include <host/typemap.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/internal-pinvokes.hh>
#include <runtime-base/jni-remapping.hh>

using namespace xamarin::android;

const char* clr_typemap_managed_to_java (
	const char *typeName,
	[[maybe_unused]] const char *assemblyFullName,
	[[maybe_unused]] const uint8_t *mvid
) noexcept
{
#if defined(RELEASE)
	return TypeMapper::managed_to_java (typeName, mvid);
#else
	return TypeMapper::managed_to_java (typeName, assemblyFullName);
#endif
}

bool clr_typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept
{
	return TypeMapper::java_to_managed (java_type_name, assembly_name, managed_type_token_id);
}

const char*
_monodroid_lookup_replacement_type (const char *jniSimpleReference)
{
	return JniRemapping::lookup_replacement_type (jniSimpleReference);
}

const JniRemappingReplacementMethod*
_monodroid_lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature)
{
	return JniRemapping::lookup_replacement_method_info (jniSourceType, jniMethodName, jniMethodSignature);
}
