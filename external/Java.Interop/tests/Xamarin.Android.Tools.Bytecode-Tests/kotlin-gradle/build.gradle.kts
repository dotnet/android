plugins {
    kotlin("jvm") version "2.4.10"
}

layout.buildDirectory.set(file((findProperty("kotlinBuildDir") as String?) ?: "$rootDir/build"))

// Don't pin a jvmToolchain -- it would force Gradle to auto-provision a
// matching JDK and fail in CI environments without download repositories
// configured. Use whatever JDK the caller already set in JAVA_HOME (the
// .NET build forwards $(JavaSdkDirectory) for consistency with the rest
// of the repo). Pin the compiler output to JVM 11 so Kotlin plugin updates
// don't change the bytecode version the tests inspect.
java {
    sourceCompatibility = JavaVersion.VERSION_11
    targetCompatibility = JavaVersion.VERSION_11
}

// Emit compiled classes into a stable, predictable location so the
// .NET test harness can load them via ClassFileFixture without needing
// to know the Gradle build directory layout.
tasks.named<org.jetbrains.kotlin.gradle.tasks.KotlinCompile>("compileKotlin") {
    compilerOptions.jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_11)
    destinationDirectory.set(file((findProperty("kotlinClassesDir") as String?) ?: "$rootDir/classes"))
}
