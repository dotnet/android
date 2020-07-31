package example;

import java.util.List;
import java.util.Map;


/**
 * Yay, Javadoc!
 * 
 * JNI sig: Lexample/Outer;
 */

 @Outer.MyAnnotation(keys={"a", "b", "c"})
public class Outer<T extends Object & Runnable, U extends Error> {

	/**
	 * <init>(java.lang.Object value)
	 * 
	 * JNI sig: (Ljava/lang/Object;)V
	 */

	public Outer(T value) {
		value.run();
	}

	/**
	 * isU(java.util.List<? super U> list)
	 * 
	 * <p>This is a paragraph.  Yay?</p>
	 * 
	 * JNI sig: (Ljava/util/List;)Ljava/lang/Error;
	 * 
	 * @param list just some random items
	 * @return some value
	 */
	
	public U isU(List<? super U> list) {
		return null;
	}

	/**
	 * Just an example annotation, for use later…
	 *
	 * JNI sig: Lexample/Outer$MyAnnotation;
	 */

	@java.lang.annotation.Target({java.lang.annotation.ElementType.TYPE})
	@java.lang.annotation.Retention(java.lang.annotation.RetentionPolicy.RUNTIME)
	public static @interface MyAnnotation {

		/**
		 * JNI sig: ()[Ljava/lang/String;
		 *
		 * @return some random keys
		 */

		 String[] keys() default {};
	}

	/**
	 * JNI sig: Lexample/Outer$Inner;
	 */
	public static interface Inner<V extends Readable> {
		/**
		 * m(U value)
		 * 
		 * JNI sig: ([[Ljava/lang/Readable;)V
		 * 
		 * @throws Throwable never, just because
		 */
		public default void m(V[][] values) throws Throwable {
			for (V[] vs : values) {
				for (V v : vs) {
					v.read(null);
				}
			}
		}

		/**
		 * JNI sig: J
		 */
		public static final long COUNT = 42;

		/**
		 * JNI sig: Lexample/Outer$Inner$NestedInner;
		 */
		public static class NestedInner<T extends Number> {

			/**
			 * JNI sig: S
			 */
			public static final short  S = 64;

			/**
			 * map(java.util.Map<T, String> map)
			 * 
			 * JNI sig: map(Ljava/util/Map;)V
			 * @param map
			 */
			public void map(Map<T, String> m) {
			}
		}
	}

	/**
	 * method(java.lang.CharSequence a, short[] b, T[] values)
	 * 
	 * JNI sig: (Ljava/lang/CharSequence;[S[Ljava/lang/Appendable;)Ljava/lang/Appendable;
	 */
	public <T extends Appendable> T method(CharSequence a, short[] b, T[] values) {
		return null;
	}

	/**
	 * main(java.lang.String[] args)
	 * 
	 * JNI sig: ([Ljava/lang/String;)V
	 */
	public static void main(String[] args) {
	}
}
