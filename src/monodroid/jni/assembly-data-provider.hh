#if !defined (ASSEMBLY_DATA_PROVIDER_HH)
#define ASSEMBLY_DATA_PROVIDER_HH

#include "helpers.hh"
#include "logger.hh"
#include "xamarin-app.hh"

namespace xamarin::android::internal
{
	class AssemblyDataProvider
	{
	public:
		virtual auto get_data (AssemblyEntry const& entry, bool fast_path) noexcept -> uint8_t* = 0;
	};

#if defined (RELEASE)
	namespace detail
	{
		class SO_AssemblyDataProvider
		{
		public:
			SO_AssemblyDataProvider () = delete;
			~SO_AssemblyDataProvider () = delete;

			static auto get_data_fastpath (AssemblyEntry const& entry) noexcept -> uint8_t*
			{
				log_debug (LOG_ASSEMBLY, "Loading assembly from libxamarin-app.so, fast path");
				if (entry.uncompressed_data_size == 0) {
					log_debug (LOG_ASSEMBLY, "Assembly is not compressed; input offset: %u; data size: %u", entry.input_data_offset, entry.input_data_size);
				} else {
					log_debug (LOG_ASSEMBLY, "Assembly is compressed; input offset: %u; compressed data size: %u; uncompressed data size: %u; input offset: %u; output offset: %u",
					           entry.input_data_offset, entry.input_data_size, entry.uncompressed_data_size, entry.input_data_offset, entry.uncompressed_data_offset);
				}

				return nullptr;
			}
		};
	}

	class SO_APK_AssemblyDataProvider final : public AssemblyDataProvider
	{
	public:
		auto get_data (AssemblyEntry const& entry, bool fast_path) noexcept -> uint8_t* override final
		{
			log_debug (LOG_ASSEMBLY, __PRETTY_FUNCTION__);
			if (fast_path) {
				return detail::SO_AssemblyDataProvider::get_data_fastpath (entry);
			}
			log_debug (LOG_ASSEMBLY, "Looking for assembly in the APK");

			return nullptr;
		}
	};

	class SO_FILESYSTEM_AssemblyDataProvider : public AssemblyDataProvider
	{
	public:
		auto get_data (AssemblyEntry const& entry, bool fast_path) noexcept -> uint8_t* override final
		{
			log_debug (LOG_ASSEMBLY, __PRETTY_FUNCTION__);
			log_debug (LOG_ASSEMBLY, "Loading assembly from a standalone DSO");
			log_debug (LOG_ASSEMBLY, "Looking for assembly on the filesystem");

			return nullptr;
		}
	};
#else // def RELEASE
	class DLL_APK_AssemblyDataProvider : public AssemblyDataProvider
	{
	public:
		auto get_data (AssemblyEntry const& entry, bool fast_path) noexcept -> uint8_t* override final
		{
			log_fatal (LOG_ASSEMBLY, "DSO assembly loading not supported in Debug mode");
			Helpers::abort_application ();
		}
	};
#endif // ndef RELEASE
}
#endif // ndef ASSEMBLY_DATA_PROVIDER_HH
