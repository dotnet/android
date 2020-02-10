#include <cerrno>
#include <cctype>
#include <libgen.h>

#include <mono/metadata/assembly.h>

#include "embedded-assemblies.hh"
#include "cpp-util.hh"
#include "globals.hh"

using namespace xamarin::android::internal;

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

	// C++17 allows template parameter type inference, but alas, Apple's antiquated compiler does
	// not support this particular part of the spec...
	simple_pointer_guard<uint8_t[]>  buf (new uint8_t[cd_size]);
	const char           *prefix     = get_assemblies_prefix ();
	size_t                prefix_len = strlen (prefix);
	size_t                buf_offset = 0;
	uint16_t              compression_method;
	uint32_t              local_header_offset;
	uint32_t              data_offset;
	uint32_t              file_size;
	char                 *entry_name;
	char                 *file_name;

	ssize_t nread = read (fd, buf.get (), cd_size);
	if (static_cast<size_t>(nread) != cd_size) {
		log_fatal (LOG_ASSEMBLY, "Failed to read Central Directory from the APK archive %s. %s (nread: %d; errno: %d)", apk_name, std::strerror (errno), nread, errno);
		exit (FATAL_EXIT_NO_ASSEMBLIES);
	}

	for (size_t i = 0; i < cd_entries; i++) {
		bool result = zip_read_entry_info (buf.get (), cd_size, buf_offset, compression_method, local_header_offset, file_size, entry_name);
		simple_pointer_guard<char> entry_name_guard = entry_name;
		file_name = entry_name_guard.get ();

#ifdef DEBUG
		log_warn (LOG_ASSEMBLY, "%s entry: %s", apk_name, file_name);
#endif
		if (!result) {
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

		if (strncmp (prefix, file_name, prefix_len) != 0)
			continue;

		// assemblies must be 4-byte aligned, or Bad Things happen
		if ((data_offset & 0x3) != 0) {
			log_fatal (LOG_ASSEMBLY, "Assembly '%s' is located at bad offset %lu within the .apk\n", file_name, data_offset);
			log_fatal (LOG_ASSEMBLY, "You MUST run `zipalign` on %s\n", strrchr (apk_name, '/') + 1);
			exit (FATAL_EXIT_MISSING_ZIPALIGN);
		}

		bool entry_is_overridden = !should_register (strrchr (file_name, '/') + 1);

		if ((utils.ends_with (file_name, ".pdb") || utils.ends_with (file_name, ".mdb")) &&
				register_debug_symbols &&
				!entry_is_overridden &&
				bundled_assemblies != nullptr) {
			md_mmap_info map_info = md_mmap_apk_file(fd, data_offset, file_size, file_name, apk_name);
			if (register_debug_symbols_for_assembly (file_name, (bundled_assemblies) [bundled_assemblies_count - 1], (const mono_byte*)map_info.area, static_cast<int>(file_size)))
				continue;
		}

		if (utils.ends_with (file_name, ".config") && bundled_assemblies != nullptr) {
			char *assembly_name = strdup (basename (file_name));
			// Remove '.config' suffix
			*strrchr (assembly_name, '.') = '\0';

			md_mmap_info map_info = md_mmap_apk_file (fd, data_offset, file_size, file_name, apk_name);
			mono_register_config_for_assembly (assembly_name, (const char*)map_info.area);

			continue;
		}

		if (!utils.ends_with (file_name, ".dll"))
			continue;

		if (entry_is_overridden)
			continue;

		size_t alloc_size = MULTIPLY_WITH_OVERFLOW_CHECK (size_t, sizeof(void*), bundled_assemblies_count + 1);
		bundled_assemblies = reinterpret_cast<MonoBundledAssembly**> (utils.xrealloc (bundled_assemblies, alloc_size));
		MonoBundledAssembly *cur = bundled_assemblies [bundled_assemblies_count] = reinterpret_cast<MonoBundledAssembly*> (utils.xcalloc (1, sizeof (MonoBundledAssembly)));
		++bundled_assemblies_count;

		md_mmap_info map_info = md_mmap_apk_file (fd, data_offset, file_size, file_name, apk_name);
		cur->name = utils.monodroid_strdup_printf ("%s", strstr (file_name, prefix) + prefix_len);
		cur->data = (const unsigned char*)map_info.area;

		// MonoBundledAssembly::size is const?!
		unsigned int *psize = (unsigned int*) &cur->size;
		*psize = static_cast<unsigned int>(file_size);

		if (utils.should_log (LOG_ASSEMBLY)) {
			const char *p = (const char*) cur->data;

			char header[9];
			for (size_t j = 0; j < sizeof(header)-1; ++j)
				header[j] = isprint (p [j]) ? p [j] : '.';
			header [sizeof(header)-1] = '\0';

			log_info_nocheck (LOG_ASSEMBLY, "file-offset: % 8x  start: %08p  end: %08p  len: % 12i  zip-entry:  %s name: %s [%s]",
			                  (int) data_offset, cur->data, cur->data + *psize, (int) file_size, file_name, cur->name, header);
		}
	}
}

bool
EmbeddedAssemblies::zip_read_cd_info (int fd, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries)
{
#if defined (WINDOWS)
	using read_count_type = unsigned int;
#else
	using read_count_type = size_t;
#endif
	// The simplest case - no file comment
	off_t ret = ::lseek (fd, -ZIP_EOCD_LEN, SEEK_END);
	if (ret < 0) {
		log_error (LOG_ASSEMBLY, "Unable to seek into the APK to find ECOD: %s (ret: %d; errno: %d)", std::strerror (errno), ret, errno);
		return false;
	}

	uint8_t eocd[ZIP_EOCD_LEN];
	ssize_t nread = ::read (fd, eocd, static_cast<read_count_type>(ZIP_EOCD_LEN));
	if (nread < 0 || nread != ZIP_EOCD_LEN) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD from the APK: %s (nread: %d; errno: %d)", std::strerror (errno), nread, errno);
		return false;
	}

	size_t index = 0; // signature
	uint8_t signature[4];

	if (!zip_read_field (eocd, ZIP_EOCD_LEN, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD signature");
		return false;
	}

	if (memcmp (signature, ZIP_EOCD_MAGIC, sizeof(signature)) == 0) {
		return zip_extract_cd_info (eocd, ZIP_EOCD_LEN, cd_offset, cd_size, cd_entries);
	}

	// Most probably a ZIP with comment
	size_t alloc_size = 65535 + ZIP_EOCD_LEN; // 64k is the biggest comment size allowed
	ret = ::lseek (fd, static_cast<off_t>(-alloc_size), SEEK_END);
	if (ret < 0) {
		log_error (LOG_ASSEMBLY, "Unable to seek into the file to find ECOD before APK comment: %s (ret: %d; errno: %d)", std::strerror (errno), ret, errno);
		return false;
	}

	auto buf = new uint8_t[alloc_size];
	// The cast removes warning on mingw:
	//
	//   warning: conversion from ‘size_t’ {aka ‘long long unsigned int’} to ‘unsigned int’ may change value [-Wconversion]
	//
	nread = ::read (fd, buf, static_cast<read_count_type>(alloc_size));

	if (nread < 0 || static_cast<size_t>(nread) != alloc_size) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD and comment from the APK: %s (nread: %d; errno: %d)", std::strerror (errno), nread, errno);
		return false;
	}

	// We scan from the end to save time
	bool found = false;
	for (ssize_t i = static_cast<ssize_t>(alloc_size - (ZIP_EOCD_LEN + 2)); i >= 0; i--) {
		if (memcmp (buf + i, ZIP_EOCD_MAGIC, sizeof(ZIP_EOCD_MAGIC)) != 0)
			continue;

		found = true;
		memcpy (eocd, buf + i, ZIP_EOCD_LEN);
		break;
	}

	delete[] buf;
	if (!found) {
		log_error (LOG_ASSEMBLY, "Unable to find EOCD in the APK (with comment)");
		return false;
	}

	return zip_extract_cd_info (eocd, ZIP_EOCD_LEN, cd_offset, cd_size, cd_entries);
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

	uint8_t local_header[ZIP_LOCAL_LEN];
	uint8_t signature[4];

	ssize_t nread = ::read (fd, local_header, static_cast<size_t>(ZIP_LOCAL_LEN));
	if (nread < 0 || nread != ZIP_LOCAL_LEN) {
		log_error (LOG_ASSEMBLY, "Failed to read local header at offset %u: %s (nread: %d; errno: %d)", local_header_offset, std::strerror (errno), nread, errno);
		return false;
	}

	size_t index = 0;
	if (!zip_read_field (local_header, ZIP_LOCAL_LEN, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header entry signature at offset %u", local_header_offset);
		return false;
	}

	if (memcmp (signature, ZIP_LOCAL_MAGIC, sizeof(signature)) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Local Header entry signature at offset %u", local_header_offset);
		return false;
	}

	uint16_t file_name_length;
	index = LH_FILE_NAME_LENGTH_OFFSET;
	if (!zip_read_field (local_header, ZIP_LOCAL_LEN, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'file name length' field at offset %u", (local_header_offset + index));
		return false;
	}

	uint16_t extra_field_length;
	index = LH_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (local_header, ZIP_LOCAL_LEN, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'extra field length' field at offset %u", (local_header_offset + index));
		return false;
	}

	data_start_offset = static_cast<uint32_t>(local_header_offset) + file_name_length + extra_field_length + static_cast<uint32_t>(ZIP_LOCAL_LEN);

	return true;
}

bool
EmbeddedAssemblies::zip_extract_cd_info (uint8_t* buf, size_t buf_len, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries)
{
	static constexpr size_t EOCD_TOTAL_ENTRIES_OFFSET = 10;
	static constexpr size_t EOCD_CD_SIZE_OFFSET       = 12;
	static constexpr size_t EOCD_CD_START_OFFSET      = 16;

	if (buf_len < ZIP_EOCD_LEN) {
		log_fatal (LOG_ASSEMBLY, "Buffer too short for EOCD");
		exit (FATAL_EXIT_OUT_OF_MEMORY);
	}

	if (!zip_read_field (buf, buf_len, EOCD_TOTAL_ENTRIES_OFFSET, cd_entries)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'total number of entries' field");
		return false;
	}

	if (!zip_read_field (buf, buf_len, EOCD_CD_START_OFFSET, cd_offset)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'central directory size' field");
		return false;
	}

	if (!zip_read_field (buf, buf_len, EOCD_CD_SIZE_OFFSET, cd_size)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'central directory offset' field");
		return false;
	}

	return true;
}

bool
EmbeddedAssemblies::zip_ensure_valid_params (uint8_t* buf, size_t buf_len, size_t index, size_t to_read)
{
	if (buf == nullptr) {
		log_error (LOG_ASSEMBLY, "No buffer to read ZIP data into");
		return false;
	}

	if (index + to_read > buf_len) {
		log_error (LOG_ASSEMBLY, "Buffer too short to read %u bytes of data", to_read);
		return false;
	}

	return true;
}

bool
EmbeddedAssemblies::zip_read_field (uint8_t* buf, size_t buf_len, size_t index, uint16_t& u)
{
	if (!zip_ensure_valid_params (buf, buf_len, index, sizeof (u))) {
		return false;
	}

	u = static_cast<uint16_t>((buf [index + 1] << 8) | buf [index]);

	return true;
}

bool
EmbeddedAssemblies::zip_read_field (uint8_t* buf, size_t buf_len, size_t index, uint32_t& u)
{
	if (!zip_ensure_valid_params (buf, buf_len, index, sizeof (u))) {
		return false;
	}

	u = (static_cast<uint32_t> (buf [index + 3]) << 24) |
		(static_cast<uint32_t> (buf [index + 2]) << 16) |
		(static_cast<uint32_t> (buf [index + 1]) << 8)  |
		(static_cast<uint32_t> (buf [index + 0]));

	return true;
}

bool
EmbeddedAssemblies::zip_read_field (uint8_t* buf, size_t buf_len, size_t index, uint8_t (&sig)[4])
{
	static constexpr size_t sig_size = sizeof(sig);

	if (!zip_ensure_valid_params (buf, buf_len, index, sig_size)) {
		return false;
	}

	memcpy (sig, buf + index, sig_size);
	return true;
}

bool
EmbeddedAssemblies::zip_read_field (uint8_t* buf, size_t buf_len, size_t index, size_t count, char*& characters)
{
	if (!zip_ensure_valid_params (buf, buf_len, index, count)) {
		return false;
	}

	size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, count, 1);
	characters = new char[alloc_size];
	memcpy (characters, buf + index, count);
	characters [count] = '\0';

	return true;
}

bool
EmbeddedAssemblies::zip_read_entry_info (uint8_t* buf, size_t buf_len, size_t& buf_offset, uint16_t& compression_method, uint32_t& local_header_offset, uint32_t& file_size, char*& file_name)
{
	static constexpr size_t CD_COMPRESSION_METHOD_OFFSET = 10;
	static constexpr size_t CD_UNCOMPRESSED_SIZE_OFFSET  = 24;
	static constexpr size_t CD_FILENAME_LENGTH_OFFSET    = 28;
	static constexpr size_t CD_EXTRA_LENGTH_OFFSET       = 30;
	static constexpr size_t CD_LOCAL_HEADER_POS_OFFSET   = 42;
	static constexpr size_t CD_COMMENT_LENGTH_OFFSET     = 32;

	size_t index = buf_offset;
	zip_ensure_valid_params (buf, buf_len, index, ZIP_CENTRAL_LEN);

	uint8_t signature[4];
	if (!zip_read_field (buf, buf_len, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry signature");
		return false;
	}

	if (memcmp (signature, ZIP_CENTRAL_MAGIC, sizeof(signature)) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Central Directory entry signature");
		return false;
	}

	index = buf_offset + CD_COMPRESSION_METHOD_OFFSET;
	if (!zip_read_field (buf, buf_len, index, compression_method)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'compression method' field");
		return false;
	}

	index = buf_offset + CD_UNCOMPRESSED_SIZE_OFFSET;;
	if (!zip_read_field (buf, buf_len, index, file_size)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'uncompressed size' field");
		return false;
	}

	uint16_t file_name_length;
	index = buf_offset + CD_FILENAME_LENGTH_OFFSET;
	if (!zip_read_field (buf, buf_len, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name length' field");
		return false;
	}

	uint16_t extra_field_length;
	index = buf_offset + CD_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (buf, buf_len, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'extra field length' field");
		return false;
	}

	uint16_t comment_length;
	index = buf_offset + CD_COMMENT_LENGTH_OFFSET;
	if (!zip_read_field (buf, buf_len, index, comment_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file comment length' field");
		return false;
	}

	index = buf_offset + CD_LOCAL_HEADER_POS_OFFSET;
	if (!zip_read_field (buf, buf_len, index, local_header_offset)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'relative offset of local header' field");
		return false;
	}
	index += sizeof(local_header_offset);

	if (file_name_length == 0) {
		file_name = new char[1];
		file_name[0] = '\0';
	} else if (!zip_read_field (buf, buf_len, index, file_name_length, file_name)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name' field");
		return false;
	}

	buf_offset += ZIP_CENTRAL_LEN + file_name_length + extra_field_length + comment_length;
	return true;
}
