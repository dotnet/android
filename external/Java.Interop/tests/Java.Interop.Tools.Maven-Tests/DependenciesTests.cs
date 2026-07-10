using System;
using System.Linq;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven_Tests.Extensions;

namespace Java.Interop.Tools.Maven_Tests;

public class DependenciesTests
{
	[Test]
	[TestCase ("dev.chrisbanes.snapper:snapper:0.3.0", "androidx.compose.foundation:foundation:1.2.1 - compile;org.jetbrains.kotlin:kotlin-stdlib-jdk8:1.6.21 - compile")]
	[TestCase ("com.squareup.wire:wire-runtime:4.4.3", "com.squareup.okio:okio:3.0.0 - runtime;org.jetbrains.kotlin:kotlin-stdlib-common:1.6.10 - runtime")]
	[TestCase ("org.jboss:jboss-vfs:3.2.17.Final", "org.jboss.logging:jboss-logging:3.1.4.GA - compile")]
	[TestCase ("com.squareup.okio:okio:1.17.4", "org.codehaus.mojo:animal-sniffer-annotations:1.10 - compile")]
	[TestCase ("org.jetbrains.kotlin:kotlin-stdlib:1.6.20", "org.jetbrains:annotations:13.0 - compile;org.jetbrains.kotlin:kotlin-stdlib-common:1.6.20 - compile")]
	[TestCase ("com.github.bumptech.glide:glide:4.13.2", "androidx.exifinterface:exifinterface:1.2.0 - compile;androidx.fragment:fragment:1.3.1 - compile;androidx.tracing:tracing:1.0.0 - compile;androidx.vectordrawable:vectordrawable-animated:1.0.0 - compile;com.github.bumptech.glide:annotations:4.13.2 - compile;com.github.bumptech.glide:disklrucache:4.13.2 - compile;com.github.bumptech.glide:gifdecoder:4.13.2 - compile")]
	[TestCase ("io.reactivex.rxjava3:rxandroid:3.0.0", "io.reactivex.rxjava3:rxjava:3.0.0 - compile")]
	[TestCase ("com.jakewharton.timber:timber:5.0.1", "org.jetbrains:annotations:20.1.0 - runtime;org.jetbrains.kotlin:kotlin-stdlib:1.5.21 - compile")]
	[TestCase ("org.greenrobot:eventbus:3.3.1", "org.greenrobot:eventbus-java:3.3.1 - compile")]
	[TestCase ("com.squareup.picasso:picasso:2.8", "androidx.annotation:annotation:1.0.0 - compile;androidx.exifinterface:exifinterface:1.0.0 - compile;com.squareup.okhttp3:okhttp:3.10.0 - compile")]
	[TestCase ("pub.devrel:easypermissions:3.0.0", "androidx.annotation:annotation:1.1.0 - compile;androidx.appcompat:appcompat:1.1.0 - compile;androidx.core:core:1.3.0 - compile;androidx.fragment:fragment:1.2.5 - compile")]
	[TestCase ("com.squareup.okio:samples:1.17.6", "com.squareup.okio:okio:1.17.6 - compile")]
	[TestCase ("com.android.volley:volley:1.2.1", "")]
	[TestCase ("as.leap:LAS-cloudcode-sdk:2.3.6", "com.fasterxml.jackson.core:jackson-core:2.5.3 - compile;com.fasterxml.jackson.core:jackson-databind:2.5.3 - compile")]
	[TestCase ("ai.grakn:grakn-dist:1.4.1", "ai.grakn:grakn-engine:1.4.1 - compile;ai.grakn:grakn-factory:1.4.1 - compile;ai.grakn:grakn-graql-shell:1.4.1 - compile;ai.grakn:migration-csv:1.4.1 - compile;ai.grakn:migration-export:1.4.1 - compile;ai.grakn:migration-json:1.4.1 - compile;ai.grakn:migration-sql:1.4.1 - compile;ai.grakn:migration-xml:1.4.1 - compile;ch.qos.logback:logback-classic:1.2.3 - compile;ch.qos.logback:logback-core:1.2.3 - compile;io.airlift:airline:0.6 - compile;org.codehaus.janino:janino:2.7.8 - compile;org.slf4j:slf4j-api:1.7.20 - compile")]
	[TestCase ("at.crea-doo.homer:shell.data:1.0.11", "at.crea-doo.homer:processing.data:1.0.11 - compile;joda-time:joda-time:2.9.9 - compile;org.apache.karaf.shell:org.apache.karaf.shell.core:4.0.9 - compile")]
	[TestCase ("at.crea-doo.homer:connector.ifttt.maker:1.0.11", "at.ac.ait.hbs.homer:at.ac.ait.hbs.homer.core.common:1.2.51 - compile;com.google.code.gson:gson:2.8.1 - compile;commons-codec:commons-codec:1.10 - compile;commons-logging:commons-logging:1.2 - compile;org.apache.httpcomponents:httpclient:4.5.3 - compile;org.apache.httpcomponents:httpcore:4.4.6 - compile")]
	[TestCase ("at.reilaender.asciidoctorj.bootconfig2adoc:bootconfig2adoc-adoc:0.1.3", "at.reilaender.asciidoctorj.bootconfig2adoc:bootconfig2adoc-core:0.1.3 - compile")]
	[TestCase ("at.researchstudio.sat:won-bot:0.9", "at.researchstudio.sat:won-core:0.9 - compile;at.researchstudio.sat:won-cryptography:0.9 - compile;at.researchstudio.sat:won-matcher:0.9 - compile;at.researchstudio.sat:won-owner:0.9 - compile;at.researchstudio.sat:won-sockets-tx:0.9 - compile;at.researchstudio.sat:won-utils-conversation:0.9 - compile;at.researchstudio.sat:won-utils-goals:0.9 - compile;ch.qos.logback:logback-classic:1.0.13 - compile;ch.qos.logback:logback-core:1.0.13 - compile;commons-io:commons-io:2.4 - compile;org.apache.commons:commons-email:1.3.1 - compile;org.apache.commons:commons-lang3:3.4 - compile;org.apache.httpcomponents:httpclient:4.5 - compile;org.apache.jena:jena-arq:3.5.0 - compile;org.apache.jena:jena-core:3.5.0 - compile;org.aspectj:aspectjweaver:1.5.4 - compile;org.bouncycastle:bcpkix-jdk15on:1.64 - compile;org.bouncycastle:bcprov-jdk15on:1.64 - compile;org.javasimon:javasimon-core:3.4.0 - compile;org.javasimon:javasimon-spring:3.4.0 - compile;org.jsoup:jsoup:1.7.3 - compile;org.quartz-scheduler:quartz:2.2.1 - compile;org.slf4j:slf4j-api:1.6.6 - compile;org.springframework:spring-core:4.3.18.RELEASE - compile;org.springframework.boot:spring-boot:1.5.17.RELEASE - compile;org.springframework.data:spring-data-commons:1.13.16.RELEASE - compile;org.springframework.data:spring-data-mongodb:1.10.3.RELEASE - compile")]
	[TestCase ("au.csiro:elk-distribution-owlapi4:0.5.0", "au.csiro:elk-owlapi4:0.5.0 - compile;org.liveontologies:owlapi-proof:0.1.0 - compile;org.liveontologies:puli:0.1.0 - compile")]
	[TestCase ("au.csiro:elk-distribution-owlapi5:0.5.0", "au.csiro:elk-distribution-owlapi4:0.5.0 - compile;au.csiro:elk-distribution-owlapi4:0.5.0 - compile;au.csiro:elk-owlapi5:0.5.0 - compile;org.liveontologies:owlapi-proof:0.1.0 - compile;org.liveontologies:puli:0.1.0 - compile")]
	[TestCase ("au.gov.amsa.risky:ais:0.6.17", "au.gov.amsa.risky:formats:0.6.17 - compile;au.gov.amsa.risky:formats:0.6.17 - compile;au.gov.amsa.risky:streams:0.6.17 - compile;com.github.davidmoten:rxjava-extras:0.8.0.20 - compile;com.github.davidmoten:rxjava-slf4j:0.7.0 - compile;com.google.guava:guava:32.1.3-jre - compile;io.reactivex:rxjava-string:1.1.1 - compile;io.reactivex:rxjava:1.3.8 - compile;org.slf4j:slf4j-api:2.0.9 - compile")]
	[TestCase ("am.ik.springmvc:new-controller:0.2.0", "me.geso:routes:0.6.0 - compile;org.slf4j:slf4j-api:1.7.8 - compile;org.springframework:spring-webmvc:4.1.4.RELEASE - compile")]
	[TestCase ("me.drakeet.multitype:multitype:3.5.0", "androidx.annotation:annotation:1.0.0 - compile;androidx.recyclerview:recyclerview:1.0.0 - compile")]
	[TestCase ("com.facebook.fresco:fresco:2.6.0", "com.facebook.fresco:drawee:2.6.0 - compile;com.facebook.fresco:fbcore:2.6.0 - compile;com.facebook.fresco:imagepipeline-native:2.6.0 - compile;com.facebook.fresco:imagepipeline:2.6.0 - compile;com.facebook.fresco:memory-type-ashmem:2.6.0 - compile;com.facebook.fresco:memory-type-java:2.6.0 - compile;com.facebook.fresco:memory-type-native:2.6.0 - compile;com.facebook.fresco:nativeimagefilters:2.6.0 - compile;com.facebook.fresco:nativeimagetranscoder:2.6.0 - compile;com.facebook.fresco:soloader:2.6.0 - runtime;com.facebook.fresco:ui-common:2.6.0 - runtime;com.facebook.soloader:nativeloader:0.10.1 - runtime")]
	[TestCase ("com.tencent.mm.opensdk:wechat-sdk-android-without-mta:6.8.0", "")]
	[TestCase ("com.facebook.android:facebook-android-sdk:14.1.1", "com.facebook.android:facebook-applinks:14.1.1 - compile;com.facebook.android:facebook-common:14.1.1 - compile;com.facebook.android:facebook-core:14.1.1 - compile;com.facebook.android:facebook-gamingservices:14.1.1 - compile;com.facebook.android:facebook-login:14.1.1 - compile;com.facebook.android:facebook-messenger:14.1.1 - compile;com.facebook.android:facebook-share:14.1.1 - compile;org.jetbrains.kotlin:kotlin-stdlib:1.5.10 - compile")]
	[TestCase ("com.airbnb.android:lottie:5.2.0", "androidx.appcompat:appcompat:1.3.1 - runtime;com.squareup.okio:okio:1.17.4 - runtime")]
	public void TestMavenCentralResolvedDependencies (string artifact, string expected)
		=> TestResolvedDependencies (MavenProjectResolver.Central, artifact, expected);

	void TestResolvedDependencies (MavenProjectResolver resolver, string artifact, string expected)
	{
		var art = Artifact.Parse (artifact);
		var project = ResolvedProject.FromArtifact (art, resolver);
		var dependencies = project.Dependencies.Where (d => d.Scope == "compile" || d.Scope == "runtime").OrderBy (d => d.ToString ()).ToList ();

		if (dependencies.FirstOrDefault (d => string.IsNullOrEmpty (d.Version)) is ResolvedDependency rd)
			throw new Exception ($"Missing version - {rd}");

		if (dependencies.FirstOrDefault (d => d.Version.Contains ('$')) is ResolvedDependency rd2)
			throw new Exception ("Unresolved variable in version");

		Console.WriteLine (string.Join (';', dependencies));
		Assert.AreEqual (expected, string.Join (';', dependencies));
	}
}
