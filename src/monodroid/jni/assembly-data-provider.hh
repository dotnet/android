#if !defined (ASSEMBLY_DATA_PROVIDER_HH)
#define ASSEMBLY_DATA_PROVIDER_HH

#include "helpers.hh"
#include "logger.hh"
#include "xamarin-app.hh"

namespace xamarin::android::internal
{
	struct AssemblyData
	{
		const uint8_t *data;
		const uint32_t size;
	};
}
#endif // ndef ASSEMBLY_DATA_PROVIDER_HH
