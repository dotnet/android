class JvmOverloadsConstructor {
    @JvmOverloads
    constructor(
        something : JvmOverloadsConstructor,
        id: Int = 1,
        imageId: Int = 2,
        title: String,
        useDivider: Boolean = false
    ) {
    }
}
