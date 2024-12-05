#include <dlfcn.h>
#include <jni.h>

#define PINVOKE_OVERRIDE_INLINE [[gnu::noinline]]
#include "pinvoke-override-api-impl.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

//
// This is generated during application build (see obj/${CONFIGURATION}/${RID}/android/pinvoke_preserve.*.ll)
//
using JniOnLoadHandler = jint (*) (JavaVM *vm, void *reserved);

extern "C" {
	void* find_pinvoke (hash_t library_name_hash, hash_t entrypoint_hash, bool &known_library);

	extern const uint32_t __jni_on_load_handler_count;
	extern const JniOnLoadHandler __jni_on_load_handlers[];
	extern const char* __jni_on_load_handler_names[];
	extern const void* __explicitly_preserved_symbols[];
}

[[gnu::flatten]]
void*
PinvokeOverride::monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name) noexcept
{
	log_debug (LOG_ASSEMBLY, __PRETTY_FUNCTION__);
	log_debug (LOG_ASSEMBLY, "library_name == '%s'; entrypoint_name == '%s'", library_name, entrypoint_name);

	if (library_name == nullptr || entrypoint_name == nullptr) [[unlikely]] {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			Util::monodroid_strdup_printf (
				"Both library name ('%s') and entry point name ('%s') must be specified",
				library_name,
				entrypoint_name
			)
		);
	}

	hash_t library_name_hash = xxhash::hash (library_name, strlen (library_name));
    hash_t entrypoint_hash = xxhash::hash (entrypoint_name, strlen (entrypoint_name));
	log_debug (LOG_ASSEMBLY, "library_name_hash == 0x%zx; entrypoint_hash == 0x%zx", library_name_hash, entrypoint_hash);

	bool known_library = true;
	void *pinvoke_ptr = find_pinvoke (library_name_hash, entrypoint_hash, known_library);
	if (pinvoke_ptr != nullptr) [[likely]] {
		log_debug (LOG_ASSEMBLY, "pinvoke_ptr == %p", pinvoke_ptr);
		return pinvoke_ptr;
	}

	if (known_library) [[unlikely]] {
		log_debug (LOG_ASSEMBLY, "Lookup in a known library == internal");
		// Should "never" happen.  It seems we have a known library hash (of one that's linked into the dynamically
		// built DSO) but an unknown symbol hash.  The symbol **probably** doesn't exist (was most likely linked out if
		// the find* functions didn't know its hash), but we cannot be sure of that so we'll try to load it.
		pinvoke_ptr = dlsym (RTLD_DEFAULT, entrypoint_name);
		if (pinvoke_ptr == nullptr) {
			Helpers::abort_application (
				LOG_ASSEMBLY,
				Util::monodroid_strdup_printf (
					"Unable to load p/invoke entry '%s/%s' from the unified runtime DSO",
					library_name,
					entrypoint_name
				)
			);
		}

		return pinvoke_ptr;
	}

	log_debug (LOG_ASSEMBLY, "p/invoke not from a known library, slow path taken.");
	pinvoke_ptr = handle_other_pinvoke_request (library_name, library_name_hash, entrypoint_name, entrypoint_hash);;
	log_debug (LOG_ASSEMBLY, "foreign library pinvoke_ptr == %p", pinvoke_ptr);
	return pinvoke_ptr;
}

void PinvokeOverride::handle_jni_on_load (JavaVM *vm, void *reserved) noexcept
{
	if (__jni_on_load_handler_count == 0) {
		return;
	}

	for (uint32_t i = 0; i < __jni_on_load_handler_count; i++) {
		__jni_on_load_handlers[i] (vm, reserved);
	}

	// This is just to reference the generated array, all we need from it is to be there
	// TODO: see if there's an attribute we can use to make the linker keep the symbol instead.
	// void *first_ptr = __explicitly_preserved_symbols;
	// if (first_ptr == nullptr) {
	// 	// This will never actually be logged, since by the time this function is called we haven't initialized
	// 	// logging categories yet.  It's here just to have some code in the if statement body.
	// 	log_debug (LOG_ASSEMBLY, "No explicitly preserved symbols");
	// }
}
