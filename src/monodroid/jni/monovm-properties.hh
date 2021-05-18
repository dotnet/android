#if !defined (__MONOVM_PROPERTIES_HH)
#define __MONOVM_PROPERTIES_HH

#if defined (NET6)
#include <cstring>
#include "monodroid-glue-internal.hh"

namespace xamarin::android::internal
{
	class MonoVMProperties final
	{
		constexpr static size_t PROPERTY_COUNT = 0;

		using property_array = const char*[PROPERTY_COUNT];

	public:
		explicit MonoVMProperties ()
		{
			static_assert (PROPERTY_COUNT == N_PROPERTY_KEYS);
			static_assert (PROPERTY_COUNT == N_PROPERTY_VALUES);
		}

		int property_count () const
		{
			if constexpr (PROPERTY_COUNT != 0) {
				return _property_count;
			} else {
				return 0;
			}
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
		template<size_t N_PROPERTIES, size_t P_INDEX>
		force_inline void
		add_init_property (const char* key)
		{
			static_assert (P_INDEX < N_PROPERTIES);
			_property_keys[P_INDEX] = key;
			_property_count++;
		}

	private:
		property_array _property_keys = {};
		constexpr static size_t N_PROPERTY_KEYS = sizeof(_property_keys) / sizeof(const char*);

		property_array _property_values = {};
		constexpr static size_t N_PROPERTY_VALUES = sizeof(_property_values) / sizeof(const char*);

		int _property_count = 0;
	};
}
#endif // def NET6
#endif // ndef __MONOVM_PROPERTIES_HH
