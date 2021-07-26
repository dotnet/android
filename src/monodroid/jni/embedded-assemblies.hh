// Dear Emacs, this is a -*- C++ -*- header
#ifndef INC_MONODROID_EMBEDDED_ASSEMBLIES_H
#define INC_MONODROID_EMBEDDED_ASSEMBLIES_H

#include <array>

#undef HAVE_CONCEPTS

// Xcode has supports for concepts only since 12.5
#if __has_include (<concepts>)
#define HAVE_CONCEPTS
#include <concepts>
#endif // __has_include

#include <cstring>
#include <limits>
#include <functional>
#include <vector>

#include <mono/metadata/object.h>
#include <mono/metadata/assembly.h>

#if defined (NET6)
#include <mono/metadata/mono-private-unstable.h>
#endif

#include "strings.hh"
#include "xamarin-app.hh"
#include "cpp-util.hh"

struct TypeMapHeader;

namespace xamarin::android::internal {
#if defined (DEBUG) || !defined (ANDROID)
	struct TypeMappingInfo;
#endif

#if defined (HAVE_CONCEPTS)
	template<typename T>
	concept ByteArrayContainer = requires (T a) {
		a.size ();
		a.data ();
		requires std::same_as<typename T::value_type, uint8_t>;
	};
#else
#define ByteArrayContainer class
#endif

	class EmbeddedAssemblies
	{
		struct md_mmap_info {
			void   *area;
			size_t  size;
		};

	private:
		static constexpr char  ZIP_CENTRAL_MAGIC[] = "PK\1\2";
		static constexpr char  ZIP_LOCAL_MAGIC[]   = "PK\3\4";
		static constexpr char  ZIP_EOCD_MAGIC[]    = "PK\5\6";
		static constexpr off_t ZIP_EOCD_LEN        = 22;
		static constexpr off_t ZIP_CENTRAL_LEN     = 46;
		static constexpr off_t ZIP_LOCAL_LEN       = 30;
		static constexpr char assemblies_prefix[] = "assemblies/";
#if defined (DEBUG) || !defined (ANDROID)
		static constexpr char override_typemap_entry_name[] = ".__override__";
#endif

	public:
		/* filename is e.g. System.dll, System.dll.mdb, System.pdb */
		using monodroid_should_register = bool (*)(const char *filename);

	public:
#if defined (DEBUG) || !defined (ANDROID)
		void try_load_typemaps_from_directory (const char *path);
#endif
		const char* typemap_managed_to_java (MonoReflectionType *type, const uint8_t *mvid);

		void install_preload_hooks_for_appdomains ();
#if defined (NET6)
		void install_preload_hooks_for_alc ();
#endif // def NET6
		MonoReflectionType* typemap_java_to_managed (MonoString *java_type);

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

#if defined (NET6)
		void get_runtime_config_blob (const char *& area, uint32_t& size) const
		{
			area = static_cast<char*>(runtime_config_blob_mmap.area);

			abort_unless (runtime_config_blob_mmap.size < std::numeric_limits<uint32_t>::max (), "Runtime config binary blob size exceeds %u bytes", std::numeric_limits<uint32_t>::max ());
			size = static_cast<uint32_t>(runtime_config_blob_mmap.size);
		}

		bool have_runtime_config_blob () const
		{
			return application_config.have_runtime_config_blob && runtime_config_blob_mmap.area != nullptr;
		}
#endif

	private:
		const char* typemap_managed_to_java (MonoType *type, MonoClass *klass, const uint8_t *mvid);
		MonoReflectionType* typemap_java_to_managed (const char *java_type_name);
		size_t register_from (const char *apk_file, monodroid_should_register should_register);
		void gather_bundled_assemblies_from_apk (const char* apk, monodroid_should_register should_register);
#if defined (NET6)
		MonoAssembly* open_from_bundles (MonoAssemblyName* aname, MonoAssemblyLoadContextGCHandle alc_gchandle, MonoError *error);
#endif // def NET6
		MonoAssembly* open_from_bundles (MonoAssemblyName* aname, bool ref_only);
		MonoAssembly* open_from_bundles (MonoAssemblyName* aname, std::function<MonoImage*(char*, uint32_t, const char*)> loader, bool ref_only);

#if defined (DEBUG) || !defined (ANDROID)
		template<typename H>
		bool typemap_read_header (int dir_fd, const char *file_type, const char *dir_path, const char *file_path, uint32_t expected_magic, H &header, size_t &file_size, int &fd);
		std::unique_ptr<uint8_t[]> typemap_load_index (int dir_fd, const char *dir_path, const char *index_path);
		std::unique_ptr<uint8_t[]> typemap_load_index (TypeMapIndexHeader &header, size_t file_size, int index_fd);
		bool typemap_load_file (int dir_fd, const char *dir_path, const char *file_path, TypeMap &module);
		bool typemap_load_file (BinaryTypeMapHeader &header, const char *dir_path, const char *file_path, int file_fd, TypeMap &module);
		static ssize_t do_read (int fd, void *buf, size_t count);
		const TypeMapEntry *typemap_managed_to_java (const char *managed_type_name);
#endif // DEBUG || !ANDROID
		template<size_t BufferSize>
		bool register_debug_symbols_for_assembly (dynamic_local_string<BufferSize> const& entry_name, MonoBundledAssembly const& assembly, const mono_byte *debug_contents, int debug_size);

		static md_mmap_info md_mmap_apk_file (int fd, uint32_t offset, uint32_t size, const char* filename, const char* apk);
		static MonoAssembly* open_from_bundles_full (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
#if defined (NET6)
		static MonoAssembly* open_from_bundles (MonoAssemblyLoadContextGCHandle alc_gchandle, MonoAssemblyName *aname, char **assemblies_path, void *user_data, MonoError *error);
#else // def NET6
		static MonoAssembly* open_from_bundles_refonly (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
#endif // ndef NET6
		static void get_assembly_data (MonoBundledAssembly const& e, char*& assembly_data, uint32_t& assembly_data_size);

		void zip_load_entries (int fd, const char *apk_name, monodroid_should_register should_register);
		bool zip_read_cd_info (int fd, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries);
		bool zip_adjust_data_offset (int fd, size_t local_header_offset, uint32_t &data_start_offset);

		template<size_t BufSize>
		bool zip_extract_cd_info (std::array<uint8_t, BufSize> const& buf, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries);

		template<class T>
		bool zip_ensure_valid_params (T const& buf, size_t index, size_t to_read) const noexcept;

		// template<size_t BufSize>
		// bool zip_read_field (std::array<uint8_t, BufSize> const& buf, size_t index, uint16_t& u)
		// {
		// 	return zip_read_field_unchecked (buf, u, index);
		// }

		// bool zip_read_field (std::vector<uint8_t> const& buf, size_t index, uint16_t& u)
		// {
		// 	return zip_read_field_unchecked (buf, u, index);
		// }

		template<ByteArrayContainer T>
		bool zip_read_field (T const& src, size_t source_index, uint16_t& dst) const noexcept;

		template<ByteArrayContainer T>
		bool zip_read_field (T const& src, size_t source_index, uint32_t& dst) const noexcept;

		template<ByteArrayContainer T>
		bool zip_read_field (T const& src, size_t source_index, std::array<uint8_t, 4>& dst_sig) const noexcept;

		template<ByteArrayContainer T>
		bool zip_read_field (T const& buf, size_t index, size_t count, dynamic_local_string<SENSIBLE_PATH_MAX>& characters) const noexcept;

		bool zip_read_entry_info (std::vector<uint8_t> const& buf, size_t& buf_offset, uint16_t& compression_method, uint32_t& local_header_offset, uint32_t& file_size, dynamic_local_string<SENSIBLE_PATH_MAX>& file_name);

		const char* get_assemblies_prefix () const
		{
			return assemblies_prefix_override != nullptr ? assemblies_prefix_override : assemblies_prefix;
		}

		size_t get_assemblies_prefix_length () const noexcept
		{
			return assemblies_prefix_override != nullptr ? strlen (assemblies_prefix_override) : sizeof(assemblies_prefix) - 1;
		}

		template<typename Key, typename Entry, int (*compare)(const Key*, const Entry*), bool use_extra_size = false>
		const Entry* binary_search (const Key *key, const Entry *base, size_t nmemb, size_t extra_size = 0);

#if defined (DEBUG) || !defined (ANDROID)
		static int compare_type_name (const char *type_name, const TypeMapEntry *entry);
#else
		static int compare_mvid (const uint8_t *mvid, const TypeMapModule *module);
		static int compare_type_token (const uint32_t *token, const TypeMapModuleEntry *entry);
		static int compare_java_name (const char *java_name, const TypeMapJava *entry);
#endif

	private:
		bool                   register_debug_symbols;
		std::vector<MonoBundledAssembly> bundled_assemblies;
		size_t                 bundled_assembly_index = 0;
#if defined (DEBUG) || !defined (ANDROID)
		TypeMappingInfo       *java_to_managed_maps;
		TypeMappingInfo       *managed_to_java_maps;
		TypeMap               *type_maps;
		size_t                 type_map_count;
#endif // DEBUG || !ANDROID
		const char            *assemblies_prefix_override = nullptr;
#if defined (NET6)
		md_mmap_info           runtime_config_blob_mmap{};
#endif // def NET6
	};
}

#if !defined (NET6)
MONO_API int monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix);
#endif // ndef NET6

#endif /* INC_MONODROID_EMBEDDED_ASSEMBLIES_H */
