#include "monovm-properties.hh"

using namespace xamarin::android::internal;

MonoVMProperties::property_array MonoVMProperties::_property_keys {
	RUNTIME_IDENTIFIER_KEY.data (),
	APP_CONTEXT_BASE_DIRECTORY_KEY.data (),
	LOCAL_DATE_TIME_OFFSET_KEY.data (),
};

MonoVMProperties::property_array MonoVMProperties::_property_values {
	SharedConstants::runtime_identifier.data (),
	nullptr,
	nullptr,
};
