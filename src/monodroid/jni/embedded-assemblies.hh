// Dear Emacs, this is a -*- C++ -*- header
#ifndef INC_MONODROID_EMBEDDED_ASSEMBLIES_H
#define INC_MONODROID_EMBEDDED_ASSEMBLIES_H

#include <cstring>
#include <mono/metadata/assembly.h>

struct TypeMapHeader;

namespace xamarin::android::internal {
#if defined (DEBUG) || !defined (ANDROID)
	struct TypeMappingInfo;
#endif
	class EmbeddedAssemblies
	{
		enum class FileType : uint8_t
		{
			DebugInfo,
			Config,
			Assembly,
		};

		struct XamarinBundledAssembly
		{
			const char *name;
			const char *apk_name;
			int         apk_fd;
			bool        config_and_symbols_processed;
			FileType    type;
			off_t       data_offset;
			size_t      data_size;
			size_t      mmap_size;
			uint8_t    *mmap_file_data;
		};

	private:
		// Amount of RAM that is allowed to be left unused ("wasted") in the `bundled_assemblies`
		// array after all the APK assemblies are loaded. This is to avoid unnecessary `realloc`
		// calls in order to save time.
		static constexpr size_t BUNDLED_ASSEMBLIES_EXCESS_ITEMS_LIMIT = 256 * sizeof(XamarinBundledAssembly);

		static constexpr char  ZIP_CENTRAL_MAGIC[] = "PK\1\2";
		static constexpr char  ZIP_LOCAL_MAGIC[]   = "PK\3\4";
		static constexpr char  ZIP_EOCD_MAGIC[]    = "PK\5\6";
		static constexpr size_t ZIP_EOCD_LEN        = 22;
		static constexpr size_t ZIP_CENTRAL_LEN     = 46;
		static constexpr size_t ZIP_LOCAL_LEN       = 30;
		static constexpr char assemblies_prefix[] = "assemblies/";
		static constexpr char config_ext[]         = ".config";
#if defined (DEBUG) || !defined (ANDROID)
		static constexpr char override_typemap_entry_name[] = ".__override__";
#endif
		static const char *suffixes[];

	public:
		/* filename is e.g. System.dll, System.dll.mdb, System.pdb */
		using monodroid_should_register = bool (*)(const char *filename);

	public:
#if defined (DEBUG) || !defined (ANDROID)
		void try_load_typemaps_from_directory (const char *path);
#endif
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
		void bundled_assemblies_cleanup ();

	private:
		size_t register_from (const char *apk_file, monodroid_should_register should_register);
		void gather_bundled_assemblies_from_apk (const char* apk, monodroid_should_register should_register);
		MonoAssembly* open_from_bundles (MonoAssemblyName* aname, bool ref_only);
		void load_assembly_config_from_bundles (const char *aname);
		void load_assembly_debug_info_from_bundles (const char *aname, const char *assembly_file_name);
		void extract_int (const char **header, const char *source_apk, const char *source_entry, const char *key_name, int *value);
#if defined (DEBUG) || !defined (ANDROID)
		bool add_type_mapping (TypeMappingInfo **info, const char *source_apk, const char *source_entry, const char *addr);
#endif // DEBUG || !ANDROID
		void md_mmap_apk_file (XamarinBundledAssembly &assembly);

		static MonoAssembly* open_from_bundles_full (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
		static MonoAssembly* open_from_bundles_refonly (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
		static int TypeMappingInfo_compare_key (const void *a, const void *b);
		const char *find_entry_in_type_map (const char *name, uint8_t map[], TypeMapHeader& header);

		void zip_load_entries (int fd, const char *apk_name, monodroid_should_register should_register);
		bool zip_read_cd_info (int fd, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries);
		bool zip_adjust_data_offset (int fd, size_t local_header_offset, uint32_t &data_start_offset);
		bool zip_extract_cd_info (uint8_t* buf, size_t buf_len, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries);
		bool zip_ensure_valid_params (uint8_t* buf, size_t buf_len, size_t index, size_t to_read);
		bool zip_read_field (uint8_t* buf, size_t buf_len, size_t index, uint16_t& u);
		bool zip_read_field (uint8_t* buf, size_t buf_len, size_t index, uint32_t& u);
		bool zip_read_field (uint8_t* buf, size_t buf_len, size_t index, uint8_t (&sig)[4]);
		bool zip_read_field (uint8_t* buf, size_t buf_len, size_t index, size_t count, char*& characters);
		bool zip_read_entry_info (uint8_t* buf, size_t buf_len, size_t& buf_offset, uint16_t& compression_method, uint32_t& local_header_offset, uint32_t& file_size, char*& file_name);

		const char* get_assemblies_prefix () const
		{
			return assemblies_prefix_override != nullptr ? assemblies_prefix_override : assemblies_prefix;
		}

		template<typename T>
		T get_mmap_file_data (XamarinBundledAssembly& xba)
		{
			if (xba.mmap_file_data == nullptr)
				md_mmap_apk_file (xba);
			return reinterpret_cast<T>(xba.mmap_file_data);
		}

		void resize_bundled_assemblies (size_t new_size);

	private:
		bool                    register_debug_symbols;
		XamarinBundledAssembly *bundled_assemblies = nullptr;
		size_t                  bundled_assemblies_count;
		size_t                  bundled_assemblies_size;
		bool                    bundled_assemblies_have_configs;
		bool                    bundled_assemblies_have_debug_info;
		bool                    first_assembly_load_hook_called = false;
#if defined (DEBUG) || !defined (ANDROID)
		TypeMappingInfo       *java_to_managed_maps;
		TypeMappingInfo       *managed_to_java_maps;
#endif // DEBUG || !ANDROID
		const char            *assemblies_prefix_override = nullptr;
	};
}

MONO_API int monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix);
#endif /* INC_MONODROID_EMBEDDED_ASSEMBLIES_H */
