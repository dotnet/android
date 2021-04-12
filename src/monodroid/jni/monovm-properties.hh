#if !defined (__MONOVM_PROPERTIES_HH)
#define __MONOVM_PROPERTIES_HH

#if defined (NET6)
#include <cstring>
#include "monodroid-glue-internal.hh"

namespace xamarin::android::internal
{
	class MonoVMProperties final
	{
		constexpr static size_t PROPERTY_COUNT = 1;

		constexpr static char PINVOKE_OVERRIDE_KEY[] = "PINVOKE_OVERRIDE";
		constexpr static size_t PINVOKE_OVERRIDE_INDEX = 0;

		using property_array = const char*[PROPERTY_COUNT];

	public:
		explicit MonoVMProperties (PInvokeOverrideFn pinvoke_override_cb)
		{
			static_assert (PROPERTY_COUNT == N_PROPERTY_KEYS);
			static_assert (PROPERTY_COUNT == N_PROPERTY_VALUES);

			snprintf (ptr_str, sizeof(ptr_str), "%p", pinvoke_override_cb);
		}

		int property_count () const
		{
			return _property_count;
		}

		const char* const* property_keys () const
		{
			return _property_keys;
		}

		const char* const* property_values () const
		{
			return _property_values;
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
		property_array _property_keys = {
			PINVOKE_OVERRIDE_KEY,
		};
		constexpr static size_t N_PROPERTY_KEYS = sizeof(_property_keys) / sizeof(const char*);

		property_array _property_values = {
			ptr_str,
		};
		constexpr static size_t N_PROPERTY_VALUES = sizeof(_property_values) / sizeof(const char*);

		int _property_count = 1;
		char ptr_str[20];
	};
}
#endif // def NET6
#endif // ndef __MONOVM_PROPERTIES_HH
