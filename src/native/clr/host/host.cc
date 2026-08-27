#include <sys/types.h>
#include <dirent.h>
#include <dlfcn.h>

#include <cerrno>
#include <cinttypes>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>
#include <unistd.h>

#include <android/looper.h>

#include <coreclrhost.h>

#include <xamarin-app.hh>
#include <host/assembly-store.hh>
#include <host/gc-bridge.hh>
#include <host/fastdev-assemblies.hh>
#include <host/host.hh>
#include <host/host-environment-clr.hh>
#include <host/host-jni.hh>
#include <host/host-util.hh>
#include <host/os-bridge.hh>
#include <host/runtime-util.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/dso-loader.hh>
#include <runtime-base/jni-wrappers.hh>
#include <runtime-base/logger.hh>
#include <runtime-base/monodroid-dl.hh>
#include <runtime-base/monodroid-state.hh>
#include <runtime-base/timing-internal.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

void Host::clr_error_writer (const char *message) noexcept
{
	log_errorf (LOG_DEFAULT, "CLR error: %s", optional_string (message));
}

bool Host::clr_external_assembly_probe (const char *path, void **data_start, int64_t *size) noexcept
{
	// TODO: `path` might be a full path, make sure it isn't
	log_debugf (LOG_DEFAULT, "clr_external_assembly_probe (\"%s\"...)", optional_string (path));
	if (data_start == nullptr || size == nullptr) {
		return false; // TODO: abort instead?
	}

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.start_event (TimingEventKind::AssemblyLoad);
	}

	auto log_and_return = [](const char *name, void *data_start, int64_t size) {
		if (FastTiming::enabled ()) [[unlikely]] {
			internal_timing.end_event (true /* uses_more_info */);
			internal_timing.add_more_info (name);
		}

		log_debugf (
			LOG_ASSEMBLY,
			"Assembly '%s' data %smapped (%p, %" PRId64 " bytes)",
			optional_string (name),
			data_start == nullptr ? "not " : "",
			data_start,
			size
		);

		return data_start != nullptr && size > 0;
	};

	if constexpr (Constants::is_debug_build) {
		*data_start = FastDevAssemblies::open_assembly (path, *size);
		if (*data_start != nullptr && *size > 0) {
			return log_and_return (path, *data_start, *size);
		}

		log_warnf (
			LOG_ASSEMBLY,
			"Assembly '%s' not found in FastDev override directory. Attempting to load from assembly store",
			optional_string (path)
		);
	}

	*data_start = AssemblyStore::open_assembly (path, *size);

	return log_and_return (path, *data_start, *size);
}

[[gnu::always_inline]]
void Host::scan_filesystem_for_assemblies_and_libraries () noexcept
{
	std::string const& native_lib_dir = AndroidSystem::get_native_libraries_dir ();
	log_debugf (LOG_ASSEMBLY, "Looking for assemblies in '%s'", native_lib_dir.c_str ());

	DIR *lib_dir = opendir (native_lib_dir.c_str ());
	if (lib_dir == nullptr) [[unlikely]] {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"Unable to open native library directory '%s'. %s",
			native_lib_dir.c_str (),
			std::strerror (errno)
		);
	}

	int dir_fd = dirfd (lib_dir);
	if (dir_fd < 0) [[unlikely]] {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"Unable to obtain file descriptor for opened directory '%s'. %s",
			native_lib_dir.c_str (),
			std::strerror (errno)
		);
	}

	do {
		errno = 0;
		dirent *cur = readdir (lib_dir);
		if (cur == nullptr) {
			if (errno != 0) {
				log_warnf (LOG_ASSEMBLY, "Failed to open a directory entry from '%s': %s", native_lib_dir.c_str (), std::strerror (errno));
				continue; // No harm, keep going
			}
			break; // we're done
		}

		// We can ignore the obvious entries
		if (cur->d_name[0] == '.') {
			continue;
		}

		if (!found_assembly_store) {
			found_assembly_store = Constants::assembly_store_file_name.compare (cur->d_name) == 0;
			if (!found_assembly_store) {
				continue;
			}

			log_debugf (LOG_ASSEMBLY, "Found assembly store in '%s/%s'", native_lib_dir.c_str (), Constants::assembly_store_file_name.data ());

			std::string store_path = native_lib_dir;
			store_path.append ("/"sv);
			store_path.append (cur->d_name);
			map_assembly_store_via_dlopen (store_path.c_str ());
			break; // we've found all we need
		}
	} while (true);
	closedir (lib_dir);
}

void Host::gather_assemblies_and_libraries ([[maybe_unused]] jstring_array_wrapper& runtimeApks, [[maybe_unused]] bool have_split_apks)
{
	if (!application_config.have_assembly_store) {
		log_debugf (LOG_ASSEMBLY, "No assembly store configured; skipping assembly store discovery");
		return;
	}

	if (!AndroidSystem::is_embedded_dso_mode_enabled ()) {
		scan_filesystem_for_assemblies_and_libraries ();
		return;
	}

	// The assembly store is wrapped as a real shared library whose payload is exposed via the
	// `_assembly_store` dynamic symbol, so the dynamic linker has already located and mapped
	// it out of the APK for us. We resolve the payload with dlopen()+dlsym() instead of parsing the
	// APK ZIP central directory ourselves.
	map_assembly_store_via_dlopen (Constants::assembly_store_file_name.data ());
}

void Host::map_assembly_store_via_dlopen (const char *store_path) noexcept
{
	// RTLD_LOCAL: we only dlsym() our own handle, so there's no need to add the store's symbols to
	// the global lookup scope (RTLD_GLOBAL would just add linker bookkeeping).
	void *handle = ::dlopen (store_path, RTLD_NOW | RTLD_LOCAL);
	if (handle == nullptr) [[unlikely]] {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"Unable to dlopen() assembly store '%s': %s",
			optional_string (store_path),
			optional_string (::dlerror ())
		);
	}

	// NOTE: intentionally not calling dlclose() - we keep the store mapped for the lifetime of the app.
	void *payload = ::dlsym (handle, DLOPEN_ASSEMBLY_STORE_SYMBOL.data ());
	if (payload == nullptr) [[unlikely]] {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"Assembly store '%s' does not export the '%s' symbol",
			optional_string (store_path),
			DLOPEN_ASSEMBLY_STORE_SYMBOL.data ()
		);
	}

	log_debugf (LOG_ASSEMBLY, "Assembly store payload via dynamic symbol: %p (%s)", payload, optional_string (store_path));
	AssemblyStore::configure_from_payload (payload, [store_path]() -> std::string { return std::string { store_path }; });
	found_assembly_store = true;
}

[[gnu::always_inline]]
auto Host::create_delegate (
	std::string_view const& assembly_name, std::string_view const& type_name,
	std::string_view const& method_name) noexcept -> void*
{
	void *delegate = nullptr;
	int hr = coreclr_create_delegate (
		clr_host,
		domain_id,
		assembly_name.data (),
		type_name.data (),
		method_name.data (),
		&delegate
	);
	log_debugf (LOG_ASSEMBLY,
			   "%s@%s.%s delegate creation result == %x; delegate == %p",
			   assembly_name.data (),
			   type_name.data (),
			   method_name.data (),
			   static_cast<unsigned int>(hr),
			   delegate
	);

	// TODO: make S_OK & friends known to us
	if (hr != 0 /* S_OK */) {
		Helpers::abort_applicationf (
			LOG_DEFAULT,
			std::source_location::current (),
			"Failed to create delegate for %s.%s.%s (result == %x)",
			assembly_name.data (),
			type_name.data (),
			method_name.data (),
			static_cast<unsigned int>(hr)
		);
	}

	return delegate;
}

[[gnu::flatten, gnu::always_inline]]
void Host::preload_jni_libraries () noexcept
{
	// NOTE: when fixing a bug here, fix also the MonoVM code in src/native/mono/monodroid-glue.cc@preload_jni_libraries
	if (application_config.number_of_shared_libraries == 0) [[unlikely]] {
		return;
	}

	log_debugf (LOG_ASSEMBLY, "DSO jni preloads index stride == %u", dso_jni_preloads_idx_stride);

	if ((dso_jni_preloads_idx_count % dso_jni_preloads_idx_stride) != 0) [[unlikely]] {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"DSO preload index is invalid, size (%u) is not a multiple of %u",
			dso_jni_preloads_idx_count,
			dso_jni_preloads_idx_stride
		);
	}

	for (size_t i = 0; i < dso_jni_preloads_idx_count; i += dso_jni_preloads_idx_stride) {
		const size_t entry_index = dso_jni_preloads_idx[i];
		DSOCacheEntry &entry = dso_cache[entry_index];
		const std::string_view dso_name = MonodroidDl::get_dso_name (&entry);

		log_debugf (
			LOG_ASSEMBLY,
			"Preloading JNI shared library: %.*s (entry's index: %zu; name hash: %x)",
			static_cast<int>(dso_name.length ()),
			dso_name.data (),
			entry_index,
			entry.hash
		);

		void *handle = MonodroidDl::monodroid_dlopen (&entry, dso_name, RTLD_NOW);

		// Set handle in all the alias entries
		for (size_t j = 1; j < dso_jni_preloads_idx_stride; j++) {
			const size_t entry_alias_index = dso_jni_preloads_idx[i + j];
			DSOCacheEntry &entry_alias = dso_cache[entry_alias_index];
			const std::string_view entry_alias_name = MonodroidDl::get_dso_name (&entry);

			log_debugf (
				LOG_ASSEMBLY,
				"Putting JNI library handle in alias entry at index %zu: %.*s",
				entry_alias_index,
				static_cast<int>(entry_alias_name.length ()),
				entry_alias_name.data ()
			);
			entry_alias.handle = handle;
		}
	}
}

void Host::Java_mono_android_Runtime_initInternal (
	JNIEnv *env, jclass runtimeClass, jstring lang, jobjectArray runtimeApksJava,

	// TODO: MonoVM used this to load the profiler, probably won't be needed anymore
	[[maybe_unused]] jstring runtimeNativeLibDir,
	jobjectArray appDirs,

	// TODO: MonoVM used this to improve performance of System.DateTime + friends, might not be needed anymore
	[[maybe_unused]] jint localDateTimeOffset,
	jobject loader,

	// TODO: was used in the past to pre-load assemblies (some versions of Xamarin.Forms needed that).
	// Might not be needed anymore.
	[[maybe_unused]] jobjectArray assembliesJava,
	jboolean isEmulator, jboolean haveSplitApks) noexcept
{
	Logger::init_logging_categories ();

	// If fast logging is disabled, log messages immediately
	FastTiming::initialize ((Logger::log_timing_categories() & LogTimingCategories::FastBare) != LogTimingCategories::FastBare);

	if (FastTiming::enabled ()) [[unlikely]] {
		_timing = &_timing_instance;
		internal_timing.start_event (TimingEventKind::TotalRuntimeInit);
	}

	jstring_array_wrapper applicationDirs (env, appDirs);
	jstring_wrapper language (env, lang);
	jstring_wrapper &files_dir = applicationDirs[Constants::APP_DIRS_FILES_DIR_INDEX];
	jstring_wrapper &cache_dir = applicationDirs[Constants::APP_DIRS_CACHE_DIR_INDEX];
	HostEnvironment::setup_environment (
		language,
		files_dir,
		cache_dir
	);
	HostEnvironment::set_variable_if_unset ("DOTNET_CrashReportRootPath"sv, cache_dir);

	java_TimeZone = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "java_util_TimeZone"sv, true);

	AndroidSystem::detect_embedded_dso_mode (applicationDirs);
	AndroidSystem::set_running_in_emulator (isEmulator);
	AndroidSystem::set_primary_override_dir (files_dir);
	AndroidSystem::set_app_code_cache_dir (applicationDirs[Constants::APP_DIRS_CODE_CACHE_DIR_INDEX]);
	AndroidSystem::create_update_dir (AndroidSystem::get_primary_override_dir ());
	AndroidSystem::setup_environment ();
	Logger::init_reference_logging (AndroidSystem::get_primary_override_dir ().c_str ());

	jstring_array_wrapper runtimeApks (env, runtimeApksJava);
	AndroidSystem::setup_app_library_directories (runtimeApks, applicationDirs, haveSplitApks);

	gather_assemblies_and_libraries (runtimeApks, haveSplitApks);

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.start_event (TimingEventKind::ManagedRuntimeInit);
	}

	coreclr_set_error_writer (clr_error_writer);
	// We REALLY shouldn't be doing this
	snprintf (host_contract_ptr_buffer.data (), host_contract_ptr_buffer.size (), "%p", &runtime_contract);

	// These indices are load-bearing: the application build emits the property names in this
	// exact order (see `ApplicationConfigNativeAssemblyGeneratorCLR`) so that we can fill in
	// the values here without searching the names array.
	constexpr size_t RUNTIME_PROPERTY_INDEX_HOST_CONTRACT = 0;
	constexpr size_t RUNTIME_PROPERTY_INDEX_RUNTIME_IDENTIFIER = 1;
	constexpr size_t RUNTIME_PROPERTY_INDEX_APP_CONTEXT_BASE_DIRECTORY = 2;

	init_runtime_property_values[RUNTIME_PROPERTY_INDEX_HOST_CONTRACT] = host_contract_ptr_buffer.data ();

	// `hostfxr` normally hands `RUNTIME_IDENTIFIER` to the runtime, but we don't use `hostfxr`.
	// Without it, `RuntimeInformation.RuntimeIdentifier` returns "unknown". The value can only
	// come from here: `libxamarin-app.so` is per-ABI, but it is generated from the (shared)
	// `*.runtimeconfig.json`, which knows nothing about the ABI it is being built for.
	init_runtime_property_values[RUNTIME_PROPERTY_INDEX_RUNTIME_IDENTIFIER] = const_cast<char*>(Constants::runtime_identifier.data ());

	// Likewise for `APP_CONTEXT_BASE_DIRECTORY`, which backs `AppContext.BaseDirectory`. Without it
	// the runtime falls back to the directory of `Assembly.GetEntryAssembly ()`, which is the empty
	// string for us since assemblies are read straight out of the APK. Point it at the application's
	// files directory, the same value MonoVM has always used. `.NET` terminates the base directory
	// with a directory separator, so we do too.
	// Storage must outlive `coreclr_initialize`, hence the function-local static. Assign on every
	// call rather than relying on the initializer, which would only ever see the first `files_dir`.
	static std::string app_context_base_directory;
	app_context_base_directory.assign (files_dir.get_cstr ());
	if (!app_context_base_directory.ends_with ('/')) {
		app_context_base_directory.push_back ('/');
	}
	init_runtime_property_values[RUNTIME_PROPERTY_INDEX_APP_CONTEXT_BASE_DIRECTORY] = const_cast<char*>(app_context_base_directory.c_str ());

	const char **prop_names = init_runtime_property_names;
	const char **prop_values = const_cast<const char**>(init_runtime_property_values);
	int prop_count = static_cast<int>(application_config.number_of_runtime_properties);

	// In Debug builds with FastDev, append `TRUSTED_PLATFORM_ASSEMBLIES` with full
	// paths to the assemblies pushed into `.__override__/<arch>/`. CoreCLR then
	// opens those files from disk so `Assembly.Location` is populated and
	// `StackTraceSymbols` can find sibling `.pdb` files for runtime-rendered
	// managed stack traces (file/line).
	if constexpr (Constants::is_debug_build) {
		// Storage must outlive `coreclr_initialize`; function-local statics
		// give us process lifetime without polluting global namespace.
		static std::string fastdev_tpa_list;
		static std::vector<const char*> fastdev_prop_names;
		static std::vector<const char*> fastdev_prop_values;

		if (FastDevAssemblies::build_tpa_list (fastdev_tpa_list)) {
			fastdev_prop_names.assign (prop_names, prop_names + prop_count);
			fastdev_prop_values.assign (prop_values, prop_values + prop_count);
			fastdev_prop_names.push_back (HOST_PROPERTY_TRUSTED_PLATFORM_ASSEMBLIES);
			fastdev_prop_values.push_back (fastdev_tpa_list.c_str ());

			prop_names = fastdev_prop_names.data ();
			prop_values = fastdev_prop_values.data ();
			prop_count = static_cast<int>(fastdev_prop_names.size ());
		}
	}

	int hr = FastTiming::time_call ("coreclr_initialize"sv, coreclr_initialize,
		application_config.android_package_name,
		"Xamarin.Android",
		prop_count,
		prop_names,
		prop_values,
		&clr_host,
		&domain_id
	);

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.end_event ();
	}

	// TODO: make S_OK & friends known to us
	if (hr != 0 /* S_OK */) {
		Helpers::abort_applicationf (
			LOG_DEFAULT,
			std::source_location::current (),
			"Failed to initialize CoreCLR. Error code: %x",
			static_cast<unsigned int>(hr)
		);
	}

	abort_unless (
		clr_host != nullptr,
		[&hr] {
			return detail::_format_message ("Failure to initialize CoreCLR host instance. Returned result 0x%x", static_cast<unsigned int>(hr));
		}
	);

	DsoLoader::init (
		env,
		RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "java_lang_System", true),
		ALooper_forThread (), // main thread looper
		gettid ()
	);

	preload_jni_libraries ();

	struct JnienvInitializeArgs init = {};
	init.javaVm                                         = jvm;
	init.env                                            = env;
	init.logCategories                                  = log_categories;
	init.version                                        = env->GetVersion ();
	init.brokenExceptionTransitions                     = 0;
	init.packageNamingPolicy                            = static_cast<int>(application_config.package_naming_policy);
	init.boundExceptionType                             = 0; // System
	init.jniAddNativeMethodRegistrationAttributePresent = application_config.jni_add_native_method_registration_attribute_present ? 1 : 0;
	init.jniRemappingInUse                              = application_config.jni_remapping_replacement_type_count > 0 || application_config.jni_remapping_replacement_method_index_entry_count > 0;
	init.marshalMethodsEnabled                          = application_config.marshal_methods_enabled;

	// GC threshold is 90% of the max GREF count
	init.grefGcThreshold                                = static_cast<int>(AndroidSystem::get_gref_gc_threshold ());
	init.grefClass                                      = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "java_lang_Class"sv, true);
	Class_getName                                       = env->GetMethodID (init.grefClass, "getName", "()Ljava/lang/String;");

	jclass lrefLoaderClass                              = env->GetObjectClass (loader);
	init.Loader_loadClass                               = env->GetMethodID (lrefLoaderClass, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
	env->DeleteLocalRef (lrefLoaderClass);

	init.grefLoader                                     = env->NewGlobalRef (loader);
	init.grefIGCUserPeer                                = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "mono_android_IGCUserPeer"sv, true);
	init.grefGCUserPeerable                             = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "net_dot_jni_GCUserPeerable"sv, true);

	log_infof (LOG_GC, "GREF GC Threshold: %d", init.grefGcThreshold);

	OSBridge::initialize_on_runtime_init (env, runtimeClass);
	GCBridge::initialize_on_runtime_init (env, runtimeClass);

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.start_event (TimingEventKind::NativeToManagedTransition);
	}

	log_debugf (LOG_ASSEMBLY, "Creating UCO delegate to %s.Initialize", Constants::JNIENVINIT_FULL_TYPE_NAME.data ());
	void *delegate = nullptr;
	delegate = FastTiming::time_call ("create_delegate for Initialize"sv, create_delegate, Constants::MONO_ANDROID_ASSEMBLY_NAME, Constants::JNIENVINIT_FULL_TYPE_NAME, "Initialize"sv);
	auto initialize = reinterpret_cast<jnienv_initialize_fn> (delegate);
	abort_unless (
		initialize != nullptr,
		[] {
			return detail::_format_message (
				"Failed to obtain unmanaged-callers-only pointer to the %s.%s.Initialize method.",
				Constants::MONO_ANDROID_ASSEMBLY_NAME,
				Constants::JNIENVINIT_FULL_TYPE_NAME
			);
		}
	);

	log_debugf (LOG_DEFAULT, "Calling into managed runtime init");
	FastTiming::time_call ("JNIEnv.Initialize UCO"sv, initialize, &init);

	// RegisterJniNatives and PropagateUncaughtException are returned from Initialize
	// to avoid extra create_delegate calls. RegisterJniNatives is null when using the
	// trimmable typemap path (the method is trimmed; registration is handled in managed code).
	jnienv_register_jni_natives = init.registerJniNativesFn;
	jnienv_propagate_uncaught_exception = init.propagateUncaughtExceptionFn;
	abort_unless (jnienv_propagate_uncaught_exception != nullptr, "Failed to obtain unmanaged-callers-only function pointer to the PropagateUncaughtException method.");

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.end_event (); // native to managed
		internal_timing.end_event (); // total init time
	}

	MonodroidState::mark_startup_done ();
}

void Host::Java_mono_android_Runtime_register (JNIEnv *env, jstring managedType, jclass nativeClass, jstring methods) noexcept
{
	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.start_event (TimingEventKind::RuntimeRegister);
	}

	jsize managedType_len = env->GetStringLength (managedType);
	const jchar *managedType_ptr = env->GetStringChars (managedType, nullptr);
	int methods_len = env->GetStringLength (methods);
	const jchar *methods_ptr = env->GetStringChars (methods, nullptr);

	const char *mt_ptr = env->GetStringUTFChars (managedType, nullptr);
	log_debugf (LOG_ASSEMBLY, "Registering type: '%s'", optional_string (mt_ptr));
	env->ReleaseStringUTFChars (managedType, mt_ptr);

	// TODO: must attach thread to the runtime here
	if (jnienv_register_jni_natives != nullptr) {
		jnienv_register_jni_natives (managedType_ptr, managedType_len, nativeClass, methods_ptr, methods_len);
	}

	env->ReleaseStringChars (methods, methods_ptr);
	env->ReleaseStringChars (managedType, managedType_ptr);

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.end_event (true /* uses_more_info */);

		mt_ptr = env->GetStringUTFChars (managedType, nullptr);
		internal_timing.add_more_info (mt_ptr);
		env->ReleaseStringUTFChars (managedType, mt_ptr);
	}
}

void Host::Java_mono_android_Runtime_registerNatives ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jclass nativeClass) noexcept
{
	// In the trimmable typemap path, registerNatives is handled entirely in managed code
	// via a dynamically registered JNI native method. This C++ stub exists only as a
	// fallback for the legacy code path (which doesn't use registerNatives).
}

auto HostCommon::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	jvm = vm;

	JNIEnv *env = nullptr;
	vm->GetEnv ((void**)&env, JNI_VERSION_1_6);
	OSBridge::initialize_on_onload (vm, env);
	GCBridge::initialize_on_onload (env);

	AndroidSystem::init_max_gref_count ();
	return JNI_VERSION_1_6;
}

void Host::propagate_uncaught_exception (JNIEnv *env, jobject javaThread, jthrowable javaException) noexcept
{
	if (jnienv_propagate_uncaught_exception == nullptr) {
		log_warnf (LOG_DEFAULT, "propagate_uncaught_exception called before JNIEnvInit.PropagateUncaughtException was initialized");
		return;
	}

	jnienv_propagate_uncaught_exception (env, javaThread, javaException);
}
