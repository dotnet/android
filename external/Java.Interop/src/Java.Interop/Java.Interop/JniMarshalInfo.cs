using System;

namespace Java.Interop {

	using CreateJValueHandler               = Func<object /* value */, JValue>;
	using CreateLocalRefHandler             = Func<object /* value */, JniObjectReference>;
	using CreateMarshalCollectionHandler    = Func<object /* value */, IJavaPeerable>;
	using CleanupMarshalCollectionHandler   = Action<IJavaPeerable /* sourceValue */, object /* destValue */>;

	public  delegate    object  CreateValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType);

	public struct JniMarshalInfo {

		public  CreateValueFromJni                  GetValueFromJni;
		public  CreateJValueHandler                 CreateJValue;
		public  CreateLocalRefHandler               CreateLocalRef;
		public  CreateMarshalCollectionHandler      CreateMarshalCollection;
		public  CleanupMarshalCollectionHandler     CleanupMarshalCollection;
	}
}

