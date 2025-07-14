namespace ApplicationUtility;

enum AndroidManifestAttributeType : uint
{
	// The 'data' field is either 0 or 1, specifying this resource is either undefined or empty, respectively.
	Null = 0x00,

	// The 'data' field holds a ResTable_ref, a reference to another resource
	Reference = 0x01,

	// The 'data' field holds an attribute resource identifier.
	Attribute = 0x02,

	// The 'data' field holds an index into the containing resource table's global value string pool.
	String = 0x03,

	// The 'data' field holds a single-precision floating point number.
	Float = 0x04,

	// The 'data' holds a complex number encoding a dimension value such as "100in".
	Dimension = 0x05,

	// The 'data' holds a complex number encoding a fraction of a container.
	Fraction = 0x06,

	// The 'data' holds a dynamic ResTable_ref, which needs to be resolved before it can be used like a Reference
	DynamicReference = 0x07,

	// The 'data' holds an attribute resource identifier, which needs to be resolved before it can be used like a Attribute.
	DynamicAttribute = 0x08,

	// The 'data' is a raw integer value of the form n..n.
	IntDec = 0x10,

	// The 'data' is a raw integer value of the form 0xn..n.
	IntHex = 0x11,

	// The 'data' is either 0 or 1, for input "false" or "true" respectively.
	IntBoolean = 0x12,

	// The 'data' is a raw integer value of the form #aarrggbb.
	IntColorARGB8 = 0x1c,

	// The 'data' is a raw integer value of the form #rrggbb.
	IntColorRGB8 = 0x1d,

	// The 'data' is a raw integer value of the form #argb.
	IntColorARGB4 = 0x1e,

	// The 'data' is a raw integer value of the form #rgb.
	IntColorRGB4 = 0x1f,
}
