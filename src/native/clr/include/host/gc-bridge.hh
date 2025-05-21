#pragma once

#include <jni.h>

#include <shared/cpp-util.hh>

struct StronglyConnectedComponent
{
	size_t Count;
	void** ContextMemory;
};

struct ComponentCrossReference
{
	size_t SourceGroupIndex;
	size_t DestinationGroupIndex;
};

using MarkCrossReferencesFtn = void (*)(size_t, StronglyConnectedComponent*, size_t, ComponentCrossReference*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		static void initialize_on_load (JNIEnv *env) noexcept;
		static void trigger_java_gc () noexcept;
		static void mark_cross_references (size_t sccsLen, StronglyConnectedComponent* sccs, size_t ccrsLen, ComponentCrossReference* ccrs) noexcept;

		static void set_finish_callback (MarkCrossReferencesFtn callback) noexcept
		{
			abort_if_invalid_pointer_argument (callback, "callback");
			bridge_processing_finish_callback = callback;
		}

	private:
		static inline jobject    Runtime_instance = nullptr;
		static inline jmethodID  Runtime_gc = nullptr;
		static inline MarkCrossReferencesFtn bridge_processing_finish_callback = nullptr;
	};
}
