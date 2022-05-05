// Dear Emacs, this is a -*- C++ -*- header
#if !defined (__MONO_IMAGE_LOADER_HH)
#define __MONO_IMAGE_LOADER_HH

#include <cstdint>

#include <mono/metadata/image.h>
#if defined (NET6)
#include <mono/metadata/mono-private-unstable.h>
#endif
#include <mono/metadata/object.h>

#include "platform-compat.hh"
#include "xamarin-app.hh"
#include "xxhash.hh"
#include "search.hh"
#include "strings.hh"

namespace xamarin::android::internal {
	enum class MonoImageLoaderContext
	{
		ALC,
		AppDomain,
	};

	class MonoImageLoader final
	{
	public:
#if defined (RELEASE) && defined (ANDROID) && defined (NET6)
		force_inline static MonoImage* get_from_index (size_t index) noexcept
		{
			return nullptr;
		}

		force_inline static MonoImage* get_with_hash (hash_t hash) noexcept
		{
			ssize_t index = find_index (hash);
			if (index < 0) {
				return nullptr;
			}

			return get_from_index (static_cast<size_t>(index));
		}
#endif // def RELEASE && def ANDROID && def NET6

#if defined (NET6)
		force_inline static MonoImage* load (dynamic_local_string<SENSIBLE_PATH_MAX> const& name, MonoAssemblyLoadContextGCHandle alc_gchandle, hash_t name_hash, uint8_t *assembly_data, uint32_t assembly_data_size) noexcept
		{
			MonoImage *image = mono_image_open_from_data_alc (
				alc_gchandle,
				reinterpret_cast<char*>(assembly_data),
				assembly_data_size,
				0 /* need_copy */,
				nullptr /* status */,
				name.get ()
			);

			return stash_and_return (image, name_hash);
		}

		force_inline static MonoImage* load (dynamic_local_string<SENSIBLE_PATH_MAX> const& name, MonoAssemblyLoadContextGCHandle alc_gchandle, uint8_t *assembly_data, uint32_t assembly_data_size) noexcept
		{
			return load (name, alc_gchandle, xxhash::hash (name.get (), name.length ()), assembly_data, assembly_data_size);
		}
#endif

		force_inline static MonoImage* load (dynamic_local_string<SENSIBLE_PATH_MAX> const& name, bool ref_only, hash_t name_hash, uint8_t *assembly_data, uint32_t assembly_data_size) noexcept
		{
			MonoImage *image = mono_image_open_from_data_with_name (
				reinterpret_cast<char*>(assembly_data),
				assembly_data_size,
				0,
				nullptr,
				ref_only,
				name.get ()
			);

			return stash_and_return (image, name_hash);
		}

		force_inline static MonoImage* load (dynamic_local_string<SENSIBLE_PATH_MAX> const& name, bool ref_only, uint8_t *assembly_data, uint32_t assembly_data_size) noexcept
		{
			return load (name, ref_only, xxhash::hash (name.get (), name.length ()), assembly_data, assembly_data_size);
		}

	private:
		force_inline static ssize_t find_index (hash_t hash) noexcept
		{
#if defined (RELEASE) && defined (ANDROID)
			return Search::binary_search (hash, assembly_image_cache_index, application_config.number_of_assemblies_in_apk);
#else
			return 0;
#endif // def RELEASE && def ANDROID
		}

		force_inline static MonoImage* stash_and_return (MonoImage *image, [[maybe_unused]] hash_t hash) noexcept
		{
#if defined (RELEASE) && defined (ANDROID) && defined (NET6)
			ssize_t index = find_index (hash);
			if (index < 0) {
				// TODO: Warn?
				return image;
			}

			// We don't need to worry about locking here.  Even if we're overwriting an entry just set from another
			// thread, the image pointer is going to be the same (at least currently, it will change when we have
			// support for unloadable Assembly Load Contexts) and the actual write operation to the destination is
			// atomic
			assembly_image_cache[index] = image;
#endif // def RELEASE && def ANDROID && def NET6
			return image;
		}
	};
}
#endif // ndef __MONO_IMAGE_LOADER_HH
