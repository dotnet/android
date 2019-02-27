// This file is based on the Jenkins scripted pipeline (as opposed to the declarative pipeline) syntax
// https://jenkins.io/doc/book/pipeline/syntax/#scripted-pipeline
import groovy.json.JsonSlurper

def XADir = "xamarin-android"

def MSBUILD_AUTOPROVISION_ARGS="/p:AutoProvision=True /p:AutoProvisionUsesSudo=True /p:IgnoreMaxMonoVersion=False"

def isPr = false                // Default to CI

def buildTarget = 'jenkins'

def stageWithTimeout(stageName, timeoutValue, timeoutUnit, directory, fatal, Closure body) {
    try {
        stage(stageName) {
            timeout(time: timeoutValue, unit: timeoutUnit) {
                dir(directory) {
                    body()
                }
            }
        }
    } catch (error) {
        def result = fatal ? 'ERROR' : 'UNSTABLE'
        echo "ERROR : ${stageName}: Unexpected error: ${error}. Marking build as ${result}."
        currentBuild.result = result
        if (fatal) {
            throw error
        }
    } finally {
        echo "Stage result: ${stageName}: ${currentBuild.currentResult}"
    }
}

def publishPackages(filePaths) {
    def status = 0
    try {
         // Note: The following function is provided by the Azure Blob Jenkins plugin
         azureUpload(storageCredentialId: env.StorageCredentialId,
                 storageType: "blobstorage",
                 containerName: env.ContainerName,
                 virtualPath: env.StorageVirtualPath,
                 filesPath: filePaths,
                 allowAnonymousAccess: true,
                 pubAccessible: true,
                 doNotWaitForPreviousBuild: true,
                 uploadArtifactsOnlyIfSuccessful: true)
    } catch (error) {
        echo "ERROR : publishPackages: Unexpected error: ${error}"
        status = 1
    }

    return status
}

prLabels = null  // Globally defined "static" list accessible within the hasPrLabel function

def hasPrLabel (gitRepo, prId, prLabel) {
    if (!prLabels) {
        prLabels = []

        def url = "https://api.github.com/repos/${gitRepo}/issues/${prId}"
        def jsonContent = new URL(url).getText()
        if (!jsonContent) {
            throw "ERROR : Unable to obtain json content containing PR labels from '${url}'"
        }

        def jsonSlurper = new JsonSlurper()             // http://groovy-lang.org/json.html
        def json = jsonSlurper.parseText(jsonContent)   // Note: We must use parseText instead of parse(url). parse(url) leads to error 'Scripts not permitted to use method groovy.json.JsonSlurper parse java.net.URL'

        for (label in json.labels) {
            prLabels.add(label.name)
        }
    }

    return prLabels.contains(prLabel)
}

timestamps {
    node("${env.BotLabel}") {
        def scmVars

        stageWithTimeout('checkout', 60, 'MINUTES', XADir, true) {    // Time ranges from seconds to minutes depending on how many changes need to be brought down
            sh "env"
            scmVars = checkout scm
        }

        stageWithTimeout('init', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second
            // Note: PR plugin environment variable settings available here: https://wiki.jenkins.io/display/JENKINS/GitHub+pull+request+builder+plugin
            isPr = env.ghprbActualCommit != null
            def branch = isPr ? env.GIT_BRANCH : scmVars.GIT_BRANCH
            def commit = isPr ? env.ghprbActualCommit : scmVars.GIT_COMMIT

            def buildType = isPr ? 'PR' : 'CI'

            echo "Git repo: ${env.GitRepo}"     // Defined as an environment variable in the jenkins build definition
            echo "Job: ${env.JOB_BASE_NAME}"
            echo "Branch: ${branch}"
            echo "Commit: ${commit}"
            echo "Build type: ${buildType}"

            if (isPr) {
                echo "PR id: ${env.ghprbPullId}"
                echo "PR link: ${env.ghprbPullLink}"

                // Clear out the PR title and description. This is the equivalent of $JENKINS_HOME/global-pre-script/remove-problematic-ghprb-parameters.groovy used by freestyle builds
                echo "Clearing the PR title and description environment variables to avoid any special characters contained within from tripping up the build"
                env.ghprbPullTitle = ''
                env.ghprbPullLongDescription = ''

                if (hasPrLabel(env.GitRepo, env.ghprbPullId, 'full-mono-integration-build')) {
                    buildTarget = 'jenkins'
                } else {
                    buildTarget = 'all'
                }
            }

            echo "${buildType} buildTarget: ${buildTarget}"
        }

        stageWithTimeout('clean', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second
            // We need to make sure there's no test AVD present and that the Android emulator isn't running
            // This is to assure that all tests start from the same state
            sh "killall -9 qemu-system-x86_64 || true"
            sh "rm -rf \$HOME/.android/avd/XamarinAndroidTestRunner.*"
        }

        stageWithTimeout('prepare deps', 30, 'MINUTES', XADir, true) {    // Typically takes less than 2 minutes
            sh "make prepare-deps CONFIGURATION=${env.BuildFlavor} MSBUILD_ARGS='$MSBUILD_AUTOPROVISION_ARGS'"
        }

        stageWithTimeout('build', 6, 'HOURS', XADir, true) {    // Typically takes less than one hour except a build on a new bot to populate local caches can take several hours
            sh "make prepare ${buildTarget} CONFIGURATION=${env.BuildFlavor} MSBUILD_ARGS='$MSBUILD_AUTOPROVISION_ARGS'"
        }

        stageWithTimeout('create vsix', 30, 'MINUTES', XADir, true) {    // Typically takes less than 5 minutes
            sh "make create-vsix CONFIGURATION=${env.BuildFlavor}"
        }

        stageWithTimeout('package oss', 30, 'MINUTES', XADir, true) {    // Typically takes less than 5 minutes
            sh "make package-oss"
        }

        stageWithTimeout('build tests', 30, 'MINUTES', XADir, true) {    // Typically takes less than 10 minutes
            sh "make all-tests CONFIGURATION=${env.BuildFlavor}"
        }

        stageWithTimeout('process build results', 10, 'MINUTES', XADir, true) {    // Typically takes less than a minute
            try {
                echo "processing build status"
                sh "make package-build-status CONFIGURATION=${env.BuildFlavor}"
            } catch (error) {
                echo "ERROR : NON-FATAL : processBuildStatus: Unexpected error: ${error}"
            }
        }

        stageWithTimeout('publish packages to Azure', 10, 'MINUTES', '', true) {    // Typically takes less than a minute
            def publishBuildFilePaths = "${XADir}/xamarin.android-oss*.zip,${XADir}/bin/Build*/Xamarin.Android.Sdk*.vsix,${XADir}/build-status*,${XADir}/xa-build-status*";

            if (!isPr) {
                publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/bin/${env.BuildFlavor}/bundle-*.zip"
            }

            echo "publishBuildFilePaths: ${publishBuildFilePaths}"
            def stageStatus = publishPackages(publishBuildFilePaths)
            if (stageStatus != 0) {
                error "publish packages to Azure FAILED, status: ${stageStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        stageWithTimeout('run all tests', 160, 'MINUTES', XADir, false) {   // Typically takes 1hr and 50 minutes (or 110 minutes)
            echo "running tests"

            def skipNunitTests = false

            if (isPr) {
                def hasPrLabelFullMonoIntegrationBuild = hasPrLabel(env.GitRepo, env.ghprbPullId, 'full-mono-integration-build')
                def hasPrLabelRunTestsRelease = hasPrLabel(env.GitRepo, env.ghprbPullId, 'run-tests-release')
                skipNunitTests = hasPrLabelFullMonoIntegrationBuild || hasPrLabelRunTestsRelease
                echo "Run all tests: Labels on the PR: 'full-mono-integration-build' (${hasPrLabelFullMonoIntegrationBuild}) and/or 'run-tests-release' (${hasPrLabelRunTestsRelease})"
            }

            commandStatus = sh (script: "make run-all-tests CONFIGURATION=${env.BuildFlavor}" + (skipNunitTests ? " SKIP_NUNIT_TESTS=1" : ""), returnStatus: true)
            if (commandStatus != 0) {
                error "run-all-tests FAILED, status: ${stageStatus}"     // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        stageWithTimeout('publish test error logs to Azure', 10, 'MINUTES', '', false) {  // Typically takes less than a minute
            echo "packaging test error logs"

            sh "make -C ${XADir} -k package-test-errors"

            def publishTestFilePaths = "${XADir}/xa-test-errors*,${XADir}/test-errors.zip"

            echo "publishTestFilePaths: ${publishTestFilePaths}"
            def stageStatus = publishPackages(publishTestFilePaths)
            if (stageStatus != 0) {
                error "publish test error logs to Azure FAILED, status: ${stageStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        stageWithTimeout('Plot build & test metrics', 30, 'SECONDS', XADir, false) {    // Typically takes less than a second
            if (isPr) {
                echo "Skipping plot metrics for PR build"
                return
            }

            plot(
                    title: 'Jcw',
                    csvFileName: 'plot-jcw-test-times.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Xamarin.Android.JcwGen_Tests-times.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Tests times',
                    logarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )
            plot(
                    title: 'Locale',
                    csvFileName: 'plot-locale-times.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Xamarin.Android.Locale_Tests-times.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Tests times',
                    logarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )
            plot(
                    title: 'Runtime test sizes',
                    csvFileName: 'plot-runtime-test-sizes.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Mono.Android_Tests-values.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Tests size',
                    logarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )
            plot(
                    title: 'Runtime merged',
                    csvFileName: 'plot-runtime-merged-test-times.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Mono.Android_Tests-times.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Tests times',
                    logarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )
            plot(
                    title: 'Xamarin.Forms app startup',
                    csvFileName: 'plot-xamarin-forms-startup-test-times.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Xamarin.Forms_Test-times.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Tests times',
                    logarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )
            plot(
                    title: 'Xamarin.Forms app',
                    csvFileName: 'plot-xamarin-forms-tests-size.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Xamarin.Forms_Tests-values.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Tests size',
                    ogarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )

            plot(
                    title: 'Hello World',
                    csvFileName: 'plot-hello-world-build-times.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Timing-HelloWorld.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Build times',
                    logarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )

            plot(
                    title: 'Xamarin.Forms',
                    csvFileName: 'plot-xamarin-forms-integration-build-times.csv',
                    csvSeries: [[
                        displayTableFlag: true, file: 'TestResult-Timing-Xamarin.Forms-Integration.csv', inclusionFlag: 'OFF'
                    ]],
                    group: 'Build times',
                    logarithmic: true,
                    style: 'line',
                    yaxis: 'ms'
            )
        }

        stageWithTimeout('Publish test results', 5, 'MINUTES', XADir, false) {    // Typically takes under 1 minute to publish test results
            def initialStageResult = currentBuild.currentResult

            xunit thresholds: [
                    failed(unstableNewThreshold: '0', unstableThreshold: '0'),
                    skipped()                                                       // Note: Empty threshold settings per settings in the xamarin-android freestyle build are not permitted here
                ],
                tools: [
                    NUnit2(deleteOutputFiles: true,
                            failIfNotNew: true,
                            pattern: 'TestResult-*.xml',
                            skipNoTestFiles: true,
                            stopProcessingIfError: false)
                ]

            if (initialStageResult == 'SUCCESS' && currentBuild.currentResult == 'UNSTABLE') {
                error "One or more tests failed"                // Force an error condition if there was a test failure to indicate that this stage was the source of the build failure
            }
        }
    }
}
