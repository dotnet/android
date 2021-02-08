/*
 * Generated file; DO NOT EDIT!
 *
 * To make changes, edit Java.Interop/build-tools/jnienv-gen and rerun
 */

#if !defined (__JAVA_INTEROP_NATIVE_H)
#define __JAVA_INTEROP_NATIVE_H

#include <jni.h>

typedef jmethodID jstaticmethodID;
typedef jfieldID  jstaticfieldID;
typedef jobject   jglobal;

#if !defined(JI_NO_VISIBILITY)
	/* VS 2010 and later have stdint.h */
	#if defined(_MSC_VER)

		#define JI_API_EXPORT __declspec(dllexport)
		#define JI_API_IMPORT __declspec(dllimport)

	#else   /* defined(_MSC_VER */

		#define JI_API_EXPORT __attribute__ ((visibility ("default")))
		#define JI_API_IMPORT

	#endif  /* !defined(_MSC_VER) */

	#if defined(JI_DLL_EXPORT)
		#define JI_API JI_API_EXPORT
	#elif defined(JI_DLL_IMPORT)
		#define JI_API JI_API_IMPORT
	#else   /* !defined(JI_DLL_IMPORT) && !defined(JI_API_IMPORT) */
		#define JI_API
	#endif  /* JI_DLL_EXPORT... */
#else // JI_NO_VISIBILITY
	#define JI_API
#endif // JI_NO_VISIBILITY

JI_API jint java_interop_jnienv_get_version (JNIEnv *env);
JI_API jclass java_interop_jnienv_define_class (JNIEnv *env, jthrowable *_thrown, const char* name, jobject loader, const jbyte* buffer, jsize bufferLength);
JI_API jclass java_interop_jnienv_find_class (JNIEnv *env, jthrowable *_thrown, const char* classname);
JI_API jobject java_interop_jnienv_to_reflected_method (JNIEnv *env, jthrowable *_thrown, jclass type, jmethodID method, jboolean isStatic);
JI_API jclass java_interop_jnienv_get_superclass (JNIEnv *env, jclass type);
JI_API jboolean java_interop_jnienv_is_assignable_from (JNIEnv *env, jclass class1, jclass class2);
JI_API jobject java_interop_jnienv_to_reflected_field (JNIEnv *env, jthrowable *_thrown, jclass type, jfieldID field, jboolean isStatic);
JI_API jint java_interop_jnienv_throw (JNIEnv *env, jthrowable toThrow);
JI_API jint java_interop_jnienv_throw_new (JNIEnv *env, jclass type, const char* message);
JI_API jthrowable java_interop_jnienv_exception_occurred (JNIEnv *env);
JI_API void java_interop_jnienv_exception_describe (JNIEnv *env);
JI_API void java_interop_jnienv_exception_clear (JNIEnv *env);
JI_API void java_interop_jnienv_fatal_error (JNIEnv *env, const char* message);
JI_API jint java_interop_jnienv_push_local_frame (JNIEnv *env, jint capacity);
JI_API jobject java_interop_jnienv_pop_local_frame (JNIEnv *env, jobject result);
JI_API jglobal java_interop_jnienv_new_global_ref (JNIEnv *env, jobject instance);
JI_API void java_interop_jnienv_delete_global_ref (JNIEnv *env, jobject instance);
JI_API void java_interop_jnienv_delete_local_ref (JNIEnv *env, jobject instance);
JI_API jboolean java_interop_jnienv_is_same_object (JNIEnv *env, jobject object1, jobject object2);
JI_API jobject java_interop_jnienv_new_local_ref (JNIEnv *env, jobject instance);
JI_API jint java_interop_jnienv_ensure_local_capacity (JNIEnv *env, jint capacity);
JI_API jobject java_interop_jnienv_alloc_object (JNIEnv *env, jthrowable *_thrown, jclass type);
JI_API jobject java_interop_jnienv_new_object (JNIEnv *env, jthrowable *_thrown, jclass type, jmethodID method);
JI_API jobject java_interop_jnienv_new_object_a (JNIEnv *env, jthrowable *_thrown, jclass type, jmethodID method, jvalue* args);
JI_API jclass java_interop_jnienv_get_object_class (JNIEnv *env, jobject instance);
JI_API jboolean java_interop_jnienv_is_instance_of (JNIEnv *env, jobject instance, jclass type);
JI_API jmethodID java_interop_jnienv_get_method_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature);
JI_API jobject java_interop_jnienv_call_object_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jobject java_interop_jnienv_call_object_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jboolean java_interop_jnienv_call_boolean_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jboolean java_interop_jnienv_call_boolean_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jbyte java_interop_jnienv_call_byte_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jbyte java_interop_jnienv_call_byte_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jchar java_interop_jnienv_call_char_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jchar java_interop_jnienv_call_char_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jshort java_interop_jnienv_call_short_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jshort java_interop_jnienv_call_short_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jint java_interop_jnienv_call_int_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jint java_interop_jnienv_call_int_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jlong java_interop_jnienv_call_long_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jlong java_interop_jnienv_call_long_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jfloat java_interop_jnienv_call_float_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jfloat java_interop_jnienv_call_float_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jdouble java_interop_jnienv_call_double_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API jdouble java_interop_jnienv_call_double_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API void java_interop_jnienv_call_void_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method);
JI_API void java_interop_jnienv_call_void_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args);
JI_API jobject java_interop_jnienv_call_nonvirtual_object_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jobject java_interop_jnienv_call_nonvirtual_object_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jboolean java_interop_jnienv_call_nonvirtual_boolean_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jboolean java_interop_jnienv_call_nonvirtual_boolean_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jbyte java_interop_jnienv_call_nonvirtual_byte_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jbyte java_interop_jnienv_call_nonvirtual_byte_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jchar java_interop_jnienv_call_nonvirtual_char_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jchar java_interop_jnienv_call_nonvirtual_char_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jshort java_interop_jnienv_call_nonvirtual_short_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jshort java_interop_jnienv_call_nonvirtual_short_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jint java_interop_jnienv_call_nonvirtual_int_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jint java_interop_jnienv_call_nonvirtual_int_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jlong java_interop_jnienv_call_nonvirtual_long_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jlong java_interop_jnienv_call_nonvirtual_long_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jfloat java_interop_jnienv_call_nonvirtual_float_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jfloat java_interop_jnienv_call_nonvirtual_float_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jdouble java_interop_jnienv_call_nonvirtual_double_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API jdouble java_interop_jnienv_call_nonvirtual_double_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API void java_interop_jnienv_call_nonvirtual_void_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method);
JI_API void java_interop_jnienv_call_nonvirtual_void_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args);
JI_API jfieldID java_interop_jnienv_get_field_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature);
JI_API jobject java_interop_jnienv_get_object_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jboolean java_interop_jnienv_get_boolean_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jbyte java_interop_jnienv_get_byte_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jchar java_interop_jnienv_get_char_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jshort java_interop_jnienv_get_short_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jint java_interop_jnienv_get_int_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jlong java_interop_jnienv_get_long_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jfloat java_interop_jnienv_get_float_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API jdouble java_interop_jnienv_get_double_field (JNIEnv *env, jobject instance, jfieldID field);
JI_API void java_interop_jnienv_set_object_field (JNIEnv *env, jobject instance, jfieldID field, jobject value);
JI_API void java_interop_jnienv_set_boolean_field (JNIEnv *env, jobject instance, jfieldID field, jboolean value);
JI_API void java_interop_jnienv_set_byte_field (JNIEnv *env, jobject instance, jfieldID field, jbyte value);
JI_API void java_interop_jnienv_set_char_field (JNIEnv *env, jobject instance, jfieldID field, jchar value);
JI_API void java_interop_jnienv_set_short_field (JNIEnv *env, jobject instance, jfieldID field, jshort value);
JI_API void java_interop_jnienv_set_int_field (JNIEnv *env, jobject instance, jfieldID field, jint value);
JI_API void java_interop_jnienv_set_long_field (JNIEnv *env, jobject instance, jfieldID field, jlong value);
JI_API void java_interop_jnienv_set_float_field (JNIEnv *env, jobject instance, jfieldID field, jfloat value);
JI_API void java_interop_jnienv_set_double_field (JNIEnv *env, jobject instance, jfieldID field, jdouble value);
JI_API jstaticmethodID java_interop_jnienv_get_static_method_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature);
JI_API jobject java_interop_jnienv_call_static_object_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jobject java_interop_jnienv_call_static_object_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jboolean java_interop_jnienv_call_static_boolean_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jboolean java_interop_jnienv_call_static_boolean_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jbyte java_interop_jnienv_call_static_byte_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jbyte java_interop_jnienv_call_static_byte_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jchar java_interop_jnienv_call_static_char_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jchar java_interop_jnienv_call_static_char_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jshort java_interop_jnienv_call_static_short_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jshort java_interop_jnienv_call_static_short_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jint java_interop_jnienv_call_static_int_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jint java_interop_jnienv_call_static_int_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jlong java_interop_jnienv_call_static_long_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jlong java_interop_jnienv_call_static_long_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jfloat java_interop_jnienv_call_static_float_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jfloat java_interop_jnienv_call_static_float_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jdouble java_interop_jnienv_call_static_double_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API jdouble java_interop_jnienv_call_static_double_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API void java_interop_jnienv_call_static_void_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method);
JI_API void java_interop_jnienv_call_static_void_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args);
JI_API jstaticfieldID java_interop_jnienv_get_static_field_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature);
JI_API jobject java_interop_jnienv_get_static_object_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jboolean java_interop_jnienv_get_static_boolean_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jbyte java_interop_jnienv_get_static_byte_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jchar java_interop_jnienv_get_static_char_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jshort java_interop_jnienv_get_static_short_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jint java_interop_jnienv_get_static_int_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jlong java_interop_jnienv_get_static_long_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jfloat java_interop_jnienv_get_static_float_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API jdouble java_interop_jnienv_get_static_double_field (JNIEnv *env, jclass type, jstaticfieldID field);
JI_API void java_interop_jnienv_set_static_object_field (JNIEnv *env, jclass type, jstaticfieldID field, jobject value);
JI_API void java_interop_jnienv_set_static_boolean_field (JNIEnv *env, jclass type, jstaticfieldID field, jboolean value);
JI_API void java_interop_jnienv_set_static_byte_field (JNIEnv *env, jclass type, jstaticfieldID field, jbyte value);
JI_API void java_interop_jnienv_set_static_char_field (JNIEnv *env, jclass type, jstaticfieldID field, jchar value);
JI_API void java_interop_jnienv_set_static_short_field (JNIEnv *env, jclass type, jstaticfieldID field, jshort value);
JI_API void java_interop_jnienv_set_static_int_field (JNIEnv *env, jclass type, jstaticfieldID field, jint value);
JI_API void java_interop_jnienv_set_static_long_field (JNIEnv *env, jclass type, jstaticfieldID field, jlong value);
JI_API void java_interop_jnienv_set_static_float_field (JNIEnv *env, jclass type, jstaticfieldID field, jfloat value);
JI_API void java_interop_jnienv_set_static_double_field (JNIEnv *env, jclass type, jstaticfieldID field, jdouble value);
JI_API jstring java_interop_jnienv_new_string (JNIEnv *env, jthrowable *_thrown, jchar* unicodeChars, jsize length);
JI_API jsize java_interop_jnienv_get_string_length (JNIEnv *env, jstring stringInstance);
JI_API const jchar* java_interop_jnienv_get_string_chars (JNIEnv *env, jstring stringInstance, jboolean* isCopy);
JI_API void java_interop_jnienv_release_string_chars (JNIEnv *env, jstring stringInstance, jchar* chars);
JI_API jsize java_interop_jnienv_get_array_length (JNIEnv *env, jarray array);
JI_API jobjectArray java_interop_jnienv_new_object_array (JNIEnv *env, jthrowable *_thrown, jsize length, jclass elementClass, jobject initialElement);
JI_API jobject java_interop_jnienv_get_object_array_element (JNIEnv *env, jthrowable *_thrown, jobjectArray array, jsize index);
JI_API void java_interop_jnienv_set_object_array_element (JNIEnv *env, jthrowable *_thrown, jobjectArray array, jsize index, jobject value);
JI_API jbooleanArray java_interop_jnienv_new_boolean_array (JNIEnv *env, jsize length);
JI_API jbyteArray java_interop_jnienv_new_byte_array (JNIEnv *env, jsize length);
JI_API jcharArray java_interop_jnienv_new_char_array (JNIEnv *env, jsize length);
JI_API jshortArray java_interop_jnienv_new_short_array (JNIEnv *env, jsize length);
JI_API jintArray java_interop_jnienv_new_int_array (JNIEnv *env, jsize length);
JI_API jlongArray java_interop_jnienv_new_long_array (JNIEnv *env, jsize length);
JI_API jfloatArray java_interop_jnienv_new_float_array (JNIEnv *env, jsize length);
JI_API jdoubleArray java_interop_jnienv_new_double_array (JNIEnv *env, jsize length);
JI_API jboolean* java_interop_jnienv_get_boolean_array_elements (JNIEnv *env, jbooleanArray array, jboolean* isCopy);
JI_API jbyte* java_interop_jnienv_get_byte_array_elements (JNIEnv *env, jbyteArray array, jboolean* isCopy);
JI_API jchar* java_interop_jnienv_get_char_array_elements (JNIEnv *env, jcharArray array, jboolean* isCopy);
JI_API jshort* java_interop_jnienv_get_short_array_elements (JNIEnv *env, jshortArray array, jboolean* isCopy);
JI_API jint* java_interop_jnienv_get_int_array_elements (JNIEnv *env, jintArray array, jboolean* isCopy);
JI_API jlong* java_interop_jnienv_get_long_array_elements (JNIEnv *env, jlongArray array, jboolean* isCopy);
JI_API jfloat* java_interop_jnienv_get_float_array_elements (JNIEnv *env, jfloatArray array, jboolean* isCopy);
JI_API jdouble* java_interop_jnienv_get_double_array_elements (JNIEnv *env, jdoubleArray array, jboolean* isCopy);
JI_API void java_interop_jnienv_release_boolean_array_elements (JNIEnv *env, jbooleanArray array, jboolean* elements, jint mode);
JI_API void java_interop_jnienv_release_byte_array_elements (JNIEnv *env, jbyteArray array, jbyte* elements, jint mode);
JI_API void java_interop_jnienv_release_char_array_elements (JNIEnv *env, jcharArray array, jchar* elements, jint mode);
JI_API void java_interop_jnienv_release_short_array_elements (JNIEnv *env, jshortArray array, jshort* elements, jint mode);
JI_API void java_interop_jnienv_release_int_array_elements (JNIEnv *env, jintArray array, jint* elements, jint mode);
JI_API void java_interop_jnienv_release_long_array_elements (JNIEnv *env, jlongArray array, jlong* elements, jint mode);
JI_API void java_interop_jnienv_release_float_array_elements (JNIEnv *env, jfloatArray array, jfloat* elements, jint mode);
JI_API void java_interop_jnienv_release_double_array_elements (JNIEnv *env, jdoubleArray array, jdouble* elements, jint mode);
JI_API void java_interop_jnienv_get_boolean_array_region (JNIEnv *env, jthrowable *_thrown, jbooleanArray array, jsize start, jsize length, jboolean* buffer);
JI_API void java_interop_jnienv_get_byte_array_region (JNIEnv *env, jthrowable *_thrown, jbyteArray array, jsize start, jsize length, jbyte* buffer);
JI_API void java_interop_jnienv_get_char_array_region (JNIEnv *env, jthrowable *_thrown, jcharArray array, jsize start, jsize length, jchar* buffer);
JI_API void java_interop_jnienv_get_short_array_region (JNIEnv *env, jthrowable *_thrown, jshortArray array, jsize start, jsize length, jshort* buffer);
JI_API void java_interop_jnienv_get_int_array_region (JNIEnv *env, jthrowable *_thrown, jintArray array, jsize start, jsize length, jint* buffer);
JI_API void java_interop_jnienv_get_long_array_region (JNIEnv *env, jthrowable *_thrown, jlongArray array, jsize start, jsize length, jlong* buffer);
JI_API void java_interop_jnienv_get_float_array_region (JNIEnv *env, jthrowable *_thrown, jlongArray array, jsize start, jsize length, jfloat* buffer);
JI_API void java_interop_jnienv_get_double_array_region (JNIEnv *env, jthrowable *_thrown, jdoubleArray array, jsize start, jsize length, jdouble* buffer);
JI_API void java_interop_jnienv_set_boolean_array_region (JNIEnv *env, jthrowable *_thrown, jbooleanArray array, jsize start, jsize length, jboolean* buffer);
JI_API void java_interop_jnienv_set_byte_array_region (JNIEnv *env, jthrowable *_thrown, jbyteArray array, jsize start, jsize length, jbyte* buffer);
JI_API void java_interop_jnienv_set_char_array_region (JNIEnv *env, jthrowable *_thrown, jcharArray array, jsize start, jsize length, const jchar* buffer);
JI_API void java_interop_jnienv_set_short_array_region (JNIEnv *env, jthrowable *_thrown, jshortArray array, jsize start, jsize length, jshort* buffer);
JI_API void java_interop_jnienv_set_int_array_region (JNIEnv *env, jthrowable *_thrown, jintArray array, jsize start, jsize length, jint* buffer);
JI_API void java_interop_jnienv_set_long_array_region (JNIEnv *env, jthrowable *_thrown, jlongArray array, jsize start, jsize length, jlong* buffer);
JI_API void java_interop_jnienv_set_float_array_region (JNIEnv *env, jthrowable *_thrown, jfloatArray array, jsize start, jsize length, jfloat* buffer);
JI_API void java_interop_jnienv_set_double_array_region (JNIEnv *env, jthrowable *_thrown, jdoubleArray array, jsize start, jsize length, jdouble* buffer);
JI_API jint java_interop_jnienv_register_natives (JNIEnv *env, jthrowable *_thrown, jclass type, const JNINativeMethod* methods, jint numMethods);
JI_API jint java_interop_jnienv_unregister_natives (JNIEnv *env, jclass type);
JI_API jint java_interop_jnienv_monitor_enter (JNIEnv *env, jobject instance);
JI_API jint java_interop_jnienv_monitor_exit (JNIEnv *env, jobject instance);
JI_API jint java_interop_jnienv_get_java_vm (JNIEnv *env, JavaVM** vm);
JI_API void* java_interop_jnienv_get_primitive_array_critical (JNIEnv *env, jarray array, jboolean* isCopy);
JI_API void java_interop_jnienv_release_primitive_array_critical (JNIEnv *env, jarray array, void* carray, jint mode);
JI_API jweak java_interop_jnienv_new_weak_global_ref (JNIEnv *env, jobject instance);
JI_API void java_interop_jnienv_delete_weak_global_ref (JNIEnv *env, jobject instance);
JI_API jboolean java_interop_jnienv_exception_check (JNIEnv *env);
JI_API jobject java_interop_jnienv_new_direct_byte_buffer (JNIEnv *env, jthrowable *_thrown, void* address, jlong capacity);
JI_API void* java_interop_jnienv_get_direct_buffer_address (JNIEnv *env, jobject buffer);
JI_API jlong java_interop_jnienv_get_direct_buffer_capacity (JNIEnv *env, jobject buffer);
JI_API jobjectRefType java_interop_jnienv_get_object_ref_type (JNIEnv *env, jobject instance);

#endif // __JAVA_INTEROP_NATIVE_H
