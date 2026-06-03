plugins {
    kotlin("jvm") version "2.0.21"
}

repositories {
    mavenCentral()
}

// Don't pin a jvmToolchain -- it would force Gradle to auto-provision a
// matching JDK and fail in CI environments without download repositories
// configured. Use whatever JDK the caller already set in JAVA_HOME (the
// .NET build forwards $(JavaSdkDirectory) for consistency with the rest
// of the repo). Kotlin 2.0.21 targets JVM 11 by default, which is fine
// for the bytecode the tests inspect.

// Emit compiled classes into a stable, predictable location so the
// .NET test harness can load them via ClassFileFixture without needing
// to know the Gradle build directory layout.
tasks.named<org.jetbrains.kotlin.gradle.tasks.KotlinCompile>("compileKotlin") {
    destinationDirectory.set(file("$rootDir/classes"))
}
