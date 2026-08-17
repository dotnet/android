// See: eng/gradle/plugin-repositories.gradle, eng/gradle/dependency-repositories.gradle
pluginManagement {
    apply(from = "$rootDir/../../../../../eng/gradle/plugin-repositories.gradle", to = this)
}
dependencyResolutionManagement {
    apply(from = "$rootDir/../../../../../eng/gradle/dependency-repositories.gradle", to = this)
}

rootProject.name = "kotlin-inline-class-fixtures"
