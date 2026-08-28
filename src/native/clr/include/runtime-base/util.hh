#pragma once

#include <elf.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

#include <cerrno>
#include <cstdlib>
#include <cstdio>
#include <cstring>
#include <limits>
#include <optional>
#include <string_view>

#include "../constants.hh"
#include <shared/helpers.hh>
#include <runtime-base/jni-wrappers.hh>
#include "logger.hh"

#if !defined(XA_HOST_NATIVEAOT)
#include "archive-dso-stub-config.hh"
#endif

namespace xamarin::android {
	namespace detail {
		struct mmap_info
		{
			void   *area;
			size_t	size;
		};
	}

	class Util
	{
		static constexpr inline char hex_map [16] {
			'0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', 'a', 'b', 'c', 'd', 'e', 'f',
		};

	public:
		static constexpr size_t LocalPathBufferSize = Constants::SENSIBLE_PATH_MAX;

		// Returns a copy of `str` allocated with `malloc`, aborting the application if the
		// allocation fails. Used for values which are set once, early during startup, and which
		// then live for as long as the process does - the copies are never freed.
		static auto duplicate_string (const char *str) noexcept -> char*
		{
			char *ret = strdup (str);
			if (ret == nullptr) [[unlikely]] {
				Helpers::abort_application (LOG_DEFAULT, "Unable to allocate memory for a string copy");
			}

			return ret;
		}

		static int create_directory (const char *pathname, mode_t mode);

		static auto create_directory (std::string_view const& dir, mode_t mode) noexcept -> int
		{
			return create_directory (dir.data (), mode);
		}

		static void create_public_directory (const char *dir);
		static auto monodroid_fopen (const char *filename, const char *mode) noexcept -> FILE*;
		static void set_world_accessable (const char *path);
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

		static auto file_exists (int dirfd, std::string_view const& file) noexcept -> bool
		{
			struct stat sbuf;
			return fstatat (dirfd, file.data (), &sbuf, 0) == 0 && fs_entry_is_mode (sbuf, S_IFREG);
		}

		static auto get_file_size_at (int dirfd, const char *file_name) noexcept -> std::optional<size_t>
		{
			struct stat sbuf;
			if (fstatat (dirfd, file_name, &sbuf, 0) == -1) {
				log_warnf (LOG_ASSEMBLY, "Failed to stat file '%s': %s", optional_string (file_name), std::strerror (errno));
				return std::nullopt;
			}

			return static_cast<size_t>(sbuf.st_size);
		}

		static auto get_file_size_at (int dirfd, std::string_view const& file_name) noexcept -> std::optional<size_t>
		{
			return get_file_size_at (dirfd, file_name.data ());
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_environment_variable (const char *name, const char *value) noexcept
		{
			log_debugf (LOG_DEFAULT, "Setting environment variable %s = '%s'", optional_string (name), optional_string (value));
			if (::setenv (name, value, 1) < 0) {
				log_warnf (LOG_DEFAULT, "Failed to set environment variable '%s': %s", optional_string (name), ::strerror (errno));
			}
		}

		[[gnu::flatten, gnu::always_inline]]
		static void set_environment_variable_if_unset (std::string_view const& name, jstring_wrapper& value) noexcept
		{
			log_debugf (
				LOG_DEFAULT,
				"Setting environment variable %.*s = '%s' if unset",
				static_cast<int>(name.length ()),
				name.data (),
				optional_string (value.get_cstr ())
			);
			if (::setenv (name.data (), value.get_cstr (), 0) < 0) {
				log_warnf (
					LOG_DEFAULT,
					"Failed to set environment variable '%.*s': %s",
					static_cast<int>(name.length ()),
					name.data (),
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
					log_warnf (
						LOG_DEFAULT,
						"Failed to create directory '%s' for environment variable '%.*s'. %s",
						optional_string (value.get_cstr ()),
						static_cast<int>(name.length ()),
						name.data (),
						strerror (errno)
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
				Helpers::abort_applicationf (
					LOG_ASSEMBLY,
					std::source_location::current (),
					"Could not mmap APK fd %d: %s; File=%.*s",
					fd,
					strerror (errno),
					static_cast<int>(filename.length ()),
					filename.data ()
				);
			}

			mmap_info.size = offsetSize;
			file_info.area = pointer_add (mmap_info.area, offsetFromPage);
			file_info.size = size;

			log_infof (
				LOG_ASSEMBLY,
				"  mmap_start: %-8p; mmap_end: %-8p\t mmap_len: %-12zu  file_start: %-8p  file_end: %-8p\t file_len: %-12zu\t  apk descriptor: %d  file: %.*s",
				mmap_info.area,
				pointer_add (mmap_info.area, mmap_info.size),
				mmap_info.size,
				file_info.area,
				pointer_add (file_info.area, file_info.size),
				file_info.size,
				fd,
				static_cast<int>(filename.length ()),
				filename.data ()
			);

			return file_info;
		}

#if !defined(XA_HOST_NATIVEAOT)
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
						log_debugf (
							LOG_ASSEMBLY,
							"Not an ELF image: %.*s",
							static_cast<int>(file_name.length ()),
							file_name.data ()
						);
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

		[[gnu::flatten, gnu::always_inline]]
		static auto ends_with (const char *value, const char *suffix) noexcept -> bool
		{
			if (value == nullptr || suffix == nullptr) {
				return false;
			}

			size_t value_length = strlen (value);
			size_t suffix_length = strlen (suffix);
			if (suffix_length > value_length) {
				return false;
			}

			return memcmp (value + value_length - suffix_length, suffix, suffix_length) == 0;
		}

		// Returns the path length excluding NUL, or the negative required capacity including NUL.
		static auto format_joined_path (char *buffer, size_t buffer_size, std::string_view first, std::string_view second) noexcept -> ssize_t
		{
			bool remove_duplicate_separator = first.ends_with ('/') && second.starts_with ('/');
			bool add_separator = !first.empty () && !second.empty () && !first.ends_with ('/') && !second.starts_with ('/');
			size_t second_offset = remove_duplicate_separator ? 1uz : 0uz;
			size_t path_length = Helpers::add_with_overflow_check<size_t> (first.length (), second.length () - second_offset);
			if (add_separator) {
				path_length = Helpers::add_with_overflow_check<size_t> (path_length, 1uz);
			}

			size_t required_capacity = Helpers::add_with_overflow_check<size_t> (path_length, 1uz);
			abort_unless (required_capacity <= static_cast<size_t>(std::numeric_limits<ssize_t>::max ()), "Joined path is too long");
			if (buffer == nullptr || buffer_size < required_capacity) {
				return -static_cast<ssize_t>(required_capacity);
			}

			char *destination = buffer;
			if (!first.empty ()) {
				memcpy (destination, first.data (), first.length ());
				destination += first.length ();
			}
			if (add_separator) {
				*destination++ = '/';
			}
			size_t second_length = second.length () - second_offset;
			if (second_length > 0) {
				memcpy (destination, second.data () + second_offset, second_length);
			}
			buffer [path_length] = '\0';

			return static_cast<ssize_t>(path_length);
		}

		static auto join_paths (char *stack_buffer, size_t stack_buffer_size, std::string_view first, std::string_view second) noexcept -> char*
		{
			ssize_t result = format_joined_path (stack_buffer, stack_buffer_size, first, second);
			if (result >= 0) {
				return stack_buffer;
			}

			size_t required_capacity = static_cast<size_t>(-result);
			char *heap_buffer = static_cast<char*> (std::malloc (required_capacity));
			abort_unless (heap_buffer != nullptr, "Failed to allocate joined path");
			result = format_joined_path (heap_buffer, required_capacity, first, second);
			abort_unless (result >= 0, "Failed to join path using the required capacity");
			return heap_buffer;
		}

		static auto get_dso_name_length (std::string_view const& name, bool add_lib_prefix) noexcept -> size_t
		{
			std::string_view prefix = add_lib_prefix && !name.starts_with (Constants::DSO_PREFIX) ? Constants::DSO_PREFIX : std::string_view {};
			std::string_view suffix = name.ends_with (Constants::dso_suffix) ? std::string_view {} : Constants::dso_suffix;

			size_t name_length = Helpers::add_with_overflow_check<size_t> (prefix.length (), name.length ());
			name_length = Helpers::add_with_overflow_check<size_t> (name_length, suffix.length ());
			return name_length;
		}

		// Returns the name length excluding NUL, or the negative required capacity including NUL.
		static auto format_dso_name (std::string_view const& name, bool add_lib_prefix, char *buffer, size_t buffer_size) noexcept -> ssize_t
		{
			size_t name_length = get_dso_name_length (name, add_lib_prefix);
			size_t required_capacity = Helpers::add_with_overflow_check<size_t> (name_length, 1uz);
			abort_unless (required_capacity <= static_cast<size_t>(std::numeric_limits<ssize_t>::max ()), "DSO name is too long");
			if (buffer == nullptr || buffer_size < required_capacity) {
				return -static_cast<ssize_t>(required_capacity);
			}

			std::string_view prefix = add_lib_prefix && !name.starts_with (Constants::DSO_PREFIX) ? Constants::DSO_PREFIX : std::string_view {};
			std::string_view suffix = name.ends_with (Constants::dso_suffix) ? std::string_view {} : Constants::dso_suffix;
			char *destination = buffer;
			if (!prefix.empty ()) {
				memcpy (destination, prefix.data (), prefix.length ());
				destination += prefix.length ();
			}
			if (!name.empty ()) {
				memcpy (destination, name.data (), name.length ());
				destination += name.length ();
			}
			if (!suffix.empty ()) {
				memcpy (destination, suffix.data (), suffix.length ());
			}
			buffer [name_length] = '\0';

			return static_cast<ssize_t>(name_length);
		}

		static auto format_dso_name (char *stack_buffer, size_t stack_buffer_size, std::string_view const& name, bool add_lib_prefix) noexcept -> char*
		{
			ssize_t result = format_dso_name (name, add_lib_prefix, stack_buffer, stack_buffer_size);
			if (result >= 0) {
				return stack_buffer;
			}

			size_t required_capacity = static_cast<size_t>(-result);
			char *heap_buffer = static_cast<char*> (std::malloc (required_capacity));
			abort_unless (heap_buffer != nullptr, "Failed to allocate DSO name");
			result = format_dso_name (name, add_lib_prefix, heap_buffer, required_capacity);
			abort_unless (result >= 0, "Failed to format DSO name using the required capacity");
			return heap_buffer;
		}

	private:
		static inline int page_size = getpagesize ();
	};
}
