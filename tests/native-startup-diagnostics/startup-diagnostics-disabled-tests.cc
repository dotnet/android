#include "startup-diagnostics.hh"

#include <cerrno>

using namespace xamarin::android::startup;

int main ()
{
	errno = EDOM;
	const char* environment[] = {"DOTNET_ANDROID_STARTUP_CAPTURE_ID", "0123456789abcdef0123456789abcdef"};
	Diagnostics::initialize (environment, 2);
	Diagnostics::boundary (Event::JniInit, Phase::Begin);
	Diagnostics::config (ConfigResult::Parsed);
	Diagnostics::expiry (1, 2, ExpiryDecision::Enabled);
	Diagnostics::runtime_config (false, Phase::Instant);
	Diagnostics::health ();
	return errno == EDOM ? 0 : 1;
}
