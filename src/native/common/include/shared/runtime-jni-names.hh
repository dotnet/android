#pragma once

#include <string_view>

namespace xamarin::android
{
	class RuntimeJniNames
	{
	public:
		static inline constexpr std::string_view RuntimeClass { "mono/android/Runtime" };
		static inline constexpr std::string_view IGCUserPeerClass { "mono/android/IGCUserPeer" };
		static inline constexpr std::string_view GCUserPeerableClass { "net/dot/jni/GCUserPeerable" };

		static inline constexpr std::string_view GCUserPeerRuntimeField { "mono_android_GCUserPeer" };
		static inline constexpr std::string_view IGCUserPeerRuntimeField { "mono_android_IGCUserPeer" };
		static inline constexpr std::string_view GCUserPeerableRuntimeField { "net_dot_jni_GCUserPeerable" };

		static inline constexpr std::string_view IGCUserPeerAddReferenceMethod { "monodroidAddReference" };
		static inline constexpr std::string_view IGCUserPeerClearReferencesMethod { "monodroidClearReferences" };
	};
}
