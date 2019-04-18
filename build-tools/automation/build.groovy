// This file is based on the Jenkins scripted pipeline (as opposed to the declarative pipeline) syntax
// https://jenkins.io/doc/book/pipeline/syntax/#scripted-pipeline
def XADir = "xamarin-android"

def EXTRA_MSBUILD_ARGS="/p:AutoProvision=True /p:AutoProvisionUsesSudo=True /p:IgnoreMaxMonoVersion=False"

def isCommercial = false
def commercialPath = ''

def isPr = false                // Default to CI

def hasPrLabelFullMonoIntegrationBuild = false

def buildTarget = 'jenkins'

def utils = null

prLabels = null  // Globally defined "static" list accessible within the hasPrLabel function

timestamps {
    node("${env.BotLabel}") {
        def scmVars = null

        stage ("checkout") {
            def ctAttempts = 3
            def retryAttempt = 0
            def waitSecondsBeforeRetry = 15
            retry(ctAttempts) {     // Retry will always invoke the body at least once for an attempt count of 0 or 1
                dir (XADir) {
                    if (retryAttempt > 0) {
                        echo "WARNING : Stage checkout failed on try #${retryAttempt}. Waiting ${waitSecondsBeforeRetry} seconds"
                        sleep(waitSecondsBeforeRetry)
                        echo "Retrying ..."
                        waitSecondsBeforeRetry = waitSecondsBeforeRetry * 2
                    }

                    retryAttempt++
                    scmVars = checkout scm
                }
            }
        }

        utils = load "${XADir}/build-tools/automation/utils.groovy"

        utils.stageWithTimeout('init', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second
            isCommercial = env.IsCommercial == '1'
            commercialPath = "external/${env.CommercialDirectory}"

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
            echo "HOME: ${HOME}"                // UNDONE: Make sure HOME resolves from groovy

            if (isPr) {
                echo "PR id: ${env.ghprbPullId}"
                echo "PR link: ${env.ghprbPullLink}"

                // Clear out the PR title and description. This is the equivalent of $JENKINS_HOME/global-pre-script/remove-problematic-ghprb-parameters.groovy used by freestyle builds
                echo "Clearing the PR title and description environment variables to avoid any special characters contained within from tripping up the build"
                env.ghprbPullTitle = ''
                env.ghprbPullLongDescription = ''

                if (utils.hasPrLabel(env.GitRepo, env.ghprbPullId, 'full-mono-integration-build')) {
                    hasPrLabelFullMonoIntegrationBuild = true
                    buildTarget = 'jenkins'
                } else {
                    buildTarget = 'all'
                    // Also compile host libs for windows so that a complete VSIX can be created
                    if (isUnix()) {
                        def uname = sh script: 'uname', returnStdout: true
                        if (uname.startsWith("Darwin")) {
                            EXTRA_MSBUILD_ARGS += " /p:AndroidSupportedHostJitAbis=Darwin:mxe-Win32:mxe-Win64"
                        }
                    }
                }
            }

            if (isCommercial) {
                echo "Commercial root: ${commercialPath}"
            }

            echo "${buildType} buildTarget: ${buildTarget}"

            sh "env"
        }

        utils.stageWithTimeout('clean', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second
            // We need to make sure there's no test AVD present and that the Android emulator isn't running
            // This is to assure that all tests start from the same state
            sh "killall -9 qemu-system-x86_64 || true"
            sh "rm -rf \$HOME/.android/avd/XamarinAndroidTestRunner.*"
        }

        utils.stageWithTimeout('prepare deps', 60, 'MINUTES', XADir, true) {    // Typically takes less than 2 minutes, but can take longer to perform the checkout involved for commercial builds
            if (isCommercial) {
                sh "make prepare-external-git-dependencies"

                utils.stageWithTimeout('provisionator', 30, 'MINUTES', "${commercialPath}/build-tools/provisionator", true) {
                    sh('./provisionator.sh profile.csx -v')
                }
            }

            sh "make prepare-deps CONFIGURATION=${env.BuildFlavor} V=1 MSBUILD_ARGS='$EXTRA_MSBUILD_ARGS'"

            if (isCommercial) {
                sh "make prepare-image-dependencies CONFIGURATION=${env.BuildFlavor} V=1 MSBUILD=msbuild MSBUILD_ARGS='$EXTRA_MSBUILD_ARGS'"
            }
        }

        utils.stageWithTimeout('build', 6, 'HOURS', XADir, true) {    // Typically takes less than one hour except a build on a new bot to populate local caches can take several hours
            sh "make prepare ${buildTarget} CONFIGURATION=${env.BuildFlavor} V=1 MSBUILD_ARGS='$EXTRA_MSBUILD_ARGS'"
        }

        utils.stageWithTimeout('create installers', 30, 'MINUTES', XADir, true) {    // Typically takes less than 5 minutes
            if (isPr) {
                // Override _MSBUILD_ARGS to ensure we only package the `AndroidSupportedTargetJitAbis` which are built.
                // Also ensure that we don't require mono bundle components in the installer if this is not a full mono integration build.
                def msbuildInstallerArgs = hasPrLabelFullMonoIntegrationBuild ? '' : '/p:IncludeMonoBundleComponents=False'
                sh "make create-installers CONFIGURATION=${env.BuildFlavor} V=1 MSBUILD_ARGS='${msbuildInstallerArgs}'"
            } else {
                sh "make create-installers CONFIGURATION=${env.BuildFlavor} V=1"
            }
        }

        utils.stageWithTimeout('package oss', 30, 'MINUTES', XADir, true) {    // Typically takes less than 5 minutes
            sh "make package-oss CONFIGURATION=${env.BuildFlavor}"
        }

        utils.stageWithTimeout('build tests', 30, 'MINUTES', XADir, true) {    // Typically takes less than 10 minutes
            // UNDONE:
            if (isCommercial) {
                echo "Skipping 'build tests' stage"
                return
            }

            sh "make all-tests CONFIGURATION=${env.BuildFlavor} V=1"
        }

        utils.stageWithTimeout('process build results', 10, 'MINUTES', XADir, true) {    // Typically takes less than a minute
            try {
                echo "processing build status"
                sh "make package-build-status CONFIGURATION=${env.BuildFlavor}"
            } catch (error) {
                echo "ERROR : NON-FATAL : processBuildStatus: Unexpected error: ${error}"
            }
        }

        utils.stageWithTimeout('publish packages to Azure', 30, 'MINUTES', '', true, 3) {    // Typically takes less than a minute, but provide ample time in situations where logs may be quite large
            def publishBuildFilePaths = "${XADir}/xamarin.android-oss*.zip,${XADir}/bin/Build*/Xamarin.Android.Sdk-OSS*,${XADir}/build-status*,${XADir}/xa-build-status*";

            if (!isPr) {
                publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/bin/${env.BuildFlavor}/bundle-*.zip"
            }

            echo "publishBuildFilePaths: ${publishBuildFilePaths}"
            def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, env.StorageVirtualPath, publishBuildFilePaths)
            if (commandStatus != 0) {
                error "publish packages to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('run all tests', 160, 'MINUTES', XADir, false) {   // Typically takes 1hr and 50 minutes (or 110 minutes)
            echo "running tests"

            // UNDONE:
            if (isCommercial) {
                echo "Skipping 'run all tests' stage"
                return
            }

            def skipNunitTests = false

            if (isPr) {
                def hasPrLabelRunTestsRelease = utils.hasPrLabel(env.GitRepo, env.ghprbPullId, 'run-tests-release')
                skipNunitTests = hasPrLabelFullMonoIntegrationBuild || hasPrLabelRunTestsRelease
                echo "Run all tests: Labels on the PR: 'full-mono-integration-build' (${hasPrLabelFullMonoIntegrationBuild}) and/or 'run-tests-release' (${hasPrLabelRunTestsRelease})"
            }

            commandStatus = sh (script: "make run-all-tests CONFIGURATION=${env.BuildFlavor} V=1" + (skipNunitTests ? " SKIP_NUNIT_TESTS=1" : ""), returnStatus: true)
            if (commandStatus != 0) {
                error "run-all-tests FAILED, status: ${commandStatus}"     // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('publish test error logs to Azure', 30, 'MINUTES', '', false, 3) {  // Typically takes less than a minute, but provide ample time in situations where logs may be quite large
            echo "packaging test error logs"

            // UNDONE:
            if (isCommercial) {
                echo "Skipping 'publish test error logs' stage"
                return
            }

            publishHTML target: [
                allowMissing:           true,
                alwaysLinkToLastBuild:  false,
                escapeUnderscores:      true,
                includes:               '**/*',
                keepAll:                true,
                reportDir:              "xamarin-android/bin/Test${env.BuildFlavor}/compatibility",
                reportFiles:            '*.html',
                reportName:             'API Compatibility Checks'
            ]

            sh "make -C ${XADir} -k package-test-results CONFIGURATION=${env.BuildFlavor}"

            def publishTestFilePaths = "${XADir}/xa-test-results*,${XADir}/test-errors.zip"

            echo "publishTestFilePaths: ${publishTestFilePaths}"
            def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, env.StorageVirtualPath, publishTestFilePaths)
            if (commandStatus != 0) {
                error "publish test error logs to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('Plot build & test metrics', 30, 'SECONDS', XADir, false, 3) {    // Typically takes less than a second
            // UNDONE: Skip plots
            // if (isPr) {
            if (isCommercial || isPr) {
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

        utils.stageWithTimeout('Publish test results', 5, 'MINUTES', XADir, false, 3) {    // Typically takes under 1 minute to publish test results
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
