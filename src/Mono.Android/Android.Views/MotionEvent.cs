namespace Android.Views {

	partial class MotionEvent {

#if ANDROID_10
		public static MotionEvent Obtain (long downTime, long eventTime, int action, int pointers, int[] pointerIds, MotionEvent.PointerCoords[] pointerCoords, MetaKeyStates metaState, float xPrecision, float yPrecision, int deviceId, Edge edgeFlags, int source, int flags)
		{
			return Obtain (downTime, eventTime, (MotionEventActions) action, pointers, pointerIds, pointerCoords, metaState, xPrecision, yPrecision, deviceId, edgeFlags, (InputSourceType) source, (MotionEventFlags) flags);
		}
#endif

#if ANDROID_7
		public static MotionEvent Obtain (long downTime, long eventTime, int action, int pointers, float x, float y, float pressure, float size, MetaKeyStates metaState, float xPrecision, float yPrecision, int deviceId, Edge edgeFlags)
		{
			return Obtain (downTime, eventTime, (MotionEventActions) action, pointers, x, y, pressure, size, metaState, xPrecision, yPrecision, deviceId, edgeFlags);
		}
#endif

		// API 4
		public static MotionEvent Obtain (long downTime, long eventTime, int action, float x, float y, MetaKeyStates metaState)
		{
			return Obtain (downTime, eventTime, (MotionEventActions) action, x, y, metaState);
		}

		// API 4
		public static MotionEvent Obtain (long downTime, long eventTime, int action, float x, float y, float pressure, float size, MetaKeyStates metaState, float xPrecision, float yPrecision, int deviceId, Edge edgeFlags)
		{
			return Obtain (downTime, eventTime, (MotionEventActions) action, x, y, pressure, size, metaState, xPrecision, yPrecision, deviceId, edgeFlags);
		}
	}
}
