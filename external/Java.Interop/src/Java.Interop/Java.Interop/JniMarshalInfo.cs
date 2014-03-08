using System;

namespace Java.Interop {

	using GetValueFromJniHandler            = Func<JniReferenceSafeHandle /* handle */, JniHandleOwnership /* transfer */, Type /* targetType */, object>;
	using CreateJValueHandler               = Func<object /* value */, JValue>;
	using CreateLocalRefHandler             = Func<object /* value */, JniLocalReference>;
	using CreateMarshalCollectionHandler    = Func<object /* value */, IJavaObject>;
	using CleanupMarshalCollectionHandler   = Action<IJavaObject /* sourceValue */, object /* destValue */>;

	public struct JniMarshalInfo {

		public  GetValueFromJniHandler              GetValueFromJni;
		public  CreateJValueHandler                 CreateJValue;
		public  CreateLocalRefHandler               CreateLocalRef;
		public  CreateMarshalCollectionHandler      CreateMarshalCollection;
		public  CleanupMarshalCollectionHandler     CleanupMarshalCollection;
	}
}

