// See: eng/gradle/plugin-repositories.gradle, eng/gradle/dependency-repositories.gradle
pluginManagement {
    apply(from = "$rootDir/../../../../../eng/gradle/plugin-repositories.gradle", to = this)
}

if (System.getenv("ANDROID_MIRROR_MAVEN_DEPENDENCIES") == "true") {
    apply(from = "$rootDir/../../../../../eng/gradle/credential-provider.gradle")
}

dependencyResolutionManagement {
    apply(from = "$rootDir/../../../../../eng/gradle/dependency-repositories.gradle", to = this)
}

rootProject.name = "kotlin-inline-class-fixtures"
