#pragma once

struct JniRemappingReplacementMethod;
struct JniRemappingReplacementField;

namespace xamarin::android
{
	//
	// Lookups over the JNI remapping tables emitted by
	// `src/Xamarin.Android.Build.Tasks/Utilities/JniRemappingAssemblyGenerator.cs`.
	//
	// The tables are sorted by their UTF-8 name so every lookup can binary-search them: R8 produces
	// one entry per renamed type and member, so the tables are far too large for linear scans.
	//
	class JniRemapping final
	{
	public:
		// `true` when the application ships any remapping data at all.
		static auto is_in_use () noexcept -> bool;

		// Original (managed) JNI type name -> the name the type has in the packaged application.
		static auto lookup_replacement_type (const char *jniSimpleReference) noexcept -> const char*;

		// The name a type has in the packaged application -> original (managed) JNI type name.
		static auto lookup_reverse_type (const char *jniSimpleReference) noexcept -> const char*;

		static auto lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature) noexcept -> const JniRemappingReplacementMethod*;
		static auto lookup_replacement_field_info (const char *jniSourceType, const char *jniFieldName, const char *jniFieldSignature) noexcept -> const JniRemappingReplacementField*;
	};
}
