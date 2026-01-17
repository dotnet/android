#pragma once

#include <elf.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

#include <cerrno>
#include <concepts>
#include <cstdio>
#include <optional>
#include <string_view>

#include "../constants.hh"
#include <shared/helpers.hh>
#include <runtime-base/jni-wrappers.hh>
#include "logger.hh"
#include <runtime-base/strings.hh>

#if !defined(XA_HOST_NATIVEAOT)
#include "archive-dso-stub-config.hh"
#endif

namespace xamarin::android {
	namespace detail {
		template<typename T>
		concept PathComponentString = requires {
			std::same_as<std::remove_cvref_t<T>, char*> ||
			std::same_as<std::remove_cvref_t<T>, std::string_view> ||
			std::same_as<std::remove_cvref_t<T>, std::string>;
		};

		template<class T, size_t MaxBufferStorage>
		concept PathBuffer = requires {
			std::derived_from<std::remove_cvref<T>, dynamic_local_storage<MaxBufferStorage>> ||
			std::derived_from<std::remove_cvref<T>, static_local_storage<MaxBufferStorage>>;
		};

		struct mmap_info
		{
			void   *area;
			size_t	size;
		};
	}

	class Util
	{
		static constexpr inline std::array<char, 16> hex_map {
			'0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', 'a', 'b', 'c', 'd', 'e', 'f',
		};

	public:
		static int create_directory (const char *pathname, mode_t mode);

		static auto create_directory (std::string_view const& dir, mode_t mode) noexcept -> int
		{
			return create_directory (dir.data (), mode);
		}

		static void create_public_directory (std::string_view const& dir);
		static auto monodroid_fopen (std::string_view const& filename, std::string_view const& mode) noexcept -> FILE*;
		static void set_world_accessable (std::string_view const& path);
		static auto set_world_accessible (int fd) noexcept -> bool;

		// Puts higher half of the `value` byte as a hexadecimal character in `high_half` and
		// the lower half in `low_half`
		static void to_hex (uint8_t value, char &high_half, char &low_half) noexcept
		{
			high_half = hex_map[(value & 0xf0) >> 4];
			low_half = hex_map[value & 0x0f];
		}

		static auto should_log (LogCategories category) noexcept -> bool
		{
			return (log_categories & category) != 0;
		}

	private:
		static auto fs_entry_is_mode (struct stat const& s, mode_t mode) noexcept -> bool
		{
			return (s.st_mode & S_IFMT) == mode;
		}

		static auto exists_and_is_mode (std::string_view const& path, mode_t mode) noexcept -> bool
		{
			struct stat s;

			if (::stat (path.data (), &s) == 0 && fs_entry_is_mode (s, mode)) {
				return true;
			}

			return false;
		}

		static auto file_exists_no_null_check (const char *file) noexcept -> bool
		{
			return exists_and_is_mode (file, S_IFREG);
		}

	public:
		static auto dir_exists (std::string_view const& dir_path) noexcept -> bool
		{
			return exists_and_is_mode (dir_path, S_IFDIR);
		}

		[[gnu::flatten]]
		static auto file_exists (std::string_view const& file) noexcept -> bool
		{
			return file_exists_no_null_check (file.data ());
		}

		static auto file_exists (const char *file) noexcept -> bool
		{
			if (file == nullptr) {
				return false;
			}

			return file_exists_no_null_check (file);
		}

		template<size_t MaxStackSize>
		static auto file_exists (dynamic_local_string<MaxStackSize> const& file) noexcept -> bool
		{
			if (file.empty ()) {
				return false;
			}

			return file_exists_no_null_check (file.get ());
		}

		static auto file_exists (int dirfd, std::string_view const& file) noexcept -> bool
		{
			struct stat sbuf;
			return fstatat (dirfd, file.data (), &sbuf, 0) == 0 && fs_entry_is_mode (sbuf, S_IFREG);
		}

#if !defined (XA_HOST_NATIVEAOT)
		static auto get_file_size_at (int dirfd, const char *file_name) noexcept -> std::optional<size_t>
		{
			struct stat sbuf;
			if (fstatat (dirfd, file_name, &sbuf, 0) == -1) {
				log_warn (LOG_ASSEMBLY, "Failed to stat file '{}': {}", file_name, std::strerror (errno));
				return std::nullopt;
			}

			return static_cast<size_t>(sbuf.st_size);
		}

		static auto get_file_size_at (int dirfd, std::string_view const& file_name) noexcept -> std::optional<size_t>
		{
			return get_file_size_at (dirfd, file_name.data ());
		}
#endif // ndef XA_HOST_NATIVEAOT

		[[gnu::flatten, gnu::always_inline]]
		static void set_environment_variable (const char *name, const char *value) noexcept
		{
			log_debug (
				LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
				"Setting environment variable %s = '%s'",
#else
				"Setting environment variable {} = '{}'"sv,
#endif
				optional_string (name),
				optional_string (value)
			);

			if (::setenv (name, value, 1) < 0) {
				log_warn (
					LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
					"Failed to set environment variable '%s': %s",
#else
					"Failed to set environment variable '{}': {}"sv,
#endif
					name,
					::strerror (errno)
				);
			}
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_environment_variable (std::string_view const& name, jstring_wrapper& value) noexcept
		{
			set_environment_variable (name.data (), value.get_cstr ());
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_environment_variable (std::string_view const& name, std::string_view const& value) noexcept
		{
			set_environment_variable (name.data (), value.data ());
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_environment_variable_for_directory (std::string_view const& name, jstring_wrapper& value, bool createDirectory, mode_t mode) noexcept
		{
			if (createDirectory) {
				int rv = create_directory (value.get_cstr (), mode);
				if (rv < 0 && errno != EEXIST) {
					log_warn (
						LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
						"Failed to create directory '%s' for environment variable '%s'. %s",
						value.get_cstr (),
						name.data (),
#else
						"Failed to create directory '{}' for environment variable '{}'. {}"sv,
						value.get_string_view (),
						name,
#endif
						::strerror (errno)
					);
				}
			}
			set_environment_variable (name, value);
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_environment_variable_for_directory (std::string_view const& name, jstring_wrapper &value) noexcept
		{
			set_environment_variable_for_directory (name, value, true, Constants::DEFAULT_DIRECTORY_MODE);
		}

		static int monodroid_getpagesize () noexcept
		{
			return page_size;
		}

#if !defined (XA_HOST_NATIVEAOT)
		static detail::mmap_info mmap_file (int fd, uint32_t offset, size_t size, std::string_view const& filename) noexcept
		{
			detail::mmap_info file_info;
			detail::mmap_info mmap_info;

			size_t pageSize       = static_cast<size_t>(Util::monodroid_getpagesize ());
			size_t offsetFromPage = offset % pageSize;
			size_t offsetPage     = offset - offsetFromPage;
			size_t offsetSize     = size + offsetFromPage;

			mmap_info.area		  = mmap (nullptr, offsetSize, PROT_READ, MAP_PRIVATE, fd, static_cast<off_t>(offsetPage));

			if (mmap_info.area == MAP_FAILED) {
				Helpers::abort_application (
					LOG_ASSEMBLY,
					std::format (
						"Could not mmap APK fd {}: {}; File={}",
						fd,
						strerror (errno),
						filename
					)
				);
			}

			mmap_info.size = offsetSize;
			file_info.area = pointer_add (mmap_info.area, offsetFromPage);
			file_info.size = size;

			log_info (
				LOG_ASSEMBLY,
				"  mmap_start: {:<8p}; mmap_end: {:<8p}	 mmap_len: {:<12}  file_start: {:<8p}  file_end: {:<8p}	 file_len: {:<12}	  apk descriptor: {}  file: {}"sv,
				mmap_info.area,
				pointer_add (mmap_info.area, mmap_info.size),
				mmap_info.size,
				file_info.area,
				pointer_add (file_info.area, file_info.size),
				file_info.size,
				fd,
				filename
			);

			return file_info;
		}

		[[gnu::always_inline]]
		static std::tuple<void*, size_t> get_wrapper_dso_payload_pointer_and_size (detail::mmap_info const& map_info, std::string_view const& file_name) noexcept
		{
			using Elf_Header = std::conditional_t<Constants::is_64_bit_target, Elf64_Ehdr, Elf32_Ehdr>;
			using Elf_SHeader = std::conditional_t<Constants::is_64_bit_target, Elf64_Shdr, Elf32_Shdr>;

			const void* const mapped_elf = map_info.area;
			auto elf_bytes = static_cast<const uint8_t* const>(mapped_elf);
			auto elf_header = reinterpret_cast<const Elf_Header*const>(mapped_elf);

			if constexpr (Constants::is_debug_build) {
				// In debug mode we might be dealing with plain data, without DSO wrapper
				if (elf_header->e_ident[EI_MAG0] != ELFMAG0 ||
					elf_header->e_ident[EI_MAG1] != ELFMAG1 ||
					elf_header->e_ident[EI_MAG2] != ELFMAG2 ||
					elf_header->e_ident[EI_MAG3] != ELFMAG3) {
						log_debug (LOG_ASSEMBLY, "Not an ELF image: {}", file_name);
						// Not an ELF image, just return what we mmapped before
						return { map_info.area, map_info.size };
				}
			}

			auto section_header = reinterpret_cast<const Elf_SHeader*const>(elf_bytes + elf_header->e_shoff);
			Elf_SHeader const& payload_hdr = section_header[ArchiveDSOStubConfig::PayloadSectionIndex];

			return {
				const_cast<void*>(reinterpret_cast<const void*const> (elf_bytes + ArchiveDSOStubConfig::PayloadSectionOffset)),
				payload_hdr.sh_size
			};
		}
#endif // ndef XA_HOST_NATIVEAOT

		static auto is_path_rooted (const char *path) noexcept -> bool
		{
			if (path == nullptr) {
				return false;
			}

			return path [0] == '/';
		}

		static auto is_path_rooted (std::string_view const& path) noexcept -> bool
		{
			if (path.empty ()) {
				return false;
			}

			return path[0] == '/';
		}

		[[gnu::flatten, gnu::always_inline]]
		static auto path_has_directory_components (std::string_view const& path) noexcept -> bool
		{
			return !path.empty () && path.contains ('/');
		}

	private:
		// TODO: needs some work to accept mixed params of different accepted types
		template<size_t MaxStackSpace, detail::PathBuffer<MaxStackSpace> TBuffer, detail::PathComponentString ...TPart>
		static void path_combine_common (TBuffer& buf, TPart&&... parts) noexcept
		{
			buf.clear ();

			for (auto const& part : {parts...}) {
				if (!buf.empty ()) {
					buf.append ("/"sv);
				}

				if constexpr (std::same_as<std::remove_cvref_t<decltype(part)>, char*>) {
					if (part != nullptr) {
						buf.append_c (part);
					}
				} else {
					buf.append (part);
				}
			}
		}

	public:
		template<size_t MaxStackSpace, detail::PathComponentString ...TParts>
		static void path_combine (dynamic_local_string<MaxStackSpace>& buf, TParts&&... parts) noexcept
		{
			path_combine_common<MaxStackSpace> (buf, std::forward<TParts>(parts)...);
		}

		template<size_t MaxStackSpace, detail::PathComponentString ...TParts>
		static void path_combine (static_local_string<MaxStackSpace>& buf, TParts&&... parts) noexcept
		{
			path_combine_common<MaxStackSpace> (buf, std::forward<TParts>(parts)...);
		}

	private:
		static inline int page_size = getpagesize ();
	};
}
