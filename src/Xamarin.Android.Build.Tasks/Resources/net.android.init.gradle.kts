/**
 * This is a generated file used by the .NET for Android build process to override the build output directory of a Gradle project.
 * See:
 *    https://docs.gradle.org/current/dsl/org.gradle.api.file.ProjectLayout.html#org.gradle.api.file.ProjectLayout:buildDirectory
 *    https://docs.gradle.org/current/kotlin-dsl/gradle/org.gradle.api.invocation/-gradle/projects-loaded.html
 */
gradle.projectsLoaded {
	gradle.startParameter.projectProperties["netAndroidBuildDirOverride"]?.let { buildDir ->
		rootProject.allprojects {
			afterEvaluate {
				layout.buildDirectory.set(file(buildDir))
			}
		}
	}
}
