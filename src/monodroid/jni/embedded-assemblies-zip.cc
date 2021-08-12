#include <array>
#include <cerrno>
#include <cctype>
#include <vector>
#include <libgen.h>

#include <mono/metadata/assembly.h>

#include "embedded-assemblies.hh"
#include "cpp-util.hh"
#include "globals.hh"

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

void
EmbeddedAssemblies::zip_load_entries (int fd, const char *apk_name, monodroid_should_register should_register)
{
	uint32_t cd_offset;
	uint32_t cd_size;
	uint16_t cd_entries;

	if (!zip_read_cd_info (fd, cd_offset, cd_size, cd_entries)) {
		log_fatal (LOG_ASSEMBLY,  "Failed to read the EOCD record from APK file %s", apk_name);
		exit (FATAL_EXIT_NO_ASSEMBLIES);
	}
#ifdef DEBUG
	log_info (LOG_ASSEMBLY, "Central directory offset: %u", cd_offset);
	log_info (LOG_ASSEMBLY, "Central directory size: %u", cd_size);
	log_info (LOG_ASSEMBLY, "Central directory entries: %u", cd_entries);
#endif
	off_t retval = ::lseek (fd, static_cast<off_t>(cd_offset), SEEK_SET);
	if (retval < 0) {
		log_fatal (LOG_ASSEMBLY, "Failed to seek to central directory position in the APK file %s. %s (result: %d; errno: %d)", apk_name, std::strerror (errno), retval, errno);
		exit (FATAL_EXIT_NO_ASSEMBLIES);
	}

	std::vector<uint8_t>  buf (cd_size);
	const char           *prefix     = get_assemblies_prefix ();
	size_t                prefix_len = get_assemblies_prefix_length ();
	size_t                buf_offset = 0;
	uint16_t              compression_method;
	uint32_t              local_header_offset;
	uint32_t              data_offset;
	uint32_t              file_size;

	ssize_t nread = read (fd, buf.data (), static_cast<read_count_type>(buf.size ()));
	if (static_cast<size_t>(nread) != cd_size) {
		log_fatal (LOG_ASSEMBLY, "Failed to read Central Directory from the APK archive %s. %s (nread: %d; errno: %d)", apk_name, std::strerror (errno), nread, errno);
		exit (FATAL_EXIT_NO_ASSEMBLIES);
	}

	dynamic_local_string<SENSIBLE_PATH_MAX> entry_name;
#if defined (NET6)
	bool runtime_config_blob_found = false;
#endif // def NET6

	bool bundled_assemblies_slow_path = false;
	if (bundled_assemblies.empty ()) {
		// We're called for the first time, let's allocate space for all the assemblies counted during build. resize()
		// will allocate all the memory and zero it in one operation
		bundled_assemblies.resize (application_config.number_of_assemblies_in_apk);
	} else {
		// We are probably registering some assemblies dynamically, possibly loading from another apk, we need to extend
		// the storage instead of taking advantage of the pre-allocated slots.
		bundled_assemblies_slow_path = true;
	}

	int64_t assembly_count = application_config.number_of_assemblies_in_apk;

	// clang-tidy claims we have a leak in the loop:
	//
	//   Potential leak of memory pointed to by 'assembly_name'
	//
	// This is because we allocate `assembly_name` for a `.config` file, pass it to Mono and we don't free the value.
	// However, clang-tidy can't know that the value is owned by Mono and we must not free it, thus the suppression.
	//
	// NOLINTNEXTLINE(clang-analyzer-unix.Malloc)
	for (size_t i = 0; i < cd_entries; i++) {
		entry_name.clear ();

		bool result = zip_read_entry_info (buf, buf_offset, compression_method, local_header_offset, file_size, entry_name);

#ifdef DEBUG
		log_warn (LOG_ASSEMBLY, "%s entry: %s", apk_name, entry_name.get () == nullptr ? "unknown" : entry_name.get ());
#endif
		if (!result || entry_name.empty ()) {
			log_fatal (LOG_ASSEMBLY, "Failed to read Central Directory info for entry %u in APK file %s", i, apk_name);
			exit (FATAL_EXIT_NO_ASSEMBLIES);
		}

		if (!zip_adjust_data_offset (fd, local_header_offset, data_offset)) {
			log_fatal (LOG_ASSEMBLY, "Failed to adjust data start offset for entry %u in APK file %s", i, apk_name);
			exit (FATAL_EXIT_NO_ASSEMBLIES);
		}
#ifdef DEBUG
		log_warn (LOG_ASSEMBLY, "    ZIP: local header offset: %u; data offset: %u; file size: %u", local_header_offset, data_offset, file_size);
#endif
		if (compression_method != 0)
			continue;

		if (strncmp (prefix, entry_name.get (), prefix_len) != 0)
			continue;

#if defined (NET6)
		if (application_config.have_runtime_config_blob && !runtime_config_blob_found) {
			if (utils.ends_with (entry_name, SharedConstants::RUNTIME_CONFIG_BLOB_NAME)) {
				runtime_config_blob_found = true;
				runtime_config_blob_mmap = md_mmap_apk_file (fd, data_offset, file_size, entry_name.get (), apk_name);
				continue;
			}
		}
#endif // def NET6

		// assemblies must be 4-byte aligned, or Bad Things happen
		if ((data_offset & 0x3) != 0) {
			log_fatal (LOG_ASSEMBLY, "Assembly '%s' is located at bad offset %lu within the .apk\n", entry_name.get (), data_offset);
			log_fatal (LOG_ASSEMBLY, "You MUST run `zipalign` on %s\n", strrchr (apk_name, '/') + 1);
			exit (FATAL_EXIT_MISSING_ZIPALIGN);
		}

		const char *last_slash = utils.find_last (entry_name, '/');
		bool entry_is_overridden = last_slash == nullptr ? false : !should_register (last_slash + 1);

		if ((utils.ends_with (entry_name, ".pdb") || utils.ends_with (entry_name, ".mdb")) &&
				register_debug_symbols &&
				!entry_is_overridden &&
				bundled_assembly_index >= 1) {
			md_mmap_info map_info = md_mmap_apk_file(fd, data_offset, file_size, entry_name.get (), apk_name);
			if (register_debug_symbols_for_assembly (entry_name, bundled_assemblies [bundled_assembly_index - 1], (const mono_byte*)map_info.area, static_cast<int>(file_size)))
				continue;
		}

#if !defined(NET6)
		if (utils.ends_with (entry_name, ".config") && !bundled_assemblies.empty ()) {
			char *assembly_name = strdup (basename (entry_name.get ()));
			// Remove '.config' suffix
			*strrchr (assembly_name, '.') = '\0';

			md_mmap_info map_info = md_mmap_apk_file (fd, data_offset, file_size, entry_name.get (), apk_name);
			mono_register_config_for_assembly (assembly_name, (const char*)map_info.area);

			continue;
		}
#endif // ndef NET6

		if (!utils.ends_with (entry_name, ".dll"))
			continue;

		if (entry_is_overridden)
			continue;

		assembly_count--;
		MonoBundledAssembly *cur;
		if (XA_UNLIKELY (assembly_count < 0 || (bundled_assemblies_slow_path && bundled_assemblies.size () <= bundled_assembly_index))) {
			if (assembly_count == -1) {
				log_warn (LOG_ASSEMBLY, "Number of assemblies stored at build time (%u) was incorrect, switching to slow bundling path.");
			}
			bundled_assemblies.emplace_back ();
			cur = &bundled_assemblies.back ();
		} else {
			cur = &bundled_assemblies [bundled_assembly_index];
		}
		bundled_assembly_index++;

		md_mmap_info map_info = md_mmap_apk_file (fd, data_offset, file_size, entry_name.get (), apk_name);
		cur->name = utils.strdup_new (entry_name.get () + prefix_len, entry_name.length () - prefix_len);
		cur->data = (const unsigned char*)map_info.area;

		// MonoBundledAssembly::size is const?!
		unsigned int *psize = (unsigned int*) &cur->size;
		*psize = static_cast<unsigned int>(file_size);

		if (XA_UNLIKELY (utils.should_log (LOG_ASSEMBLY))) {
			const char *p = (const char*) cur->data;

			std::array<char, 9> header;
			for (size_t j = 0; j < header.size () - 1; ++j)
				header[j] = isprint (p [j]) ? p [j] : '.';
			header [header.size () - 1] = '\0';

			log_info_nocheck (LOG_ASSEMBLY, "file-offset: % 8x  start: %08p  end: %08p  len: % 12i  zip-entry:  %s name: %s [%s]",
			                  (int) data_offset, cur->data, cur->data + *psize, (int) file_size, entry_name.get (), cur->name, header.data ());
		}
	}
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
	if (nread < 0 || nread != ZIP_EOCD_LEN) {
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
EmbeddedAssemblies::zip_adjust_data_offset (int fd, size_t local_header_offset, uint32_t &data_start_offset)
{
	static constexpr size_t LH_FILE_NAME_LENGTH_OFFSET   = 26;
	static constexpr size_t LH_EXTRA_LENGTH_OFFSET       = 28;

	off_t result = ::lseek (fd, static_cast<off_t>(local_header_offset), SEEK_SET);
	if (result < 0) {
		log_error (LOG_ASSEMBLY, "Failed to seek to archive entry local header at offset %u. %s (result: %d; errno: %d)", local_header_offset, result, errno);
		return false;
	}

	std::array<uint8_t, ZIP_LOCAL_LEN> local_header;
	std::array<uint8_t, 4> signature;

	ssize_t nread = ::read (fd, local_header.data (), static_cast<size_t>(ZIP_LOCAL_LEN));
	if (nread < 0 || nread != ZIP_LOCAL_LEN) {
		log_error (LOG_ASSEMBLY, "Failed to read local header at offset %u: %s (nread: %d; errno: %d)", local_header_offset, std::strerror (errno), nread, errno);
		return false;
	}

	size_t index = 0;
	if (!zip_read_field (local_header, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header entry signature at offset %u", local_header_offset);
		return false;
	}

	if (memcmp (signature.data (), ZIP_LOCAL_MAGIC, sizeof(signature)) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Local Header entry signature at offset %u", local_header_offset);
		return false;
	}

	uint16_t file_name_length;
	index = LH_FILE_NAME_LENGTH_OFFSET;
	if (!zip_read_field (local_header, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'file name length' field at offset %u", (local_header_offset + index));
		return false;
	}

	uint16_t extra_field_length;
	index = LH_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (local_header, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'extra field length' field at offset %u", (local_header_offset + index));
		return false;
	}

	data_start_offset = static_cast<uint32_t>(local_header_offset) + file_name_length + extra_field_length + static_cast<uint32_t>(ZIP_LOCAL_LEN);

	return true;
}

template<size_t BufSize>
bool
EmbeddedAssemblies::zip_extract_cd_info (std::array<uint8_t, BufSize> const& buf, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries)
{
	static constexpr size_t EOCD_TOTAL_ENTRIES_OFFSET = 10;
	static constexpr size_t EOCD_CD_SIZE_OFFSET       = 12;
	static constexpr size_t EOCD_CD_START_OFFSET      = 16;

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
EmbeddedAssemblies::zip_read_entry_info (std::vector<uint8_t> const& buf, size_t& buf_offset, uint16_t& compression_method, uint32_t& local_header_offset, uint32_t& file_size, dynamic_local_string<SENSIBLE_PATH_MAX>& file_name)
{
	static constexpr size_t CD_COMPRESSION_METHOD_OFFSET = 10;
	static constexpr size_t CD_UNCOMPRESSED_SIZE_OFFSET  = 24;
	static constexpr size_t CD_FILENAME_LENGTH_OFFSET    = 28;
	static constexpr size_t CD_EXTRA_LENGTH_OFFSET       = 30;
	static constexpr size_t CD_LOCAL_HEADER_POS_OFFSET   = 42;
	static constexpr size_t CD_COMMENT_LENGTH_OFFSET     = 32;

	size_t index = buf_offset;
	zip_ensure_valid_params (buf, index, ZIP_CENTRAL_LEN);

	std::array<uint8_t, 4> signature;
	if (!zip_read_field (buf, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry signature");
		return false;
	}

	if (memcmp (signature.data (), ZIP_CENTRAL_MAGIC, sizeof(signature)) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Central Directory entry signature");
		return false;
	}

	index = buf_offset + CD_COMPRESSION_METHOD_OFFSET;
	if (!zip_read_field (buf, index, compression_method)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'compression method' field");
		return false;
	}

	index = buf_offset + CD_UNCOMPRESSED_SIZE_OFFSET;;
	if (!zip_read_field (buf, index, file_size)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'uncompressed size' field");
		return false;
	}

	uint16_t file_name_length;
	index = buf_offset + CD_FILENAME_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name length' field");
		return false;
	}

	uint16_t extra_field_length;
	index = buf_offset + CD_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'extra field length' field");
		return false;
	}

	uint16_t comment_length;
	index = buf_offset + CD_COMMENT_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, comment_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file comment length' field");
		return false;
	}

	index = buf_offset + CD_LOCAL_HEADER_POS_OFFSET;
	if (!zip_read_field (buf, index, local_header_offset)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'relative offset of local header' field");
		return false;
	}
	index += sizeof(local_header_offset);

	if (file_name_length == 0) {
		file_name.clear ();
	} else if (!zip_read_field (buf, index, file_name_length, file_name)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name' field");
		return false;
	}

	buf_offset += ZIP_CENTRAL_LEN + file_name_length + extra_field_length + comment_length;
	return true;
}

template<size_t BufferSize>
bool
EmbeddedAssemblies::register_debug_symbols_for_assembly (dynamic_local_string<BufferSize> const& entry_name, MonoBundledAssembly const& assembly, const mono_byte *debug_contents, int debug_size)
{
	const char *entry_basename = utils.find_last (entry_name, '/') + 1; // strrchr (entry_name, '/') + 1;
	// System.dll, System.dll.mdb case
	if (strncmp (assembly.name, entry_basename, strlen (assembly.name)) != 0) {
		// That failed; try for System.dll, System.pdb case
		const char *eb_ext = utils.find_last (entry_name, '.');
		if (eb_ext == nullptr)
			return false;
		off_t basename_len    = static_cast<off_t>(eb_ext - entry_basename);
		abort_unless (basename_len > 0, "basename must have a length!");
		if (strncmp (assembly.name, entry_basename, static_cast<size_t>(basename_len)) != 0)
			return false;
	}

	mono_register_symfile_for_assembly (assembly.name, debug_contents, debug_size);

	return true;
}
