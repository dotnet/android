// Dear Emacs, this is a -*- C++ -*- header
#ifndef INC_MONODROID_EMBEDDED_ASSEMBLIES_H
#define INC_MONODROID_EMBEDDED_ASSEMBLIES_H

#include <cstring>
#include "dylib-mono.h"
#include "unzip.h"
#include "ioapi.h"

namespace xamarin { namespace android { namespace internal {
	struct TypeMappingInfo;
	struct md_mmap_info;

	class EmbeddedAssemblies
	{
	private:
		static constexpr char assemblies_prefix[] = "assemblies/";
		static constexpr char override_typemap_entry_name[] = ".__override__";
		static const char *suffixes[];

	public:
		/* filename is e.g. System.dll, System.dll.mdb, System.pdb */
		using monodroid_should_register = bool (*)(const char *filename);

	public:
		void try_load_typemaps_from_directory (const char *path);
		void install_preload_hooks ();
		const char* typemap_java_to_managed (const char *java);
		const char* typemap_managed_to_java (const char *managed);

		/* returns current number of *all* assemblies found from all invocations */
		template<bool (*should_register_fn)(const char*)>
		size_t register_from (const char *apk_file)
		{
			static_assert (should_register_fn != nullptr, "should_register_fn is a required template parameter");
			return register_from (apk_file, should_register_fn);
		}

		bool get_register_debug_symbols () const
		{
			return register_debug_symbols;
		}

		void set_register_debug_symbols (bool value)
		{
			register_debug_symbols = value;
		}

		void set_assemblies_prefix (const char *prefix);

	private:
		size_t register_from (const char *apk_file, monodroid_should_register should_register);
		bool gather_bundled_assemblies_from_apk (const char* apk, monodroid_should_register should_register);
		MonoAssembly* open_from_bundles (MonoAssemblyName* aname, bool ref_only);
		void extract_int (const char **header, const char *source_apk, const char *source_entry, const char *key_name, int *value);
		bool add_type_mapping (TypeMappingInfo **info, const char *source_apk, const char *source_entry, const char *addr);
		bool register_debug_symbols_for_assembly (const char *entry_name, MonoBundledAssembly *assembly, const mono_byte *debug_contents, int debug_size);

		static md_mmap_info md_mmap_apk_file (int fd, uLong offset, uLong size, const char* filename, const char* apk);
		static void* md_mmap_open_file (void *opaque, const char *filename, int mode);
		static uLong md_mmap_read_file (void *opaque, void *stream, void *buf, uLong size);
		static long md_mmap_tell_file (void *opaque, void *stream);
		static long md_mmap_seek_file (void *opaque, void *stream, uLong offset, int origin);
		static int md_mmap_close_file (void *opaque, void *stream);
		static int md_mmap_error_file (void *opaque, void *stream);

		static MonoAssembly* open_from_bundles_full (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
		static MonoAssembly* open_from_bundles_refonly (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
		static int TypeMappingInfo_compare_key (const void *a, const void *b);

		const char* get_assemblies_prefix () const
		{
			return assemblies_prefix_override != nullptr ? assemblies_prefix_override : assemblies_prefix;
		}

	private:
		bool                   register_debug_symbols;
		MonoBundledAssembly  **bundled_assemblies;
		size_t                 bundled_assemblies_count;
		TypeMappingInfo       *java_to_managed_maps;
		TypeMappingInfo       *managed_to_java_maps;
		const char            *assemblies_prefix_override = nullptr;
	};
}}}

MONO_API int monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix);
#endif /* INC_MONODROID_EMBEDDED_ASSEMBLIES_H */
