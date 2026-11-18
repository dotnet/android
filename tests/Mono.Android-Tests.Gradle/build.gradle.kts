// Same versions as tests/CodeGen-Binding/.../JavaLib and tests/Xamarin.Android.Tools.Bytecode-Tests/kotlin-gradle.
// AGP compiles Kotlin with its built-in support, using the Kotlin plugin version declared here.
plugins {
    id("com.android.library") version "9.5.1" apply false
    kotlin("jvm") version "2.4.21" apply false
}
