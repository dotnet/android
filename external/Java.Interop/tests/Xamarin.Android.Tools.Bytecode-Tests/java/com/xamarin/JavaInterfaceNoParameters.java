package com.xamarin;

public interface JavaInterfaceNoParameters {
	/**
	 * JNI sig: ([Ljava/lang/Object;)Ljava/util/List;
	 */
	<T> java.util.List<T> asList(T... a);

	/**
	 * JNI sig: ([Ljava/lang/Object;IILjava/lang/Object;)I
	 *
	 * @param a [Ljava/lang/Object;
	 * @param fromIndex int
	 * @param toIndex int
	 * @param key Ljava/lang/Object
	 * @return int
	 */
	int binarySearch(Object[] a, int fromIndex, int toIndex, Object key);
}
