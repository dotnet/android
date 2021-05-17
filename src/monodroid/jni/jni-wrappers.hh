// Dear Emacs, this is a -*- C++ -*- header
#ifndef __JNI_WRAPPERS_H
#define __JNI_WRAPPERS_H

#include <jni.h>
#include <cstdlib>

#include "cpp-util.hh"

#ifdef __cplusplus

namespace xamarin::android
{
	class jstring_array_wrapper;

	class jstring_wrapper
	{
	public:
		explicit jstring_wrapper (JNIEnv *_env) noexcept
			: env (_env),
			  jstr (nullptr)
		{
			abort_if_invalid_pointer_argument (_env);
		}

		explicit jstring_wrapper (JNIEnv *_env, const jobject jo) noexcept
			: env (_env),
			  jstr (reinterpret_cast<jstring> (jo))
		{
			abort_if_invalid_pointer_argument (_env);
		}

		explicit jstring_wrapper (JNIEnv *_env, const jstring js) noexcept
			: env (_env),
			  jstr (js)
		{
			abort_if_invalid_pointer_argument (_env);
		}

		jstring_wrapper (const jstring_wrapper&) = delete;

		~jstring_wrapper () noexcept
		{
			release ();
		}

		jstring_wrapper& operator=(const jstring_wrapper&) = delete;

		bool hasValue () noexcept
		{
			return jstr != nullptr;
		}

		const char* get_cstr () noexcept
		{
			if (jstr == nullptr)
				return nullptr;
			if (cstr == nullptr && env != nullptr)
				cstr = env->GetStringUTFChars (jstr, nullptr);

			return cstr;
		}

		jstring_wrapper& operator= (const jobject new_jo) noexcept
		{
			assign (reinterpret_cast<jstring> (new_jo));
			return *this;
		}

		jstring_wrapper& operator= (const jstring new_js) noexcept
		{
			assign (new_js);
			return *this;
		}

	protected:
		void release () noexcept
		{
			if (jstr == nullptr || cstr == nullptr || env == nullptr)
				return;
			env->ReleaseStringUTFChars (jstr, cstr);
			jobjectRefType type = env->GetObjectRefType (jstr);
			switch (type) {
				case JNILocalRefType:
					env->DeleteLocalRef (jstr);
					break;

				case JNIGlobalRefType:
					env->DeleteGlobalRef (jstr);
					break;

				case JNIWeakGlobalRefType:
					env->DeleteWeakGlobalRef (jstr);
					break;

				case JNIInvalidRefType: // To hush compiler warning
					break;
			}

			jstr = nullptr;
			cstr = nullptr;
		}

		void assign (const jstring new_js) noexcept
		{
			release ();
			if (new_js == nullptr)
				return;

			jstr = new_js;
			cstr = nullptr;
		}

		friend class jstring_array_wrapper;

	private:
		jstring_wrapper ()
			: env (nullptr),
			  jstr (nullptr)
		{}

	private:
		JNIEnv *env;
		jstring jstr;
		const char *cstr = nullptr;
	};

	class jstring_array_wrapper
	{
	public:
		explicit jstring_array_wrapper (JNIEnv *_env) noexcept
			: jstring_array_wrapper(_env, nullptr)
		{
		}

		explicit jstring_array_wrapper (JNIEnv *_env, jobjectArray _arr)
			: env (_env),
			  arr (_arr)
		{
			abort_if_invalid_pointer_argument (_env);
			if (_arr != nullptr) {
				len = static_cast<size_t>(_env->GetArrayLength (_arr));
				if (len > sizeof (static_wrappers) / sizeof (jstring_wrapper))
					wrappers = new jstring_wrapper [len];
				else
					wrappers = static_wrappers;
			} else {
				len = 0;
				wrappers = nullptr;
			}
		}

		~jstring_array_wrapper () noexcept
		{
			if (wrappers != nullptr && wrappers != static_wrappers)
				delete[] wrappers;
		}

		size_t get_length () const noexcept
		{
			return len;
		}

		jstring_wrapper& operator[] (size_t index) noexcept
		{
			if (index >= len)
				return invalid_wrapper;

			if (wrappers [index].env == nullptr) {
				wrappers [index].env = env;
				wrappers [index].jstr = reinterpret_cast <jstring> (env->GetObjectArrayElement (arr, static_cast<jsize>(index)));
			}

			return wrappers [index];
		}

	private:
		JNIEnv *env;
		jobjectArray arr;
		size_t len;
		jstring_wrapper *wrappers;
		jstring_wrapper  static_wrappers[5];
		jstring_wrapper  invalid_wrapper;
	};
}

#endif // __cplusplus
#endif // __JNI_WRAPPERS_H
