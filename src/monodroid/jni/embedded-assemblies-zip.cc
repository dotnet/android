#include <array>
#include <cerrno>
#include <vector>
#include <libgen.h>

#include <mono/metadata/assembly.h>

#include "embedded-assemblies.hh"
#include "globals.hh"
#include "xamarin-app.hh"

using namespace xamarin::android::internal;

// This type is needed when calling read(2) in a MinGW build, as it defines the `count` parameter as `unsigned int`
// instead of `size_t` which then causes the following warning if we pass a value of type `size_t`:
//
//   warning: conversion from ‘size_t’ {aka ‘long long unsigned int’} to ‘unsigned int’ may change value [-Wconversion]
//
#if defined (WINDOWS)
using read_count_type = unsigned int;
#else
using read_count_type = size_t;
#endif

force_inline bool
EmbeddedAssemblies::is_debug_file (dynamic_local_string<SENSIBLE_PATH_MAX> const& name) noexcept
{
	return utils.ends_with (name, ".pdb")
#if !defined (NET)
		|| utils.ends_with (name, ".mdb")
#endif
		;
}

force_inline bool
EmbeddedAssemblies::zip_load_entry_common (size_t entry_index, std::vector<uint8_t> const& buf, dynamic_local_string<SENSIBLE_PATH_MAX> &entry_name, ZipEntryLoadState &state) noexcept
{
	entry_name.clear ();

	bool result = zip_read_entry_info (buf, entry_name, state);

#ifdef DEBUG
	log_info (LOG_ASSEMBLY, "%s entry: %s", state.apk_name, entry_name.get () == nullptr ? "unknown" : entry_name.get ());
#endif
	if (!result || entry_name.empty ()) {
		log_fatal (LOG_ASSEMBLY, "Failed to read Central Directory info for entry %u in APK file %s", entry_index, state.apk_name);
		Helpers::abort_application ();
	}

	if (!zip_adjust_data_offset (state.apk_fd, state)) {
		log_fatal (LOG_ASSEMBLY, "Failed to adjust data start offset for entry %u in APK file %s", entry_index, state.apk_name);
		Helpers::abort_application ();
	}
#ifdef DEBUG
	log_info (LOG_ASSEMBLY, "    ZIP: local header offset: %u; data offset: %u; file size: %u", state.local_header_offset, state.data_offset, state.file_size);
#endif
	if (state.compression_method != 0) {
		return false;
	}

	static_assert (assembly_dso_prefix.size () > 1, "assembly_dso_prefix must be longer than 1 byte");

	if ( // library location is not overridable, so we can test for it beginning with a hardcoded 'l'
		(entry_name.get ()[0] != 'l' || entry_name.length () < assembly_dso_prefix.size () || memcmp (assembly_dso_prefix.data (), entry_name.get (), assembly_dso_prefix.size () - 1) != 0) &&
		(entry_name.get ()[0] != state.prefix[0] || entry_name.length () < state.prefix_len || memcmp (state.prefix, entry_name.get (), state.prefix_len) != 0)
	) {
		return false;
	}
	state.location = entry_name.get ()[0] == 'l' ? EntryLocation::Libs : EntryLocation::Assemblies;

#if defined (NET)
	if (application_config.have_runtime_config_blob && !runtime_config_blob_found && state.location == EntryLocation::Assemblies) {
		if (utils.ends_with (entry_name, SharedConstants::RUNTIME_CONFIG_BLOB_NAME)) {
			runtime_config_blob_found = true;
			runtime_config_blob_mmap = md_mmap_apk_file (state.apk_fd, state.data_offset, state.file_size, entry_name.get ());
			return false;
		}
	}
#endif // def NET

	// assemblies and shared libraries must be 4-byte aligned, or Bad Things happen
	if ((state.data_offset & 0x3) != 0) {
		log_fatal (LOG_ASSEMBLY, "Entry '%s' is located at bad offset %lu within the .apk\n", entry_name.get (), state.data_offset);
		log_fatal (LOG_ASSEMBLY, "You MUST run `zipalign` on %s\n", strrchr (state.apk_name, '/') + 1);
		Helpers::abort_application ();
	}

	return true;
}

force_inline void
EmbeddedAssemblies::zip_load_individual_assembly_entries (std::vector<uint8_t> const& buf, uint32_t num_entries, [[maybe_unused]] monodroid_should_register should_register, ZipEntryLoadState &state) noexcept
{
	dynamic_local_string<SENSIBLE_PATH_MAX> entry_name;
	bool bundled_assemblies_slow_path = bundled_assembly_index >= application_config.number_of_assemblies_in_apk;
	uint32_t max_assembly_name_size = application_config.bundled_assembly_name_width - 1;

	// clang-tidy claims we have a leak in the loop:
	//
	//   Potential leak of memory pointed to by 'assembly_name'
	//
	// This is because we allocate `assembly_name` for a `.config` file, pass it to Mono and we don't free the value.
	// However, clang-tidy can't know that the value is owned by Mono and we must not free it, thus the suppression.
	//
	// NOLINTNEXTLINE(clang-analyzer-unix.Malloc)
	for (size_t i = 0; i < num_entries; i++) {
		bool interesting_entry = zip_load_entry_common (i, buf, entry_name, state);
		if (!interesting_entry) {
			continue;
		}

#if defined (DEBUG)
		const char *last_slash = utils.find_last (entry_name, '/');
		bool entry_is_overridden = last_slash == nullptr ? false : !should_register (last_slash + 1);
#else
		constexpr bool entry_is_overridden = false;
#endif

		if (register_debug_symbols && !entry_is_overridden && is_debug_file (entry_name)) {
			if (bundled_debug_data == nullptr) {
				bundled_debug_data = new std::vector<XamarinAndroidBundledAssembly> ();
				bundled_debug_data->reserve (application_config.number_of_assemblies_in_apk);
			}

			bundled_debug_data->emplace_back ();
			set_debug_entry_data (bundled_debug_data->back (), state.apk_fd, state.data_offset, state.file_size, state.prefix_len, max_assembly_name_size, entry_name);
			continue;
		}

		if (state.location != EntryLocation::Assemblies || !utils.ends_with (entry_name, SharedConstants::DLL_EXTENSION)) {
			continue;
		}

#if defined (DEBUG)
		if (entry_is_overridden)
			continue;
#endif

		if (XA_UNLIKELY (bundled_assembly_index >= application_config.number_of_assemblies_in_apk || bundled_assemblies_slow_path)) {
			if (!bundled_assemblies_slow_path && bundled_assembly_index == application_config.number_of_assemblies_in_apk) {
				log_warn (LOG_ASSEMBLY, "Number of assemblies stored at build time (%u) was incorrect, switching to slow bundling path.");
			}

			if (extra_bundled_assemblies == nullptr) {
				extra_bundled_assemblies = new std::vector<XamarinAndroidBundledAssembly> ();
			}

			extra_bundled_assemblies->emplace_back ();
			// <true> means we need to allocate memory to store the entry name, only the entries pre-allocated during
			// build have valid pointer to the name storage area
			set_entry_data<true> (extra_bundled_assemblies->back (), state.apk_fd, state.data_offset, state.file_size, state.prefix_len, max_assembly_name_size, entry_name);
			continue;
		}

		set_assembly_entry_data (bundled_assemblies [bundled_assembly_index], state.apk_fd, state.data_offset, state.file_size, state.prefix_len, max_assembly_name_size, entry_name);
		bundled_assembly_index++;
		number_of_found_assembly_dsos = bundled_assembly_index;
	}

	have_and_want_debug_symbols = register_debug_symbols && bundled_debug_data != nullptr;
}

#if defined(RELEASE)
force_inline void
EmbeddedAssemblies::zip_load_standalone_dso_entries (std::vector<uint8_t> const& buf, uint32_t num_entries, ZipEntryLoadState &state) noexcept
{
	if (all_required_zip_entries_found ()) {
		return;
	}

	dynamic_local_string<SENSIBLE_PATH_MAX> entry_name;

	log_debug (LOG_ASSEMBLY, "Looking for assembly DSOs in APK, at prefix %s", assembly_dso_prefix);
	for (size_t i = 0; i < num_entries; i++) {
		if (all_required_zip_entries_found ()) {
			need_to_scan_more_apks = false;
			break;
		}

		bool interesting_entry = zip_load_entry_common (i, buf, entry_name, state);
		if (!interesting_entry || state.location != EntryLocation::Libs) {
			continue;
		}

		if (entry_name.length () < assembly_dso_min_length) {
			log_warn (LOG_ASSEMBLY, "APK entry '%s' looks like an assembly DSO, but its name is not long enough. Expected at least %zu characters", entry_name.get (), assembly_dso_min_length);
			continue;
		}

		number_of_found_assembly_dsos++;

		// We have an assembly DSO
		log_info (LOG_ASSEMBLY, "Found an assembly DSO: %s; index: %s; data offset: %u", entry_name.get (), entry_name.get () + (entry_name.length () - 7), state.data_offset);

		bool valid_hex = true;
		auto integer_from_hex_char = []<size_t TLen> (dynamic_local_string<TLen> const& s, size_t pos, bool &is_valid, size_t shift) -> uint16_t
		{
			uint8_t ch = s[pos];
			if (ch >= '0' && ch <= '9') {
				return static_cast<uint16_t>((ch - 48) << shift); // 48 is ASCII '0'
			}

			if (ch >= 'A' && ch <= 'F') {
				return static_cast<uint16_t>((ch - 55) << shift); // ASCII 'A' is 65, and it represents decimal 10
			}

			is_valid = false;
			return static_cast<uint16_t>(0);
		};

		const size_t index_pos = entry_name.length () - assembly_index_start_offset;
		uint16_t index =
			integer_from_hex_char (entry_name, index_pos,     valid_hex, 12u) |
			integer_from_hex_char (entry_name, index_pos + 1, valid_hex, 8u) |
			integer_from_hex_char (entry_name, index_pos + 2, valid_hex, 4u) |
			integer_from_hex_char (entry_name, index_pos + 3, valid_hex, 0u);

		if (!valid_hex) [[unlikely]] {
			log_fatal (LOG_ASSEMBLY, "Unable to determine DSO storage index from '%s'", entry_name.get ());
			Helpers::abort_application ();
		}

		if (index >= xa_assemblies_config.assembly_dso_count) [[unlikely]] {
			log_fatal (LOG_ASSEMBLY, "Index retrieved from '%s' exceeds the maximum allowed value of %u", entry_name.get (), xa_assemblies_config.assembly_dso_count - 1);
			Helpers::abort_application ();
		}

		AssemblyLoadInfo &load_info = xa_assemblies_load_info[index];
		load_info.apk_offset = state.data_offset;
		load_info.apk_data_size = state.file_size;
	}
}
#endif

void
EmbeddedAssemblies::zip_load_entries (int fd, const char *apk_name, [[maybe_unused]] monodroid_should_register should_register)
{
	uint32_t cd_offset;
	uint32_t cd_size;
	uint16_t cd_entries;

	if (!zip_read_cd_info (fd, cd_offset, cd_size, cd_entries)) {
		log_fatal (LOG_ASSEMBLY,  "Failed to read the EOCD record from APK file %s", apk_name);
		Helpers::abort_application ();
	}
#ifdef DEBUG
	log_info (LOG_ASSEMBLY, "Central directory offset: %u", cd_offset);
	log_info (LOG_ASSEMBLY, "Central directory size: %u", cd_size);
	log_info (LOG_ASSEMBLY, "Central directory entries: %u", cd_entries);
#endif
	off_t retval = ::lseek (fd, static_cast<off_t>(cd_offset), SEEK_SET);
	if (retval < 0) {
		log_fatal (LOG_ASSEMBLY, "Failed to seek to central directory position in the APK file %s. %s (result: %d; errno: %d)", apk_name, std::strerror (errno), retval, errno);
		Helpers::abort_application ();
	}

	std::vector<uint8_t>  buf (cd_size);
	ZipEntryLoadState state {
		.apk_fd              = fd,
		.apk_name            = apk_name,
		.prefix              = get_assemblies_prefix (),
		.prefix_len          = get_assemblies_prefix_length (),
		.buf_offset          = 0,
		.compression_method  = 0,
		.local_header_offset = 0,
		.data_offset         = 0,
		.file_size           = 0,
	};

	ssize_t nread = read (fd, buf.data (), static_cast<read_count_type>(buf.size ()));
	if (static_cast<size_t>(nread) != cd_size) {
		log_fatal (LOG_ASSEMBLY, "Failed to read Central Directory from the APK archive %s. %s (nread: %d; errno: %d)", apk_name, std::strerror (errno), nread, errno);
		Helpers::abort_application ();
	}

#if defined (RELEASE)
	if (application_config.have_standalone_assembly_dsos) {
		zip_load_standalone_dso_entries (buf, cd_entries, state);
	} else
#endif // def RELEASE
	{
		zip_load_individual_assembly_entries (buf, cd_entries, should_register, state);
	}
}

template<bool NeedsNameAlloc>
force_inline void
EmbeddedAssemblies::set_entry_data (XamarinAndroidBundledAssembly &entry, int apk_fd, uint32_t data_offset, uint32_t data_size, uint32_t prefix_len, uint32_t max_name_size, dynamic_local_string<SENSIBLE_PATH_MAX> const& entry_name) noexcept
{
	entry.apk_fd = apk_fd;
	if constexpr (NeedsNameAlloc) {
		entry.name = utils.strdup_new (entry_name.get () + prefix_len);
	} else {
		// entry.name is preallocated on build time here and is max_name_size + 1 bytes long, filled with 0s, thus we
		// don't need to append the terminating NUL even for strings of `max_name_size` characters
		strncpy (entry.name, entry_name.get () + prefix_len, max_name_size);
	}
	entry.name_length = std::min (static_cast<uint32_t>(entry_name.length ()) - prefix_len, max_name_size);
	entry.data_offset = data_offset;
	entry.data_size = data_size;
}

force_inline void
EmbeddedAssemblies::set_assembly_entry_data (XamarinAndroidBundledAssembly &entry, int apk_fd, uint32_t data_offset, uint32_t data_size, uint32_t prefix_len, uint32_t max_name_size, dynamic_local_string<SENSIBLE_PATH_MAX> const& entry_name) noexcept
{
	set_entry_data<false> (entry, apk_fd, data_offset, data_size, prefix_len, max_name_size, entry_name);
}

force_inline void
EmbeddedAssemblies::set_debug_entry_data (XamarinAndroidBundledAssembly &entry, int apk_fd, uint32_t data_offset, uint32_t data_size, uint32_t prefix_len, uint32_t max_name_size, dynamic_local_string<SENSIBLE_PATH_MAX> const& entry_name) noexcept
{
	set_entry_data<true> (entry, apk_fd, data_offset, data_size, prefix_len, max_name_size, entry_name);
}

bool
EmbeddedAssemblies::zip_read_cd_info (int fd, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries)
{
	// The simplest case - no file comment
	off_t ret = ::lseek (fd, -ZIP_EOCD_LEN, SEEK_END);
	if (ret < 0) {
		log_error (LOG_ASSEMBLY, "Unable to seek into the APK to find ECOD: %s (ret: %d; errno: %d)", std::strerror (errno), ret, errno);
		return false;
	}

	std::array<uint8_t, ZIP_EOCD_LEN> eocd;
	ssize_t nread = ::read (fd, eocd.data (), static_cast<read_count_type>(eocd.size ()));
	if (nread < 0 || nread != eocd.size ()) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD from the APK: %s (nread: %d; errno: %d)", std::strerror (errno), nread, errno);
		return false;
	}

	size_t index = 0; // signature
	std::array<uint8_t, 4> signature;

	if (!zip_read_field (eocd, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD signature");
		return false;
	}

	if (memcmp (signature.data (), ZIP_EOCD_MAGIC, signature.size ()) == 0) {
		return zip_extract_cd_info (eocd, cd_offset, cd_size, cd_entries);
	}

	// Most probably a ZIP with comment
	constexpr size_t alloc_size = 65535 + ZIP_EOCD_LEN; // 64k is the biggest comment size allowed
	ret = ::lseek (fd, static_cast<off_t>(-alloc_size), SEEK_END);
	if (ret < 0) {
		log_error (LOG_ASSEMBLY, "Unable to seek into the file to find ECOD before APK comment: %s (ret: %d; errno: %d)", std::strerror (errno), ret, errno);
		return false;
	}

	std::vector<uint8_t> buf (alloc_size);

	nread = ::read (fd, buf.data (), static_cast<read_count_type>(buf.size ()));

	if (nread < 0 || static_cast<size_t>(nread) != alloc_size) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD and comment from the APK: %s (nread: %d; errno: %d)", std::strerror (errno), nread, errno);
		return false;
	}

	// We scan from the end to save time
	bool found = false;
	const uint8_t* data = buf.data ();
	for (ssize_t i = static_cast<ssize_t>(alloc_size - (ZIP_EOCD_LEN + 2)); i >= 0; i--) {
		if (memcmp (data + i, ZIP_EOCD_MAGIC, sizeof(ZIP_EOCD_MAGIC)) != 0)
			continue;

		found = true;
		memcpy (eocd.data (), data + i, ZIP_EOCD_LEN);
		break;
	}

	if (!found) {
		log_error (LOG_ASSEMBLY, "Unable to find EOCD in the APK (with comment)");
		return false;
	}

	return zip_extract_cd_info (eocd, cd_offset, cd_size, cd_entries);
}

bool
EmbeddedAssemblies::zip_adjust_data_offset (int fd, ZipEntryLoadState &state)
{
	static constexpr size_t LH_FILE_NAME_LENGTH_OFFSET   = 26;
	static constexpr size_t LH_EXTRA_LENGTH_OFFSET       = 28;

	off_t result = ::lseek (fd, static_cast<off_t>(state.local_header_offset), SEEK_SET);
	if (result < 0) {
		log_error (LOG_ASSEMBLY, "Failed to seek to archive entry local header at offset %u. %s (result: %d; errno: %d)", state.local_header_offset, result, errno);
		return false;
	}

	std::array<uint8_t, ZIP_LOCAL_LEN> local_header;
	std::array<uint8_t, 4> signature;

	ssize_t nread = ::read (fd, local_header.data (), local_header.size ());
	if (nread < 0 || nread != ZIP_LOCAL_LEN) {
		log_error (LOG_ASSEMBLY, "Failed to read local header at offset %u: %s (nread: %d; errno: %d)", state.local_header_offset, std::strerror (errno), nread, errno);
		return false;
	}

	size_t index = 0;
	if (!zip_read_field (local_header, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header entry signature at offset %u", state.local_header_offset);
		return false;
	}

	if (memcmp (signature.data (), ZIP_LOCAL_MAGIC, signature.size ()) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Local Header entry signature at offset %u", state.local_header_offset);
		return false;
	}

	uint16_t file_name_length;
	index = LH_FILE_NAME_LENGTH_OFFSET;
	if (!zip_read_field (local_header, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'file name length' field at offset %u", (state.local_header_offset + index));
		return false;
	}

	uint16_t extra_field_length;
	index = LH_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (local_header, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'extra field length' field at offset %u", (state.local_header_offset + index));
		return false;
	}

	state.data_offset = static_cast<uint32_t>(state.local_header_offset) + file_name_length + extra_field_length + local_header.size ();

	return true;
}

template<size_t BufSize>
bool
EmbeddedAssemblies::zip_extract_cd_info (std::array<uint8_t, BufSize> const& buf, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries)
{
	constexpr size_t EOCD_TOTAL_ENTRIES_OFFSET = 10;
	constexpr size_t EOCD_CD_SIZE_OFFSET       = 12;
	constexpr size_t EOCD_CD_START_OFFSET      = 16;

	static_assert (BufSize >= ZIP_EOCD_LEN, "Buffer too short for EOCD");

	if (!zip_read_field (buf, EOCD_TOTAL_ENTRIES_OFFSET, cd_entries)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'total number of entries' field");
		return false;
	}

	if (!zip_read_field (buf, EOCD_CD_START_OFFSET, cd_offset)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'central directory size' field");
		return false;
	}

	if (!zip_read_field (buf, EOCD_CD_SIZE_OFFSET, cd_size)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'central directory offset' field");
		return false;
	}

	return true;
}

template<class T>
force_inline bool
EmbeddedAssemblies::zip_ensure_valid_params (T const& buf, size_t index, size_t to_read) const noexcept
{
	if (index + to_read > buf.size ()) {
		log_error (LOG_ASSEMBLY, "Buffer too short to read %u bytes of data", to_read);
		return false;
	}

	return true;
}

template<ByteArrayContainer T>
bool
EmbeddedAssemblies::zip_read_field (T const& src, size_t source_index, uint16_t& dst) const noexcept
{
	if (!zip_ensure_valid_params (src, source_index, sizeof (dst))) {
		return false;
	}

	dst = static_cast<uint16_t>((src [source_index + 1] << 8) | src [source_index]);

	return true;
}

template<ByteArrayContainer T>
bool
EmbeddedAssemblies::zip_read_field (T const& src, size_t source_index, uint32_t& dst) const noexcept
{
	if (!zip_ensure_valid_params (src, source_index, sizeof (dst))) {
		return false;
	}

	dst =
		(static_cast<uint32_t> (src [source_index + 3]) << 24) |
		(static_cast<uint32_t> (src [source_index + 2]) << 16) |
		(static_cast<uint32_t> (src [source_index + 1]) << 8)  |
		(static_cast<uint32_t> (src [source_index + 0]));

	return true;
}

template<ByteArrayContainer T>
bool
EmbeddedAssemblies::zip_read_field (T const& src, size_t source_index, std::array<uint8_t, 4>& dst_sig) const noexcept
{
	if (!zip_ensure_valid_params (src, source_index, dst_sig.size ())) {
		return false;
	}

	memcpy (dst_sig.data (), src.data () + source_index, dst_sig.size ());
	return true;
}

template<ByteArrayContainer T>
bool
EmbeddedAssemblies::zip_read_field (T const& buf, size_t index, size_t count, dynamic_local_string<SENSIBLE_PATH_MAX>& characters) const noexcept
{
	if (!zip_ensure_valid_params (buf, index, count)) {
		return false;
	}

	characters.assign (reinterpret_cast<const char*>(buf.data () + index), count);
	return true;
}

bool
EmbeddedAssemblies::zip_read_entry_info (std::vector<uint8_t> const& buf, dynamic_local_string<SENSIBLE_PATH_MAX>& file_name, ZipEntryLoadState &state)
{
	constexpr size_t CD_COMPRESSION_METHOD_OFFSET = 10;
	constexpr size_t CD_UNCOMPRESSED_SIZE_OFFSET  = 24;
	constexpr size_t CD_FILENAME_LENGTH_OFFSET    = 28;
	constexpr size_t CD_EXTRA_LENGTH_OFFSET       = 30;
	constexpr size_t CD_LOCAL_HEADER_POS_OFFSET   = 42;
	constexpr size_t CD_COMMENT_LENGTH_OFFSET     = 32;

	size_t index = state.buf_offset;
	zip_ensure_valid_params (buf, index, ZIP_CENTRAL_LEN);

	std::array<uint8_t, 4> signature;
	if (!zip_read_field (buf, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry signature");
		return false;
	}

	if (memcmp (signature.data (), ZIP_CENTRAL_MAGIC, signature.size ()) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Central Directory entry signature");
		return false;
	}

	index = state.buf_offset + CD_COMPRESSION_METHOD_OFFSET;
	if (!zip_read_field (buf, index, state.compression_method)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'compression method' field");
		return false;
	}

	index = state.buf_offset + CD_UNCOMPRESSED_SIZE_OFFSET;;
	if (!zip_read_field (buf, index, state.file_size)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'uncompressed size' field");
		return false;
	}

	uint16_t file_name_length;
	index = state.buf_offset + CD_FILENAME_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name length' field");
		return false;
	}

	uint16_t extra_field_length;
	index = state.buf_offset + CD_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'extra field length' field");
		return false;
	}

	uint16_t comment_length;
	index = state.buf_offset + CD_COMMENT_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, comment_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file comment length' field");
		return false;
	}

	index = state.buf_offset + CD_LOCAL_HEADER_POS_OFFSET;
	if (!zip_read_field (buf, index, state.local_header_offset)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'relative offset of local header' field");
		return false;
	}
	index += sizeof(state.local_header_offset);

	if (file_name_length == 0) {
		file_name.clear ();
	} else if (!zip_read_field (buf, index, file_name_length, file_name)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name' field");
		return false;
	}

	state.buf_offset += ZIP_CENTRAL_LEN + file_name_length + extra_field_length + comment_length;
	return true;
}
