#include <fcntl.h>

#include <cerrno>
#include <format>
#include <tuple>
#include <vector>

#include <runtime-base/logger.hh>
#include <shared/helpers.hh>
#include <startup/zip.hh>
#include <xamarin-app.hh>

using namespace xamarin::android;

[[gnu::always_inline]]
std::tuple<const char*, uint32_t> Zip::get_assemblies_prefix_and_length () noexcept
{
	return {lib_prefix.data (), lib_prefix.size () };
}

[[gnu::always_inline]]
bool Zip::zip_adjust_data_offset (int fd, zip_scan_state &state) noexcept
{
	static constexpr size_t LH_FILE_NAME_LENGTH_OFFSET	 = 26uz;
	static constexpr size_t LH_EXTRA_LENGTH_OFFSET		 = 28uz;

	off_t result = ::lseek (fd, static_cast<off_t>(state.local_header_offset), SEEK_SET);
	if (result < 0) {
		log_error (
			LOG_ASSEMBLY,
			"Failed to seek to archive entry local header at offset {}. {} (result: {}; errno: {})",
			state.local_header_offset, std::strerror (errno), result, errno
		);
		return false;
	}

	std::array<uint8_t, ZIP_LOCAL_LEN> local_header;
	std::array<uint8_t, 4> signature;

	ssize_t nread = ::read (fd, local_header.data (), local_header.size ());
	if (nread < 0 || nread != ZIP_LOCAL_LEN) {
		log_error (LOG_ASSEMBLY, "Failed to read local header at offset {}: {} (nread: {}; errno: {})", state.local_header_offset, std::strerror (errno), nread, errno);
		return false;
	}

	size_t index = 0;
	if (!zip_read_field (local_header, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header entry signature at offset {}", state.local_header_offset);
		return false;
	}

	if (memcmp (signature.data (), ZIP_LOCAL_MAGIC.data (), signature.size ()) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Local Header entry signature at offset {}", state.local_header_offset);
		return false;
	}

	uint16_t file_name_length;
	index = LH_FILE_NAME_LENGTH_OFFSET;
	if (!zip_read_field (local_header, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'file name length' field at offset {}", (state.local_header_offset + index));
		return false;
	}

	uint16_t extra_field_length;
	index = LH_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (local_header, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Local Header 'extra field length' field at offset {}", (state.local_header_offset + index));
		return false;
	}

	state.data_offset = static_cast<uint32_t>(state.local_header_offset) + file_name_length + extra_field_length + local_header.size ();

	return true;
}

[[gnu::always_inline]]
bool Zip::zip_read_entry_info (std::vector<uint8_t> const& buf, dynamic_local_string<SENSIBLE_PATH_MAX>& file_name, zip_scan_state &state) noexcept
{
	constexpr size_t CD_COMPRESSION_METHOD_OFFSET = 10uz;
	constexpr size_t CD_UNCOMPRESSED_SIZE_OFFSET  = 24uz;
	constexpr size_t CD_FILENAME_LENGTH_OFFSET	  = 28uz;
	constexpr size_t CD_EXTRA_LENGTH_OFFSET		  = 30uz;
	constexpr size_t CD_LOCAL_HEADER_POS_OFFSET	  = 42uz;
	constexpr size_t CD_COMMENT_LENGTH_OFFSET	  = 32uz;

	size_t index = state.buf_offset;
	zip_ensure_valid_params (buf, index, ZIP_CENTRAL_LEN);

	std::array<uint8_t, 4> signature;
	if (!zip_read_field (buf, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry signature"sv);
		return false;
	}

	if (memcmp (signature.data (), ZIP_CENTRAL_MAGIC.data (), signature.size ()) != 0) {
		log_error (LOG_ASSEMBLY, "Invalid Central Directory entry signature"sv);
		return false;
	}

	index = state.buf_offset + CD_COMPRESSION_METHOD_OFFSET;
	if (!zip_read_field (buf, index, state.compression_method)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'compression method' field"sv);
		return false;
	}

	index = state.buf_offset + CD_UNCOMPRESSED_SIZE_OFFSET;;
	if (!zip_read_field (buf, index, state.file_size)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'uncompressed size' field"sv);
		return false;
	}

	uint16_t file_name_length;
	index = state.buf_offset + CD_FILENAME_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, file_name_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name length' field"sv);
		return false;
	}

	uint16_t extra_field_length;
	index = state.buf_offset + CD_EXTRA_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, extra_field_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'extra field length' field"sv);
		return false;
	}

	uint16_t comment_length;
	index = state.buf_offset + CD_COMMENT_LENGTH_OFFSET;
	if (!zip_read_field (buf, index, comment_length)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file comment length' field"sv);
		return false;
	}

	index = state.buf_offset + CD_LOCAL_HEADER_POS_OFFSET;
	if (!zip_read_field (buf, index, state.local_header_offset)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'relative offset of local header' field"sv);
		return false;
	}
	index += sizeof(state.local_header_offset);

	if (file_name_length == 0) {
		file_name.clear ();
	} else if (!zip_read_field (buf, index, file_name_length, file_name)) {
		log_error (LOG_ASSEMBLY, "Failed to read Central Directory entry 'file name' field"sv);
		return false;
	}

	state.buf_offset += ZIP_CENTRAL_LEN + file_name_length + extra_field_length + comment_length;
	return true;
}

bool Zip::zip_load_entry_common (size_t entry_index, std::vector<uint8_t> const& buf, dynamic_local_string<SENSIBLE_PATH_MAX> &entry_name, zip_scan_state &state) noexcept
{
	entry_name.clear ();

	bool result = zip_read_entry_info (buf, entry_name, state);

	log_debug (LOG_ASSEMBLY, "{} entry: {}", state.file_name, optional_string (entry_name.get (), "unknown"));
	if (!result || entry_name.empty ()) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Failed to read Central Directory info for entry {} in APK {}",
				entry_index,
				state.file_name
			)
		);
	}

	if (!zip_adjust_data_offset (state.file_fd, state)) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Failed to adjust data start offset for entry {} in APK {}",
				entry_index,
				state.file_name
			)
		);
	}

	log_debug (LOG_ASSEMBLY, "	  ZIP: local header offset: {}; data offset: {}; file size: {}", state.local_header_offset, state.data_offset, state.file_size);
	if (state.compression_method != 0) {
		return false;
	}

	if (entry_name.get ()[0] != state.prefix[0] || entry_name.length () < state.prefix_len || memcmp (state.prefix, entry_name.get (), state.prefix_len) != 0) {
		// state.prefix and lib_prefix can point to the same location, see get_assemblies_prefix_and_length()
		// In such instance we short-circuit and avoid a couple of comparisons below.
		if (state.prefix == lib_prefix.data ()) {
			return false;
		}

		if (entry_name.get ()[0] != lib_prefix[0] || memcmp (lib_prefix.data (), entry_name.get (), lib_prefix.size () - 1) != 0) {
			return false;
		}
	}

	// assemblies must be 16-byte or 4-byte aligned, or Bad Things happen
	if (((state.data_offset & 0xf) != 0) || ((state.data_offset & 0x3) != 0)) {
		std::string_view::size_type pos = state.file_name.find_last_of ('/');
		if (pos == state.file_name.npos) {
			pos = 0;
		} else {
			pos++;
		}
		std::string_view const& name_no_path = state.file_name.substr (pos);

		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Assembly '{}' is at bad offset {} in the APK (not aligned to 4 or 16 bytes). 'zipalign' MUST be used on {} to align it properly",
				optional_string (entry_name.get ()),
				state.data_offset,
				name_no_path
			)
		);
	}

	return true;
}

template<size_t BufSize> [[gnu::always_inline]]
bool Zip::zip_extract_cd_info (std::array<uint8_t, BufSize> const& buf, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries) noexcept
{
	constexpr size_t EOCD_TOTAL_ENTRIES_OFFSET = 10uz;
	constexpr size_t EOCD_CD_SIZE_OFFSET	   = 12uz;
	constexpr size_t EOCD_CD_START_OFFSET	   = 16uz;

	static_assert (BufSize >= ZIP_EOCD_LEN, "Buffer too short for EOCD");

	if (!zip_read_field (buf, EOCD_TOTAL_ENTRIES_OFFSET, cd_entries)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'total number of entries' field"sv);
		return false;
	}

	if (!zip_read_field (buf, EOCD_CD_START_OFFSET, cd_offset)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'central directory size' field"sv);
		return false;
	}

	if (!zip_read_field (buf, EOCD_CD_SIZE_OFFSET, cd_size)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD 'central directory offset' field"sv);
		return false;
	}

	return true;
}

template<class T> [[gnu::always_inline]]
bool Zip::zip_ensure_valid_params (T const& buf, size_t index, size_t to_read) noexcept
{
	if (index + to_read > buf.size ()) {
		log_error (LOG_ASSEMBLY, "Buffer too short to read {} bytes of data", to_read);
		return false;
	}

	return true;
}

template<detail::ByteArrayContainer T> [[gnu::always_inline]]
bool Zip::zip_read_field (T const& src, size_t source_index, uint16_t& dst) noexcept
{
	if (!zip_ensure_valid_params (src, source_index, sizeof (dst))) {
		return false;
	}

	dst = static_cast<uint16_t>((src [source_index + 1] << 8) | src [source_index]);
	return true;
}

template<detail::ByteArrayContainer T> [[gnu::always_inline]]
bool Zip::zip_read_field (T const& src, size_t source_index, uint32_t& dst) noexcept
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

template<detail::ByteArrayContainer T> [[gnu::always_inline]]
bool Zip::zip_read_field (T const& src, size_t source_index, std::array<uint8_t, 4>& dst_sig) noexcept
{
	if (!zip_ensure_valid_params (src, source_index, dst_sig.size ())) {
		return false;
	}

	memcpy (dst_sig.data (), src.data () + source_index, dst_sig.size ());
	return true;
}

template<detail::ByteArrayContainer T> [[gnu::always_inline]]
bool Zip::zip_read_field (T const& buf, size_t index, size_t count, dynamic_local_string<SENSIBLE_PATH_MAX>& characters) noexcept
{
	if (!zip_ensure_valid_params (buf, index, count)) {
		return false;
	}

	characters.assign (reinterpret_cast<const char*>(buf.data () + index), count);
	return true;
}

[[gnu::always_inline]]
bool Zip::zip_read_cd_info (int apk_fd, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries) noexcept
{
	// The simplest case - no file comment
	off_t ret = ::lseek (apk_fd, -ZIP_EOCD_LEN, SEEK_END);
	if (ret < 0) {
		log_error (LOG_ASSEMBLY, "Unable to seek into the APK to find ECOD: {} (ret: {}; errno: {})", std::strerror (errno), ret, errno);
		return false;
	}

	std::array<uint8_t, ZIP_EOCD_LEN> eocd;
	ssize_t nread = ::read (apk_fd, eocd.data (), eocd.size ());
	if (nread < 0 || nread != eocd.size ()) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD from the APK: {} (nread: {}; errno: {})", std::strerror (errno), nread, errno);
		return false;
	}

	size_t index = 0uz; // signature
	std::array<uint8_t, 4uz> signature;

	if (!zip_read_field (eocd, index, signature)) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD signature"sv);
		return false;
	}

	if (memcmp (signature.data (), ZIP_EOCD_MAGIC.data (), signature.size ()) == 0) {
		return zip_extract_cd_info (eocd, cd_offset, cd_size, cd_entries);
	}

	// Most probably a ZIP with comment
	constexpr size_t alloc_size = 65535uz + ZIP_EOCD_LEN; // 64k is the biggest comment size allowed
	ret = ::lseek (apk_fd, static_cast<off_t>(-alloc_size), SEEK_END);
	if (ret < 0) {
		log_error (LOG_ASSEMBLY, "Unable to seek into the file to find ECOD before APK comment: {} (ret: {}; errno: {})", std::strerror (errno), ret, errno);
		return false;
	}

	std::vector<uint8_t> buf (alloc_size);

	nread = ::read (apk_fd, buf.data (), buf.size ());

	if (nread < 0 || static_cast<size_t>(nread) != alloc_size) {
		log_error (LOG_ASSEMBLY, "Failed to read EOCD and comment from the APK: {} (nread: {}; errno: {})", std::strerror (errno), nread, errno);
		return false;
	}

	// We scan from the end to save time
	bool found = false;
	const uint8_t* data = buf.data ();
	for (ssize_t i = static_cast<ssize_t>(alloc_size - (ZIP_EOCD_LEN + 2)); i >= 0z; i--) {
		if (memcmp (data + i, ZIP_EOCD_MAGIC.data (), sizeof(ZIP_EOCD_MAGIC)) != 0)
			continue;

		found = true;
		memcpy (eocd.data (), data + i, ZIP_EOCD_LEN);
		break;
	}

	if (!found) {
		log_error (LOG_ASSEMBLY, "Unable to find EOCD in the APK (with comment)"sv);
		return false;
	}

	return zip_extract_cd_info (eocd, cd_offset, cd_size, cd_entries);
}

[[gnu::always_inline]]
bool Zip::zip_scan_entries (int apk_fd, std::string_view const& apk_path, ScanCallbackFn entry_cb) noexcept
{
	uint32_t cd_offset;
	uint32_t cd_size;
	uint16_t cd_entries;

	if (!zip_read_cd_info (apk_fd, cd_offset, cd_size, cd_entries)) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Failed to read the EOCD record from APK file %s",
				apk_path
			)
		);
	}

	log_debug (LOG_ASSEMBLY, "Central directory offset: {}", cd_offset);
	log_debug (LOG_ASSEMBLY, "Central directory size: {}", cd_size);
	log_debug (LOG_ASSEMBLY, "Central directory entries: {}", cd_entries);

	off_t retval = ::lseek (apk_fd, static_cast<off_t>(cd_offset), SEEK_SET);
	if (retval < 0) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Failed to seek to central directory position in APK: {}. retval={} errno={}, File={}",
				std::strerror (errno),
				retval,
				errno,
				apk_path
			)
		);
	}

	std::vector<uint8_t>  buf (cd_size);
	const auto [prefix, prefix_len] = get_assemblies_prefix_and_length ();
	zip_scan_state state {
		.file_fd			 = apk_fd,
		.file_name			 = apk_path,
		.prefix				 = prefix,
		.prefix_len			 = prefix_len,
		.buf_offset			 = 0uz,
		.compression_method	 = 0u,
		.local_header_offset = 0u,
		.data_offset		 = 0u,
		.file_size			 = 0u,
		.bundled_assemblies_slow_path = false,
		.max_assembly_name_size = 0u,
		.max_assembly_file_name_size = 0u,
	};

	ssize_t nread;
	do {
		nread = read (apk_fd, buf.data (), buf.size ());
	} while (nread < 0 && errno == EINTR);

	if (static_cast<size_t>(nread) != cd_size) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Failed to read Central Directory from APK: {}. nread={} errno={} File={}",
				std::strerror (errno),
				nread,
				errno,
				apk_path
			)
		);
	}

	dynamic_local_string<SENSIBLE_PATH_MAX> entry_name;
	bool keep_archive_open = false;

	for (size_t i = 0uz; i < cd_entries; i++) {
		bool interesting_entry = zip_load_entry_common (i, buf, entry_name, state);
		if (!interesting_entry) {
			continue;
		}

		keep_archive_open |= entry_cb (apk_path, apk_fd, entry_name, state.data_offset, state.file_size);
	}

	return keep_archive_open;
}

void Zip::scan_archive (std::string_view const& apk_path, ScanCallbackFn entry_cb) noexcept
{
	int fd;
	do {
		fd = open (apk_path.data (), O_RDONLY);
	} while (fd < 0 && errno == EINTR);

	if (fd < 0) {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"ERROR: Unable to load application package {}. {}",
				apk_path, strerror (errno)
			)
		);
	}
	log_debug (LOG_ASSEMBLY, "APK {} FD: {}", apk_path, fd);
	if (!zip_scan_entries (fd, apk_path, entry_cb)) {
		return;
	}

	if (close (fd) < 0) {
		log_warn (
			LOG_ASSEMBLY,
			"Failed to close file descriptor for {}. {}",
			apk_path,
			strerror (errno)
		);
	}
}
