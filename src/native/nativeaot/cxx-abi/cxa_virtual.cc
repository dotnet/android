#include <shared/helpers.hh>

namespace __cxxabiv1 {
	extern "C" {
		[[noreturn]]
		void __cxa_pure_virtual(void) {
			xamarin::android::Helpers::abort_application ("Pure virtual function called!");
		}
	}
}
