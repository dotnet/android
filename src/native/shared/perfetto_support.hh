#if !defined(PERFETTO_SUPPORT_HH)
#define PERFETTO_SUPPORT_HH

#if defined(PERFETTO_ENABLED)
#include <string_view>

#include <mono/jit/jit.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/class.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/mono-config.h>
#include <mono/metadata/mono-debug.h>
#include <mono/metadata/object.h>
#include <mono/utils/mono-dl-fallback.h>
#include <mono/utils/mono-logger.h>

#include <perfetto.h>

#include "cpp-util.hh"

namespace xamarin::android {
	class PerfettoConstants
	{
	public:
		static constexpr std::string_view ManagedRuntimeCategory      { "managed-runtime" };

		static constexpr std::string_view AssemblyLoadAnnotation      { "Assembly load" };
		static constexpr std::string_view ImageLoadAnnotation         { "Assembly image load" };
		static constexpr std::string_view ClassLoadAnnotation         { "Class load" };
		static constexpr std::string_view VTableLoadAnnotation        { "VTable load" };
		static constexpr std::string_view MethodInvokeAnnotation      { "Method invoke" };
		static constexpr std::string_view MethodRunTimeAnnotation     { "Method inner run time" };
		static constexpr std::string_view MonitorContentionAnnotation { "Monitor contention" };
	};
}

PERFETTO_DEFINE_CATEGORIES (
	perfetto::Category (xamarin::android::PerfettoConstants::ManagedRuntimeCategory.data ()).SetDescription ("Events from the MonoVM runtime")
);

namespace xamarin::android {
	namespace detail {
		template<typename TMonoType>
		concept SupportedMonoType = requires {
			requires std::same_as<MonoAssembly, TMonoType> ||
		         std::same_as<MonoImage, TMonoType> ||
		         std::same_as<MonoClass, TMonoType> ||
		         std::same_as<MonoVTable, TMonoType> ||
		         std::same_as<MonoMethod, TMonoType> ||
		         std::same_as<MonoObject, TMonoType>;
		};
	}

	class PerfettoSupport
	{
		force_inline static void set_desc_name (perfetto::protos::gen::TrackDescriptor &desc, const char *name) noexcept
		{
			if (name == nullptr) {
				return;
			}
			desc.set_name (name);
		}

	public:
		template<detail::SupportedMonoType TMonoType>
		force_inline static uint64_t get_track_id (TMonoType *data)
		{
			return reinterpret_cast<uint64_t>(data);
		}

		template<detail::SupportedMonoType TMonoType>
		force_inline static perfetto::Track get_name_annotated_track (TMonoType *data)
		{
			auto track = perfetto::Track::FromPointer (data, perfetto::ThreadTrack::Current ());
			auto desc = track.Serialize ();

			if constexpr (std::is_same_v<MonoAssembly, TMonoType>) {
				MonoAssemblyName *asm_name = mono_assembly_get_name (data);
				set_desc_name (desc, asm_name == nullptr ? nullptr : mono_assembly_name_get_name (asm_name));
			} else if constexpr (std::is_same_v<MonoImage, TMonoType>) {
				set_desc_name (desc, mono_image_get_name (data));
			} else if constexpr (std::is_same_v<MonoClass, TMonoType>) {
				std::string name{};
				append_full_class_name (data, name);
				desc.set_name (name);
			} else if constexpr (std::is_same_v<MonoVTable, TMonoType>) {
				return get_name_annotated_track (mono_vtable_class (data));
			} else if constexpr (std::is_same_v<MonoMethod, TMonoType>) {
				std::string name{};
				append_full_class_name (mono_method_get_class (data), name);
				name.append (".");
				name.append (mono_method_get_name (data));
				desc.set_name (name);
			} else if constexpr (std::is_same_v<MonoObject, TMonoType>) {
				return get_name_annotated_track (mono_object_get_class (data));
			}
			set_track_event_descriptor (track, desc);
			return track;
		}

	private:
		static void append_full_class_name (const MonoClass *klass, std::string &name)
		{
			name.append (mono_class_get_namespace (const_cast<MonoClass*>(klass)));
			name.append (".");
			name.append (mono_class_get_name (const_cast<MonoClass*>(klass)));
		}

	private:
		static void set_track_event_descriptor (perfetto::Track &track, perfetto::protos::gen::TrackDescriptor &desc);
	};
}
#endif // def PERFETTO_ENABLED

#endif // ndef PERFETTO_SUPPORT_HH
