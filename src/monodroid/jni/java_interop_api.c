/*
 * Generated file; DO NOT EDIT!
 *
 * To make changes, edit Java.Interop/build-tools/jnienv-gen and rerun
 */

#include "java_interop_api.h"

JI_API jint
java_interop_jnienv_get_version (JNIEnv *env)
{
	jint _r_ = (*env)->GetVersion (env);
	return _r_;
}

JI_API jclass
java_interop_jnienv_define_class (JNIEnv *env, jthrowable *_thrown, const char* name, jobject loader, const jbyte* buffer, jsize bufferLength)
{
	*_thrown = 0;
	jclass _r_ = (*env)->DefineClass (env, name, loader, buffer, bufferLength);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jclass
java_interop_jnienv_find_class (JNIEnv *env, jthrowable *_thrown, const char* classname)
{
	*_thrown = 0;
	jclass _r_ = (*env)->FindClass (env, classname);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_to_reflected_method (JNIEnv *env, jthrowable *_thrown, jclass type, jmethodID method, jboolean isStatic)
{
	*_thrown = 0;
	jobject _r_ = (*env)->ToReflectedMethod (env, type, method, isStatic);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jclass
java_interop_jnienv_get_superclass (JNIEnv *env, jclass type)
{
	jclass _r_ = (*env)->GetSuperclass (env, type);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_is_assignable_from (JNIEnv *env, jclass class1, jclass class2)
{
	jboolean _r_ = (*env)->IsAssignableFrom (env, class1, class2);
	return _r_;
}

JI_API jobject
java_interop_jnienv_to_reflected_field (JNIEnv *env, jthrowable *_thrown, jclass type, jfieldID field, jboolean isStatic)
{
	*_thrown = 0;
	jobject _r_ = (*env)->ToReflectedField (env, type, field, isStatic);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_throw (JNIEnv *env, jthrowable toThrow)
{
	jint _r_ = (*env)->Throw (env, toThrow);
	return _r_;
}

JI_API jint
java_interop_jnienv_throw_new (JNIEnv *env, jclass type, const char* message)
{
	jint _r_ = (*env)->ThrowNew (env, type, message);
	return _r_;
}

JI_API jthrowable
java_interop_jnienv_exception_occurred (JNIEnv *env)
{
	jthrowable _r_ = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API void
java_interop_jnienv_exception_describe (JNIEnv *env)
{
	(*env)->ExceptionDescribe (env);
}

JI_API void
java_interop_jnienv_exception_clear (JNIEnv *env)
{
	(*env)->ExceptionClear (env);
}

JI_API void
java_interop_jnienv_fatal_error (JNIEnv *env, const char* message)
{
	(*env)->FatalError (env, message);
}

JI_API jint
java_interop_jnienv_push_local_frame (JNIEnv *env, jint capacity)
{
	jint _r_ = (*env)->PushLocalFrame (env, capacity);
	return _r_;
}

JI_API jobject
java_interop_jnienv_pop_local_frame (JNIEnv *env, jobject result)
{
	jobject _r_ = (*env)->PopLocalFrame (env, result);
	return _r_;
}

JI_API jglobal
java_interop_jnienv_new_global_ref (JNIEnv *env, jobject instance)
{
	jglobal _r_ = (*env)->NewGlobalRef (env, instance);
	return _r_;
}

JI_API void
java_interop_jnienv_delete_global_ref (JNIEnv *env, jobject instance)
{
	(*env)->DeleteGlobalRef (env, instance);
}

JI_API void
java_interop_jnienv_delete_local_ref (JNIEnv *env, jobject instance)
{
	(*env)->DeleteLocalRef (env, instance);
}

JI_API jboolean
java_interop_jnienv_is_same_object (JNIEnv *env, jobject object1, jobject object2)
{
	jboolean _r_ = (*env)->IsSameObject (env, object1, object2);
	return _r_;
}

JI_API jobject
java_interop_jnienv_new_local_ref (JNIEnv *env, jobject instance)
{
	jobject _r_ = (*env)->NewLocalRef (env, instance);
	return _r_;
}

JI_API jint
java_interop_jnienv_ensure_local_capacity (JNIEnv *env, jint capacity)
{
	jint _r_ = (*env)->EnsureLocalCapacity (env, capacity);
	return _r_;
}

JI_API jobject
java_interop_jnienv_alloc_object (JNIEnv *env, jthrowable *_thrown, jclass type)
{
	*_thrown = 0;
	jobject _r_ = (*env)->AllocObject (env, type);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_new_object (JNIEnv *env, jthrowable *_thrown, jclass type, jmethodID method)
{
	*_thrown = 0;
	jobject _r_ = (*env)->NewObject (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_new_object_a (JNIEnv *env, jthrowable *_thrown, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jobject _r_ = (*env)->NewObjectA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jclass
java_interop_jnienv_get_object_class (JNIEnv *env, jobject instance)
{
	jclass _r_ = (*env)->GetObjectClass (env, instance);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_is_instance_of (JNIEnv *env, jobject instance, jclass type)
{
	jboolean _r_ = (*env)->IsInstanceOf (env, instance, type);
	return _r_;
}

JI_API jmethodID
java_interop_jnienv_get_method_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature)
{
	*_thrown = 0;
	jmethodID _r_ = (*env)->GetMethodID (env, type, name, signature);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_call_object_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jobject _r_ = (*env)->CallObjectMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_call_object_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jobject _r_ = (*env)->CallObjectMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_call_boolean_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jboolean _r_ = (*env)->CallBooleanMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_call_boolean_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jboolean _r_ = (*env)->CallBooleanMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_call_byte_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jbyte _r_ = (*env)->CallByteMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_call_byte_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jbyte _r_ = (*env)->CallByteMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jchar
java_interop_jnienv_call_char_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jchar _r_ = (*env)->CallCharMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jchar
java_interop_jnienv_call_char_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jchar _r_ = (*env)->CallCharMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jshort
java_interop_jnienv_call_short_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jshort _r_ = (*env)->CallShortMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jshort
java_interop_jnienv_call_short_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jshort _r_ = (*env)->CallShortMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_call_int_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jint _r_ = (*env)->CallIntMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_call_int_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jint _r_ = (*env)->CallIntMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jlong
java_interop_jnienv_call_long_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jlong _r_ = (*env)->CallLongMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jlong
java_interop_jnienv_call_long_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jlong _r_ = (*env)->CallLongMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_call_float_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jfloat _r_ = (*env)->CallFloatMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_call_float_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jfloat _r_ = (*env)->CallFloatMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_call_double_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	jdouble _r_ = (*env)->CallDoubleMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_call_double_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jdouble _r_ = (*env)->CallDoubleMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API void
java_interop_jnienv_call_void_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method)
{
	*_thrown = 0;
	(*env)->CallVoidMethod (env, instance, method);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_call_void_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	(*env)->CallVoidMethodA (env, instance, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API jobject
java_interop_jnienv_call_nonvirtual_object_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jobject _r_ = (*env)->CallNonvirtualObjectMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_call_nonvirtual_object_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jobject _r_ = (*env)->CallNonvirtualObjectMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_call_nonvirtual_boolean_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jboolean _r_ = (*env)->CallNonvirtualBooleanMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_call_nonvirtual_boolean_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jboolean _r_ = (*env)->CallNonvirtualBooleanMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_call_nonvirtual_byte_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jbyte _r_ = (*env)->CallNonvirtualByteMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_call_nonvirtual_byte_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jbyte _r_ = (*env)->CallNonvirtualByteMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jchar
java_interop_jnienv_call_nonvirtual_char_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jchar _r_ = (*env)->CallNonvirtualCharMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jchar
java_interop_jnienv_call_nonvirtual_char_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jchar _r_ = (*env)->CallNonvirtualCharMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jshort
java_interop_jnienv_call_nonvirtual_short_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jshort _r_ = (*env)->CallNonvirtualShortMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jshort
java_interop_jnienv_call_nonvirtual_short_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jshort _r_ = (*env)->CallNonvirtualShortMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_call_nonvirtual_int_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jint _r_ = (*env)->CallNonvirtualIntMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_call_nonvirtual_int_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jint _r_ = (*env)->CallNonvirtualIntMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jlong
java_interop_jnienv_call_nonvirtual_long_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jlong _r_ = (*env)->CallNonvirtualLongMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jlong
java_interop_jnienv_call_nonvirtual_long_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jlong _r_ = (*env)->CallNonvirtualLongMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_call_nonvirtual_float_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jfloat _r_ = (*env)->CallNonvirtualFloatMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_call_nonvirtual_float_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jfloat _r_ = (*env)->CallNonvirtualFloatMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_call_nonvirtual_double_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	jdouble _r_ = (*env)->CallNonvirtualDoubleMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_call_nonvirtual_double_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	jdouble _r_ = (*env)->CallNonvirtualDoubleMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API void
java_interop_jnienv_call_nonvirtual_void_method (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method)
{
	*_thrown = 0;
	(*env)->CallNonvirtualVoidMethod (env, instance, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_call_nonvirtual_void_method_a (JNIEnv *env, jthrowable *_thrown, jobject instance, jclass type, jmethodID method, jvalue* args)
{
	*_thrown = 0;
	(*env)->CallNonvirtualVoidMethodA (env, instance, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API jfieldID
java_interop_jnienv_get_field_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature)
{
	*_thrown = 0;
	jfieldID _r_ = (*env)->GetFieldID (env, type, name, signature);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_get_object_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jobject _r_ = (*env)->GetObjectField (env, instance, field);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_get_boolean_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jboolean _r_ = (*env)->GetBooleanField (env, instance, field);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_get_byte_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jbyte _r_ = (*env)->GetByteField (env, instance, field);
	return _r_;
}

JI_API jchar
java_interop_jnienv_get_char_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jchar _r_ = (*env)->GetCharField (env, instance, field);
	return _r_;
}

JI_API jshort
java_interop_jnienv_get_short_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jshort _r_ = (*env)->GetShortField (env, instance, field);
	return _r_;
}

JI_API jint
java_interop_jnienv_get_int_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jint _r_ = (*env)->GetIntField (env, instance, field);
	return _r_;
}

JI_API jlong
java_interop_jnienv_get_long_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jlong _r_ = (*env)->GetLongField (env, instance, field);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_get_float_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jfloat _r_ = (*env)->GetFloatField (env, instance, field);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_get_double_field (JNIEnv *env, jobject instance, jfieldID field)
{
	jdouble _r_ = (*env)->GetDoubleField (env, instance, field);
	return _r_;
}

JI_API void
java_interop_jnienv_set_object_field (JNIEnv *env, jobject instance, jfieldID field, jobject value)
{
	(*env)->SetObjectField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_boolean_field (JNIEnv *env, jobject instance, jfieldID field, jboolean value)
{
	(*env)->SetBooleanField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_byte_field (JNIEnv *env, jobject instance, jfieldID field, jbyte value)
{
	(*env)->SetByteField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_char_field (JNIEnv *env, jobject instance, jfieldID field, jchar value)
{
	(*env)->SetCharField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_short_field (JNIEnv *env, jobject instance, jfieldID field, jshort value)
{
	(*env)->SetShortField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_int_field (JNIEnv *env, jobject instance, jfieldID field, jint value)
{
	(*env)->SetIntField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_long_field (JNIEnv *env, jobject instance, jfieldID field, jlong value)
{
	(*env)->SetLongField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_float_field (JNIEnv *env, jobject instance, jfieldID field, jfloat value)
{
	(*env)->SetFloatField (env, instance, field, value);
}

JI_API void
java_interop_jnienv_set_double_field (JNIEnv *env, jobject instance, jfieldID field, jdouble value)
{
	(*env)->SetDoubleField (env, instance, field, value);
}

JI_API jstaticmethodID
java_interop_jnienv_get_static_method_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature)
{
	*_thrown = 0;
	jstaticmethodID _r_ = (*env)->GetStaticMethodID (env, type, name, signature);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_call_static_object_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jobject _r_ = (*env)->CallStaticObjectMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_call_static_object_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jobject _r_ = (*env)->CallStaticObjectMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_call_static_boolean_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jboolean _r_ = (*env)->CallStaticBooleanMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_call_static_boolean_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jboolean _r_ = (*env)->CallStaticBooleanMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_call_static_byte_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jbyte _r_ = (*env)->CallStaticByteMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_call_static_byte_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jbyte _r_ = (*env)->CallStaticByteMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jchar
java_interop_jnienv_call_static_char_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jchar _r_ = (*env)->CallStaticCharMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jchar
java_interop_jnienv_call_static_char_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jchar _r_ = (*env)->CallStaticCharMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jshort
java_interop_jnienv_call_static_short_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jshort _r_ = (*env)->CallStaticShortMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jshort
java_interop_jnienv_call_static_short_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jshort _r_ = (*env)->CallStaticShortMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_call_static_int_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jint _r_ = (*env)->CallStaticIntMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_call_static_int_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jint _r_ = (*env)->CallStaticIntMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jlong
java_interop_jnienv_call_static_long_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jlong _r_ = (*env)->CallStaticLongMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jlong
java_interop_jnienv_call_static_long_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jlong _r_ = (*env)->CallStaticLongMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_call_static_float_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jfloat _r_ = (*env)->CallStaticFloatMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_call_static_float_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jfloat _r_ = (*env)->CallStaticFloatMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_call_static_double_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	jdouble _r_ = (*env)->CallStaticDoubleMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_call_static_double_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	jdouble _r_ = (*env)->CallStaticDoubleMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API void
java_interop_jnienv_call_static_void_method (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method)
{
	*_thrown = 0;
	(*env)->CallStaticVoidMethod (env, type, method);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_call_static_void_method_a (JNIEnv *env, jthrowable *_thrown, jclass type, jstaticmethodID method, jvalue* args)
{
	*_thrown = 0;
	(*env)->CallStaticVoidMethodA (env, type, method, args);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API jstaticfieldID
java_interop_jnienv_get_static_field_id (JNIEnv *env, jthrowable *_thrown, jclass type, const char* name, const char* signature)
{
	*_thrown = 0;
	jstaticfieldID _r_ = (*env)->GetStaticFieldID (env, type, name, signature);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_get_static_object_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jobject _r_ = (*env)->GetStaticObjectField (env, type, field);
	return _r_;
}

JI_API jboolean
java_interop_jnienv_get_static_boolean_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jboolean _r_ = (*env)->GetStaticBooleanField (env, type, field);
	return _r_;
}

JI_API jbyte
java_interop_jnienv_get_static_byte_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jbyte _r_ = (*env)->GetStaticByteField (env, type, field);
	return _r_;
}

JI_API jchar
java_interop_jnienv_get_static_char_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jchar _r_ = (*env)->GetStaticCharField (env, type, field);
	return _r_;
}

JI_API jshort
java_interop_jnienv_get_static_short_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jshort _r_ = (*env)->GetStaticShortField (env, type, field);
	return _r_;
}

JI_API jint
java_interop_jnienv_get_static_int_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jint _r_ = (*env)->GetStaticIntField (env, type, field);
	return _r_;
}

JI_API jlong
java_interop_jnienv_get_static_long_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jlong _r_ = (*env)->GetStaticLongField (env, type, field);
	return _r_;
}

JI_API jfloat
java_interop_jnienv_get_static_float_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jfloat _r_ = (*env)->GetStaticFloatField (env, type, field);
	return _r_;
}

JI_API jdouble
java_interop_jnienv_get_static_double_field (JNIEnv *env, jclass type, jstaticfieldID field)
{
	jdouble _r_ = (*env)->GetStaticDoubleField (env, type, field);
	return _r_;
}

JI_API void
java_interop_jnienv_set_static_object_field (JNIEnv *env, jclass type, jstaticfieldID field, jobject value)
{
	(*env)->SetStaticObjectField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_boolean_field (JNIEnv *env, jclass type, jstaticfieldID field, jboolean value)
{
	(*env)->SetStaticBooleanField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_byte_field (JNIEnv *env, jclass type, jstaticfieldID field, jbyte value)
{
	(*env)->SetStaticByteField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_char_field (JNIEnv *env, jclass type, jstaticfieldID field, jchar value)
{
	(*env)->SetStaticCharField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_short_field (JNIEnv *env, jclass type, jstaticfieldID field, jshort value)
{
	(*env)->SetStaticShortField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_int_field (JNIEnv *env, jclass type, jstaticfieldID field, jint value)
{
	(*env)->SetStaticIntField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_long_field (JNIEnv *env, jclass type, jstaticfieldID field, jlong value)
{
	(*env)->SetStaticLongField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_float_field (JNIEnv *env, jclass type, jstaticfieldID field, jfloat value)
{
	(*env)->SetStaticFloatField (env, type, field, value);
}

JI_API void
java_interop_jnienv_set_static_double_field (JNIEnv *env, jclass type, jstaticfieldID field, jdouble value)
{
	(*env)->SetStaticDoubleField (env, type, field, value);
}

JI_API jstring
java_interop_jnienv_new_string (JNIEnv *env, jthrowable *_thrown, jchar* unicodeChars, jsize length)
{
	*_thrown = 0;
	jstring _r_ = (*env)->NewString (env, unicodeChars, length);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jsize
java_interop_jnienv_get_string_length (JNIEnv *env, jstring stringInstance)
{
	jsize _r_ = (*env)->GetStringLength (env, stringInstance);
	return _r_;
}

JI_API const jchar*
java_interop_jnienv_get_string_chars (JNIEnv *env, jstring stringInstance, jboolean* isCopy)
{
	const jchar* _r_ = (*env)->GetStringChars (env, stringInstance, isCopy);
	return _r_;
}

JI_API void
java_interop_jnienv_release_string_chars (JNIEnv *env, jstring stringInstance, jchar* chars)
{
	(*env)->ReleaseStringChars (env, stringInstance, chars);
}

JI_API jsize
java_interop_jnienv_get_array_length (JNIEnv *env, jarray array)
{
	jsize _r_ = (*env)->GetArrayLength (env, array);
	return _r_;
}

JI_API jobjectArray
java_interop_jnienv_new_object_array (JNIEnv *env, jthrowable *_thrown, jsize length, jclass elementClass, jobject initialElement)
{
	*_thrown = 0;
	jobjectArray _r_ = (*env)->NewObjectArray (env, length, elementClass, initialElement);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_get_object_array_element (JNIEnv *env, jthrowable *_thrown, jobjectArray array, jsize index)
{
	*_thrown = 0;
	jobject _r_ = (*env)->GetObjectArrayElement (env, array, index);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API void
java_interop_jnienv_set_object_array_element (JNIEnv *env, jthrowable *_thrown, jobjectArray array, jsize index, jobject value)
{
	*_thrown = 0;
	(*env)->SetObjectArrayElement (env, array, index, value);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API jbooleanArray
java_interop_jnienv_new_boolean_array (JNIEnv *env, jsize length)
{
	jbooleanArray _r_ = (*env)->NewBooleanArray (env, length);
	return _r_;
}

JI_API jbyteArray
java_interop_jnienv_new_byte_array (JNIEnv *env, jsize length)
{
	jbyteArray _r_ = (*env)->NewByteArray (env, length);
	return _r_;
}

JI_API jcharArray
java_interop_jnienv_new_char_array (JNIEnv *env, jsize length)
{
	jcharArray _r_ = (*env)->NewCharArray (env, length);
	return _r_;
}

JI_API jshortArray
java_interop_jnienv_new_short_array (JNIEnv *env, jsize length)
{
	jshortArray _r_ = (*env)->NewShortArray (env, length);
	return _r_;
}

JI_API jintArray
java_interop_jnienv_new_int_array (JNIEnv *env, jsize length)
{
	jintArray _r_ = (*env)->NewIntArray (env, length);
	return _r_;
}

JI_API jlongArray
java_interop_jnienv_new_long_array (JNIEnv *env, jsize length)
{
	jlongArray _r_ = (*env)->NewLongArray (env, length);
	return _r_;
}

JI_API jfloatArray
java_interop_jnienv_new_float_array (JNIEnv *env, jsize length)
{
	jfloatArray _r_ = (*env)->NewFloatArray (env, length);
	return _r_;
}

JI_API jdoubleArray
java_interop_jnienv_new_double_array (JNIEnv *env, jsize length)
{
	jdoubleArray _r_ = (*env)->NewDoubleArray (env, length);
	return _r_;
}

JI_API jboolean*
java_interop_jnienv_get_boolean_array_elements (JNIEnv *env, jbooleanArray array, jboolean* isCopy)
{
	jboolean* _r_ = (*env)->GetBooleanArrayElements (env, array, isCopy);
	return _r_;
}

JI_API jbyte*
java_interop_jnienv_get_byte_array_elements (JNIEnv *env, jbyteArray array, jboolean* isCopy)
{
	jbyte* _r_ = (*env)->GetByteArrayElements (env, array, isCopy);
	return _r_;
}

JI_API jchar*
java_interop_jnienv_get_char_array_elements (JNIEnv *env, jcharArray array, jboolean* isCopy)
{
	jchar* _r_ = (*env)->GetCharArrayElements (env, array, isCopy);
	return _r_;
}

JI_API jshort*
java_interop_jnienv_get_short_array_elements (JNIEnv *env, jshortArray array, jboolean* isCopy)
{
	jshort* _r_ = (*env)->GetShortArrayElements (env, array, isCopy);
	return _r_;
}

JI_API jint*
java_interop_jnienv_get_int_array_elements (JNIEnv *env, jintArray array, jboolean* isCopy)
{
	jint* _r_ = (*env)->GetIntArrayElements (env, array, isCopy);
	return _r_;
}

JI_API jlong*
java_interop_jnienv_get_long_array_elements (JNIEnv *env, jlongArray array, jboolean* isCopy)
{
	jlong* _r_ = (*env)->GetLongArrayElements (env, array, isCopy);
	return _r_;
}

JI_API jfloat*
java_interop_jnienv_get_float_array_elements (JNIEnv *env, jfloatArray array, jboolean* isCopy)
{
	jfloat* _r_ = (*env)->GetFloatArrayElements (env, array, isCopy);
	return _r_;
}

JI_API jdouble*
java_interop_jnienv_get_double_array_elements (JNIEnv *env, jdoubleArray array, jboolean* isCopy)
{
	jdouble* _r_ = (*env)->GetDoubleArrayElements (env, array, isCopy);
	return _r_;
}

JI_API void
java_interop_jnienv_release_boolean_array_elements (JNIEnv *env, jbooleanArray array, jboolean* elements, jint mode)
{
	(*env)->ReleaseBooleanArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_release_byte_array_elements (JNIEnv *env, jbyteArray array, jbyte* elements, jint mode)
{
	(*env)->ReleaseByteArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_release_char_array_elements (JNIEnv *env, jcharArray array, jchar* elements, jint mode)
{
	(*env)->ReleaseCharArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_release_short_array_elements (JNIEnv *env, jshortArray array, jshort* elements, jint mode)
{
	(*env)->ReleaseShortArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_release_int_array_elements (JNIEnv *env, jintArray array, jint* elements, jint mode)
{
	(*env)->ReleaseIntArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_release_long_array_elements (JNIEnv *env, jlongArray array, jlong* elements, jint mode)
{
	(*env)->ReleaseLongArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_release_float_array_elements (JNIEnv *env, jfloatArray array, jfloat* elements, jint mode)
{
	(*env)->ReleaseFloatArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_release_double_array_elements (JNIEnv *env, jdoubleArray array, jdouble* elements, jint mode)
{
	(*env)->ReleaseDoubleArrayElements (env, array, elements, mode);
}

JI_API void
java_interop_jnienv_get_boolean_array_region (JNIEnv *env, jthrowable *_thrown, jbooleanArray array, jsize start, jsize length, jboolean* buffer)
{
	*_thrown = 0;
	(*env)->GetBooleanArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_get_byte_array_region (JNIEnv *env, jthrowable *_thrown, jbyteArray array, jsize start, jsize length, jbyte* buffer)
{
	*_thrown = 0;
	(*env)->GetByteArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_get_char_array_region (JNIEnv *env, jthrowable *_thrown, jcharArray array, jsize start, jsize length, jchar* buffer)
{
	*_thrown = 0;
	(*env)->GetCharArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_get_short_array_region (JNIEnv *env, jthrowable *_thrown, jshortArray array, jsize start, jsize length, jshort* buffer)
{
	*_thrown = 0;
	(*env)->GetShortArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_get_int_array_region (JNIEnv *env, jthrowable *_thrown, jintArray array, jsize start, jsize length, jint* buffer)
{
	*_thrown = 0;
	(*env)->GetIntArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_get_long_array_region (JNIEnv *env, jthrowable *_thrown, jlongArray array, jsize start, jsize length, jlong* buffer)
{
	*_thrown = 0;
	(*env)->GetLongArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_get_float_array_region (JNIEnv *env, jthrowable *_thrown, jlongArray array, jsize start, jsize length, jfloat* buffer)
{
	*_thrown = 0;
	(*env)->GetFloatArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_get_double_array_region (JNIEnv *env, jthrowable *_thrown, jdoubleArray array, jsize start, jsize length, jdouble* buffer)
{
	*_thrown = 0;
	(*env)->GetDoubleArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_boolean_array_region (JNIEnv *env, jthrowable *_thrown, jbooleanArray array, jsize start, jsize length, jboolean* buffer)
{
	*_thrown = 0;
	(*env)->SetBooleanArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_byte_array_region (JNIEnv *env, jthrowable *_thrown, jbyteArray array, jsize start, jsize length, jbyte* buffer)
{
	*_thrown = 0;
	(*env)->SetByteArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_char_array_region (JNIEnv *env, jthrowable *_thrown, jcharArray array, jsize start, jsize length, const jchar* buffer)
{
	*_thrown = 0;
	(*env)->SetCharArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_short_array_region (JNIEnv *env, jthrowable *_thrown, jshortArray array, jsize start, jsize length, jshort* buffer)
{
	*_thrown = 0;
	(*env)->SetShortArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_int_array_region (JNIEnv *env, jthrowable *_thrown, jintArray array, jsize start, jsize length, jint* buffer)
{
	*_thrown = 0;
	(*env)->SetIntArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_long_array_region (JNIEnv *env, jthrowable *_thrown, jlongArray array, jsize start, jsize length, jlong* buffer)
{
	*_thrown = 0;
	(*env)->SetLongArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_float_array_region (JNIEnv *env, jthrowable *_thrown, jfloatArray array, jsize start, jsize length, jfloat* buffer)
{
	*_thrown = 0;
	(*env)->SetFloatArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API void
java_interop_jnienv_set_double_array_region (JNIEnv *env, jthrowable *_thrown, jdoubleArray array, jsize start, jsize length, jdouble* buffer)
{
	*_thrown = 0;
	(*env)->SetDoubleArrayRegion (env, array, start, length, buffer);
	*_thrown = (*env)->ExceptionOccurred (env);
}

JI_API jint
java_interop_jnienv_register_natives (JNIEnv *env, jthrowable *_thrown, jclass type, const JNINativeMethod* methods, jint numMethods)
{
	*_thrown = 0;
	jint _r_ = (*env)->RegisterNatives (env, type, methods, numMethods);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API jint
java_interop_jnienv_unregister_natives (JNIEnv *env, jclass type)
{
	jint _r_ = (*env)->UnregisterNatives (env, type);
	return _r_;
}

JI_API jint
java_interop_jnienv_monitor_enter (JNIEnv *env, jobject instance)
{
	jint _r_ = (*env)->MonitorEnter (env, instance);
	return _r_;
}

JI_API jint
java_interop_jnienv_monitor_exit (JNIEnv *env, jobject instance)
{
	jint _r_ = (*env)->MonitorExit (env, instance);
	return _r_;
}

JI_API jint
java_interop_jnienv_get_java_vm (JNIEnv *env, JavaVM** vm)
{
	jint _r_ = (*env)->GetJavaVM (env, vm);
	return _r_;
}

JI_API void*
java_interop_jnienv_get_primitive_array_critical (JNIEnv *env, jarray array, jboolean* isCopy)
{
	void* _r_ = (*env)->GetPrimitiveArrayCritical (env, array, isCopy);
	return _r_;
}

JI_API void
java_interop_jnienv_release_primitive_array_critical (JNIEnv *env, jarray array, void* carray, jint mode)
{
	(*env)->ReleasePrimitiveArrayCritical (env, array, carray, mode);
}

JI_API jweak
java_interop_jnienv_new_weak_global_ref (JNIEnv *env, jobject instance)
{
	jweak _r_ = (*env)->NewWeakGlobalRef (env, instance);
	return _r_;
}

JI_API void
java_interop_jnienv_delete_weak_global_ref (JNIEnv *env, jobject instance)
{
	(*env)->DeleteWeakGlobalRef (env, instance);
}

JI_API jboolean
java_interop_jnienv_exception_check (JNIEnv *env)
{
	jboolean _r_ = (*env)->ExceptionCheck (env);
	return _r_;
}

JI_API jobject
java_interop_jnienv_new_direct_byte_buffer (JNIEnv *env, jthrowable *_thrown, void* address, jlong capacity)
{
	*_thrown = 0;
	jobject _r_ = (*env)->NewDirectByteBuffer (env, address, capacity);
	*_thrown = (*env)->ExceptionOccurred (env);
	return _r_;
}

JI_API void*
java_interop_jnienv_get_direct_buffer_address (JNIEnv *env, jobject buffer)
{
	void* _r_ = (*env)->GetDirectBufferAddress (env, buffer);
	return _r_;
}

JI_API jlong
java_interop_jnienv_get_direct_buffer_capacity (JNIEnv *env, jobject buffer)
{
	jlong _r_ = (*env)->GetDirectBufferCapacity (env, buffer);
	return _r_;
}

JI_API jobjectRefType
java_interop_jnienv_get_object_ref_type (JNIEnv *env, jobject instance)
{
	jobjectRefType _r_ = (*env)->GetObjectRefType (env, instance);
	return _r_;
}
