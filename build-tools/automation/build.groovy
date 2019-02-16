// This file is based on the Jenkins scripted pipeline (as opposed to the declarative pipeline) syntax
// https://jenkins.io/doc/book/pipeline/syntax/#scripted-pipeline

def XADir = "xamarin-android"

def MSBUILD_AUTOPROVISION_ARGS="/p:AutoProvision=True /p:AutoProvisionUsesSudo=True /p:IgnoreMaxMonoVersion=False"

def isPr = false                // Default to CI

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
         azureUpload(storageCredentialId: "${env.StorageCredentialId}",
                 storageType: "blobstorage",
                 containerName: "${env.ContainerName}",
                 virtualPath: "${env.StorageVirtualPath}",
                 filesPath: "${filePaths}",
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

timestamps {
    node("${env.BotLabel}") {
        def scmVars

        stageWithTimeout('checkout', 60, 'MINUTES', XADir, true) {    // Time ranges from seconds to minutes depending on how many changes need to be brought down
            scmVars = checkout scm
        }

        stageWithTimeout('init', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second
            // Note: PR plugin environment variable settings available here: https://wiki.jenkins.io/display/JENKINS/GitHub+pull+request+builder+plugin
            isPr = env.ghprbActualCommit != null
            def branch = isPr ? env.GIT_BRANCH : scmVars.GIT_BRANCH
            def commit = isPr ? env.ghprbActualCommit : scmVars.GIT_COMMIT

            def buildType = isPr ? 'PR' : 'CI'

            echo "Job: ${env.JOB_BASE_NAME}"
            echo "Branch: ${branch}"
            echo "Commit: ${commit}"
            echo "Build type: ${buildType}"
            if (isPr) {
                echo "PR id: ${env.ghprbPullId}"
                echo "PR link: ${env.ghprbPullLink}"
            }
        }

        stageWithTimeout('clean', 30, 'SECONDS', 'xamarin-android_tmp', true) {    // Typically takes less than a second
            deleteDir()
        }

        stageWithTimeout('prepare deps', 30, 'MINUTES', XADir, true) {    // Typically takes less than 2 minutes
            sh "make prepare-deps CONFIGURATION=${env.BuildFlavor} MSBUILD_ARGS='$MSBUILD_AUTOPROVISION_ARGS'"
        }

        stageWithTimeout('build', 6, 'HOURS', XADir, true) {    // Typically takes less than one hour except a build on a new bot to populate local caches can take several hours
            if (isPr) {
                echo "PR build definition detected: building with 'make all'"
                sh "make all CONFIGURATION=${env.BuildFlavor} MSBUILD_ARGS='$MSBUILD_AUTOPROVISION_ARGS'"
            } else {
                echo "PR build definition *not* detected: building with 'make jenkins'"
                sh "make jenkins CONFIGURATION=${env.BuildFlavor} MSBUILD_ARGS='$MSBUILD_AUTOPROVISION_ARGS'"
            }
        }

        stageWithTimeout('create vsix', 30, 'MINUTES', XADir, true) {    // Typically takes less than 5 minutes
            sh "make create-vsix CONFIGURATION=${env.BuildFlavor}"
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

        stageWithTimeout('publish packages to Azure', 10, 'MINUTES', XADir, true) {    // Typically takes less than a minute
            def publishBuildFilePaths = "xamarin.android-oss*.zip,bin/${env.BuildFlavor}/bundle-*.zip,bin/Build*/Xamarin.Android.Sdk*.vsix,prepare-image-dependencies.sh,build-status*,xa-build-status*";
            echo "publishBuildFilePaths: ${publishBuildFilePaths}"
            def stageStatus = publishPackages(publishBuildFilePaths)
            if (stageStatus != 0) {
                error "publish packages to Azure FAILED, status: ${stageStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        stageWithTimeout('run all tests', 160, 'MINUTES', XADir, false) {   // Typically takes 1hr and 50 minutes (or 110 minutes)
            echo "running tests"
            def stageStatus = sh(
                script: "make run-all-tests CONFIGURATION=${env.BuildFlavor}",
                returnStatus: true
            );

            if (stageStatus != 0) {
                error "run-all-tests FAILED, status: ${stageStatus}"     // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        stageWithTimeout('publish test error logs to Azure', 10, 'MINUTES', XADir, false) {  // Typically takes less than a minute
            echo "packaging test error logs"
            sh "make package-test-errors"

            def publishTestFilePaths = "xa-test-errors*"
            echo "publishTestFilePaths: ${publishTestFilePaths}"
            def stageStatus = publishPackages(publishTestFilePaths)
            if (stageStatus != 0) {
                error "publish test error logs to Azure FAILED, status: ${stageStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        stageWithTimeout('Plot build & test metrics', 30, 'SECONDS', XADir, false) {    // Typically takes less than a second
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
