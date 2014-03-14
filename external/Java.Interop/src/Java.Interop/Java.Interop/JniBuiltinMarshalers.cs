using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Java.Interop {

	partial class JavaVM {

		static readonly KeyValuePair<Type, JniMarshalInfo>[] JniBuiltinMarshalers = new []{
			new KeyValuePair<Type, JniMarshalInfo>(typeof (string), new JniMarshalInfo {
				GetValueFromJni             = JniEnvironment.Strings.ToString,
				CreateLocalRef              = JniEnvironment.Strings.NewString,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Boolean), new JniMarshalInfo {
				CreateJValue                = JniBoolean.CreateJValue,
				GetValueFromJni             = JniBoolean.GetValueFromJni,
				CreateLocalRef              = JniBoolean.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Char), new JniMarshalInfo {
				CreateJValue                = JniCharacter.CreateJValue,
				GetValueFromJni             = JniCharacter.GetValueFromJni,
				CreateLocalRef              = JniCharacter.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int16), new JniMarshalInfo {
				CreateJValue                = JniShort.CreateJValue,
				GetValueFromJni             = JniShort.GetValueFromJni,
				CreateLocalRef              = JniShort.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int32), new JniMarshalInfo {
				CreateJValue                = JniInteger.CreateJValue,
				GetValueFromJni             = JniInteger.GetValueFromJni,
				CreateLocalRef              = JniInteger.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int64), new JniMarshalInfo {
				CreateJValue                = JniLong.CreateJValue,
				GetValueFromJni             = JniLong.GetValueFromJni,
				CreateLocalRef              = JniLong.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Single), new JniMarshalInfo {
				CreateJValue                = JniFloat.CreateJValue,
				GetValueFromJni             = JniFloat.GetValueFromJni,
				CreateLocalRef              = JniFloat.CreateLocalRef,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Double), new JniMarshalInfo {
				CreateJValue                = JniDouble.CreateJValue,
				GetValueFromJni             = JniDouble.GetValueFromJni,
				CreateLocalRef              = JniDouble.CreateLocalRef,
			}),
		};
	}

	static class JniBoolean {
		internal    const   string  JniTypeName = "java/lang/Boolean";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Boolean) value);
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Boolean);
			TypeRef.GetCachedConstructor (ref init, "(Z)V");
			return TypeRef.NewObject (init, new JValue ((Boolean) value));
		}

		static JniInstanceMethodID booleanValue;
		internal static object GetValueFromJni (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (Boolean));
			TypeRef.GetCachedInstanceMethod (ref booleanValue, "booleanValue", "()Z");
			try {
				return booleanValue.CallVirtualBooleanMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}

	static class JniCharacter {
		internal    const   string  JniTypeName = "java/lang/Character";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Char) value);
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Char);
			TypeRef.GetCachedConstructor (ref init, "(C)V");
			return TypeRef.NewObject (init, new JValue ((Char) value));
		}

		static JniInstanceMethodID charValue;
		internal static object GetValueFromJni (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (Char));
			TypeRef.GetCachedInstanceMethod (ref charValue, "charValue", "()C");
			try {
				return charValue.CallVirtualCharMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}

	static class JniShort {
		internal    const   string  JniTypeName = "java/lang/Short";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Int16) value);
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int16);
			TypeRef.GetCachedConstructor (ref init, "(S)V");
			return TypeRef.NewObject (init, new JValue ((Int16) value));
		}

		static JniInstanceMethodID shortValue;
		internal static object GetValueFromJni (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (Int16));
			TypeRef.GetCachedInstanceMethod (ref shortValue, "shortValue", "()S");
			try {
				return shortValue.CallVirtualInt16Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}

	static class JniInteger {
		internal    const   string  JniTypeName = "java/lang/Integer";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Int32) value);
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int32);
			TypeRef.GetCachedConstructor (ref init, "(I)V");
			return TypeRef.NewObject (init, new JValue ((Int32) value));
		}

		static JniInstanceMethodID intValue;
		internal static object GetValueFromJni (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (Int32));
			TypeRef.GetCachedInstanceMethod (ref intValue, "intValue", "()I");
			try {
				return intValue.CallVirtualInt32Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}

	static class JniLong {
		internal    const   string  JniTypeName = "java/lang/Long";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Int64) value);
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Int64);
			TypeRef.GetCachedConstructor (ref init, "(J)V");
			return TypeRef.NewObject (init, new JValue ((Int64) value));
		}

		static JniInstanceMethodID longValue;
		internal static object GetValueFromJni (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (Int64));
			TypeRef.GetCachedInstanceMethod (ref longValue, "longValue", "()J");
			try {
				return longValue.CallVirtualInt64Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}

	static class JniFloat {
		internal    const   string  JniTypeName = "java/lang/Float";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Single) value);
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Single);
			TypeRef.GetCachedConstructor (ref init, "(F)V");
			return TypeRef.NewObject (init, new JValue ((Single) value));
		}

		static JniInstanceMethodID floatValue;
		internal static object GetValueFromJni (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (Single));
			TypeRef.GetCachedInstanceMethod (ref floatValue, "floatValue", "()F");
			try {
				return floatValue.CallVirtualSingleMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}

	static class JniDouble {
		internal    const   string  JniTypeName = "java/lang/Double";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		internal static JValue CreateJValue (object value)
		{
			return new JValue ((Double) value);
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference CreateLocalRef (object value)
		{
			Debug.Assert (value is Double);
			TypeRef.GetCachedConstructor (ref init, "(D)V");
			return TypeRef.NewObject (init, new JValue ((Double) value));
		}

		static JniInstanceMethodID doubleValue;
		internal static object GetValueFromJni (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (Double));
			TypeRef.GetCachedInstanceMethod (ref doubleValue, "doubleValue", "()D");
			try {
				return doubleValue.CallVirtualDoubleMethod (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}
}
