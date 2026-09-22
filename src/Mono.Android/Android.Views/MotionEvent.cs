namespace Android.Views {

	partial class MotionEvent {

#if ANDROID_10
		public static MotionEvent? Obtain (long downTime, long eventTime, int action, int pointers, int[] pointerIds, MotionEvent.PointerCoords[] pointerCoords, MetaKeyStates metaState, float xPrecision, float yPrecision, int deviceId, Edge edgeFlags, int source, int flags)
		{
#pragma warning disable CS0618 // Preserve the legacy overload with integer enum parameters.
			return Obtain (downTime, eventTime, (MotionEventActions) action, pointers, pointerIds, pointerCoords, metaState, xPrecision, yPrecision, deviceId, edgeFlags, (InputSourceType) source, (MotionEventFlags) flags);
#pragma warning restore CS0618
		}
#endif

#if ANDROID_7
		public static MotionEvent? Obtain (long downTime, long eventTime, int action, int pointers, float x, float y, float pressure, float size, MetaKeyStates metaState, float xPrecision, float yPrecision, int deviceId, Edge edgeFlags)
		{
#pragma warning disable CS0618 // Preserve the legacy overload with an integer action parameter.
			return Obtain (downTime, eventTime, (MotionEventActions) action, pointers, x, y, pressure, size, metaState, xPrecision, yPrecision, deviceId, edgeFlags);
#pragma warning restore CS0618
		}
#endif

		// API 4
		public static MotionEvent? Obtain (long downTime, long eventTime, int action, float x, float y, MetaKeyStates metaState)
		{
			return Obtain (downTime, eventTime, (MotionEventActions) action, x, y, metaState);
		}

		// API 4
		public static MotionEvent? Obtain (long downTime, long eventTime, int action, float x, float y, float pressure, float size, MetaKeyStates metaState, float xPrecision, float yPrecision, int deviceId, Edge edgeFlags)
		{
			return Obtain (downTime, eventTime, (MotionEventActions) action, x, y, pressure, size, metaState, xPrecision, yPrecision, deviceId, edgeFlags);
		}
	}
}
