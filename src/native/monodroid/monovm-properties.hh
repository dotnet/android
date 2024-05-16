#if !defined (__MONOVM_PROPERTIES_HH)
#define __MONOVM_PROPERTIES_HH

#if defined (NET)
#include <cstring>
#include "monodroid-glue-internal.hh"
#include "jni-wrappers.hh"

namespace xamarin::android::internal
{
	class MonoVMProperties final
	{
		constexpr static size_t PROPERTY_COUNT = 3;

		constexpr static char RUNTIME_IDENTIFIER_KEY[] = "RUNTIME_IDENTIFIER";
		constexpr static size_t RUNTIME_IDENTIFIER_INDEX = 0;

		constexpr static char APP_CONTEXT_BASE_DIRECTORY_KEY[] = "APP_CONTEXT_BASE_DIRECTORY";
		constexpr static size_t APP_CONTEXT_BASE_DIRECTORY_INDEX = 1;

		constexpr static char LOCAL_DATE_TIME_OFFSET_KEY[] = "System.TimeZoneInfo.LocalDateTimeOffset";
		constexpr static size_t LOCAL_DATE_TIME_OFFSET_INDEX = 2;

		using property_array = const char*[PROPERTY_COUNT];

	public:
		explicit MonoVMProperties (jstring_wrapper& filesDir, jint localDateTimeOffset)
		{
			static_assert (PROPERTY_COUNT == N_PROPERTY_KEYS);
			static_assert (PROPERTY_COUNT == N_PROPERTY_VALUES);

			_property_values[APP_CONTEXT_BASE_DIRECTORY_INDEX] = strdup (filesDir.get_cstr ());

			static_local_string<32> localDateTimeOffsetBuffer;
			localDateTimeOffsetBuffer.append (localDateTimeOffset);
			_property_values[LOCAL_DATE_TIME_OFFSET_INDEX] = strdup (localDateTimeOffsetBuffer.get ());
		}

		constexpr int property_count () const
		{
			return PROPERTY_COUNT;
		}

		const char* const* property_keys () const
		{
			if constexpr (PROPERTY_COUNT != 0) {
				return _property_keys;
			} else {
				return nullptr;
			}
		}

		const char* const* property_values () const
		{
			if constexpr (PROPERTY_COUNT != 0) {
				return _property_values;
			} else {
				return nullptr;
			}
		}

	private:
		static property_array _property_keys;
		constexpr static size_t N_PROPERTY_KEYS = sizeof(_property_keys) / sizeof(const char*);

		static property_array _property_values;
		constexpr static size_t N_PROPERTY_VALUES = sizeof(_property_values) / sizeof(const char*);
	};
}
#endif // def NET
#endif // ndef __MONOVM_PROPERTIES_HH
