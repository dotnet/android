package net.dot.jni.test;

public class ObjectHelper {
	private ObjectHelper()
	{
	}

	public static int getHashCodeHelper (Object o) {
		return o.hashCode();
	}

	public static class StringLookupFixture {
		public int champ\u00c9;
		public static int champStatique\u00c9;

		public StringLookupFixture () {}
		public StringLookupFixture (
			Object p0, Object p1, Object p2, Object p3, Object p4, Object p5, Object p6, Object p7,
			Object p8, Object p9, Object p10, Object p11, Object p12, Object p13, Object p14, Object p15,
			Object p16, Object p17, Object p18, Object p19, Object p20, Object p21, Object p22, Object p23,
			Object p24, Object p25, Object p26, Object p27, Object p28, Object p29, Object p30, Object p31) {}

		public void m\u00e9thode () {}
		public static void m\u00e9thodeStatique () {}

		public void longMethod (
			Object p0, Object p1, Object p2, Object p3, Object p4, Object p5, Object p6, Object p7,
			Object p8, Object p9, Object p10, Object p11, Object p12, Object p13, Object p14, Object p15,
			Object p16, Object p17, Object p18, Object p19, Object p20, Object p21, Object p22, Object p23,
			Object p24, Object p25, Object p26, Object p27, Object p28, Object p29, Object p30, Object p31) {}

		public static void longStaticMethod (
			Object p0, Object p1, Object p2, Object p3, Object p4, Object p5, Object p6, Object p7,
			Object p8, Object p9, Object p10, Object p11, Object p12, Object p13, Object p14, Object p15,
			Object p16, Object p17, Object p18, Object p19, Object p20, Object p21, Object p22, Object p23,
			Object p24, Object p25, Object p26, Object p27, Object p28, Object p29, Object p30, Object p31) {}
	}
}
