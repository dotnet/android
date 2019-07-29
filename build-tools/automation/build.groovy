// This file is based on the Jenkins scripted pipeline (as opposed to the declarative pipeline) syntax
// https://jenkins.io/doc/book/pipeline/syntax/#scripted-pipeline
def XADir = 'xamarin-android'
def packageDir = 'package'

def EXTRA_MSBUILD_ARGS="/p:AutoProvision=True /p:AutoProvisionUsesSudo=True /p:IgnoreMaxMonoVersion=False"

def isCommercial = false
def commercialPath = ''
def packagePath = ''
def storageVirtualPath = ''

def isPr = false                // Default to CI

def gitRepo = ''
def branch = ''
def commit = ''

def skipSigning = false
def skipTest = false

def hasPrLabelFullMonoIntegrationBuild = false

def prepareFlags = 'PREPARE_CI=1'
def buildTarget = 'jenkins'

def utils = null

prLabels = null  // Globally defined "static" list accessible within the hasPrLabel function

@NonCPS
def getBuildTasksRedirect() {
    def connection = "https://dl.internalx.com/build-tools/latest/Xamarin.Build.Tasks.nupkg".toURL().openConnection()
    connection.instanceFollowRedirects = false
    // should be in scope at the time of this call (called within a withCredentials block)
    connection.setRequestProperty("Authorization", "token ${env.GITHUB_AUTH_TOKEN}")
    def response = connection.responseCode
    connection.disconnect()
    if (response == 302) {
        return connection.getHeaderField("Location")
    } else {
        throw new Exception("DL link failed ${response}: ${content}")
    }
}

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
            packagePath =  "${env.WORKSPACE}/${packageDir}"
            storageVirtualPath = env.StorageVirtualPath

            skipSigning = env.SkipSigning == '1'
            skipTest = env.SkipTest == '1'

            gitRepo = scmVars.GIT_URL.replace("git@github.com:", "").split("/").takeRight(2).join("/").replace(".git", "")     // Example result: xamarin/xamarin-android

            // Note: PR plugin environment variable settings available here: https://wiki.jenkins.io/display/JENKINS/GitHub+pull+request+builder+plugin
            isPr = env.ghprbActualCommit != null
            branch = isPr ? env.GIT_BRANCH : scmVars.GIT_BRANCH
            commit = isPr ? env.ghprbActualCommit : scmVars.GIT_COMMIT

            def buildType = isPr ? 'PR' : 'CI'

            echo "Git repo: ${gitRepo}"         // Example: xamarin/xamarin-android
            echo "Job: ${env.JOB_BASE_NAME}"
            echo "Job name: ${env.JOB_NAME}"
            echo "Workspace: ${env.WORKSPACE}"
            echo "Branch: ${branch}"
            echo "Commit: ${commit}"
            echo "Build type: ${buildType}"
            echo "Build number: ${env.BUILD_NUMBER}"
            echo "IsCommercial: ${isCommercial}"

            if (isCommercial) {
                echo "Commercial path: ${commercialPath}"
                storageVirtualPath = "${env.JOB_NAME}-${env.BUILD_NUMBER}/${branch}/${commit}"      // This needs to be unique for commercial builds since the path determines where artifacts.json used for GitHub statuses will be published
            }

            echo "Package path: ${packagePath}"
            echo "Storage path: ${storageVirtualPath}"

            echo "SkipSigning: ${skipSigning}"
            echo "SkipTest: ${skipTest}"

            if (isPr) {
                echo "PR id: ${env.ghprbPullId}"
                echo "PR link: ${env.ghprbPullLink}"

                // Clear out the PR title and description. This is the equivalent of $JENKINS_HOME/global-pre-script/remove-problematic-ghprb-parameters.groovy used by freestyle builds
                echo "Clearing the PR title and description environment variables to avoid any special characters contained within from tripping up the build"
                env.ghprbPullTitle = ''
                env.ghprbPullLongDescription = ''

                if (utils.hasPrLabel(gitRepo, env.ghprbPullId, 'full-mono-integration-build')) {
                    hasPrLabelFullMonoIntegrationBuild = true
                    buildTarget = 'jenkins'
                } else {
                    prepareFlags = 'PREPARE_CI_PR=1'
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

            echo "${buildType} buildTarget: ${buildTarget}"

            sh "env"
        }

        utils.stageWithTimeout('clean', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second
            // We need to make sure there's no test AVD present and that the Android emulator isn't running
            // This is to assure that all tests start from the same state
            sh "killall -9 qemu-system-x86_64 || true"
            sh "rm -rf \$HOME/.android/avd/XamarinAndroidTestRunner.*"
            if (fileExists(packagePath)) {
                sh "rm -rf ${packagePath}"
            }

            sh "mkdir -p ${packagePath}"
        }

        utils.stageWithTimeout('prepare deps', 30, 'MINUTES', XADir, true) {    // Typically takes less than 2 minutes, but can take longer if any prereqs need to be provisioned
            if (isCommercial) {
                sh "make prepare-external-git-dependencies ${prepareFlags} V=1 "

                utils.stageWithTimeout('provisionator', 30, 'MINUTES', "${commercialPath}/build-tools/provisionator", true) {
                    sh('./provisionator.sh profile.csx -v')
                }

                utils.stageWithTimeout('build tasks', 30, 'MINUTES', env.WORKSPACE, true) {
                    withCredentials([string(credentialsId: "${env.GitHubAuthTokenCredentialId}", variable: 'GITHUB_AUTH_TOKEN')]) {
                        def redirect = getBuildTasksRedirect()
                        sh "curl -o Xamarin.Build.Tasks.nupkg \"${redirect}\""
                        dir("BuildTasks") {
                            deleteDir()
                            sh "unzip ../Xamarin.Build.Tasks.nupkg"
                        }
                    }
                }
            }
        }

        utils.stageWithTimeout('build', 6, 'HOURS', XADir, true) {    // Typically takes less than one hour except a build on a new bot to populate local caches can take several hours
            // The 'prepare*' targets must run separately to '${buildTarget}` as preparation generates the 'rules.mk' file conditionally included by
            // Makefile and it will **NOT** be included if we call e.g `make prepare jenkins` so the 'jenkns' target will **NOT** build all the supported
            // targets and architectures leading to test errors later on (e.g. in EmbeddedDSOs tests)
            sh "make prepare-update-mono CONFIGURATION=${env.BuildFlavor} V=1 ${prepareFlags} MSBUILD_ARGS='$EXTRA_MSBUILD_ARGS'"
            sh "make prepare CONFIGURATION=${env.BuildFlavor} V=1 ${prepareFlags} MSBUILD_ARGS='$EXTRA_MSBUILD_ARGS'"
            sh "make ${buildTarget} CONFIGURATION=${env.BuildFlavor} V=1 ${prepareFlags} MSBUILD_ARGS='$EXTRA_MSBUILD_ARGS'"

            if (isCommercial) {
                sh '''
                    VERSION=`LANG=C; export LANG && git log --no-color --first-parent -n1 --pretty=format:%ct`
                    echo "d1ec039f-f3db-468b-a508-896d7c382999 $VERSION" > ../package/updateinfo
                '''
            }
        }

        utils.stageWithTimeout('create installers', 30, 'MINUTES', XADir, true) {    // Typically takes less than 5 minutes
            if (isPr) {
                // Override _MSBUILD_ARGS to ensure we only package the `AndroidSupportedTargetJitAbis` which are built.
                // Also ensure that we don't require mono bundle components in the installer if this is not a full mono integration build.
                def msbuildInstallerArgs = hasPrLabelFullMonoIntegrationBuild ? '' : '/p:IncludeMonoBundleComponents=False'
                sh "make create-installers CONFIGURATION=${env.BuildFlavor} V=1 _MSBUILD_ARGS='${msbuildInstallerArgs}'"
            } else {
                sh "make create-installers CONFIGURATION=${env.BuildFlavor} V=1"
            }

            if (isCommercial) {
                sh "cp bin/Build*/xamarin.android*.pkg ${packagePath}"
                sh "cp bin/Build*/Xamarin.Android*.vsix ${packagePath}"
            }
        }

        utils.stageWithTimeout('package oss', 30, 'MINUTES', XADir, true) {    // Typically takes less than 5 minutes
            if (!isCommercial) {
                sh "make package-oss CONFIGURATION=${env.BuildFlavor} V=1"
            }
        }

        utils.stageWithTimeout('sign packages', 3, 'MINUTES', packageDir, false) {    // Typically takes less than 10 seconds
            if (isPr || !isCommercial || skipSigning) {
                echo "Skipping 'sign packages' stage. Packages are only signed for commercial CI builds. IsPr: ${isPr} / IsCommercial: ${isCommercial} / SkipSigning: ${skipSigning}"
                return
            }

            def packages = findFiles(glob: '*.pkg')
            def tmpPrefix = "/tmp/${env.JOB_NAME}"
            def tmpdir = sh (script: "mkdir -p ${tmpPrefix} && mktemp -d ${tmpPrefix}/XXXXXXXXX", returnStdout: true).trim()
            withCredentials([string(credentialsId: 'codesign_keychain_pw', variable: 'KEYCHAIN_PASSWORD')]) {
                for (pkg in packages) {
                    def tmp = "${tmpdir}/${pkg.name}"
                    sh("mv ${pkg} ${tmpdir}")
                    sh("security unlock-keychain -p ${env.KEYCHAIN_PASSWORD} login.keychain")
                    sh("/usr/bin/productsign -s \"Developer ID Installer: Xamarin Inc\" \"${tmp}\" \"${pkg}\"")
                }
            }
        }

        utils.stageWithTimeout('build tests', 30, 'MINUTES', XADir, true) {    // Typically takes less than 10 minutes
            if (skipTest) {
                echo "Skipping 'build tests' stage. Clear the SkipTest variable setting to build and run tests"
                return
            }

            sh "make all-tests CONFIGURATION=${env.BuildFlavor} V=1"
        }

        utils.stageWithTimeout('process build results', 10, 'MINUTES', XADir, true) {    // Typically takes less than a minute
            try {
                echo "processing build status"
                sh "make package-build-status CONFIGURATION=${env.BuildFlavor} V=1"

                if (isCommercial) {
                    sh "cp bin/Build${env.BuildFlavor}/xa-build-status-*.zip ${packagePath}"
                }
            } catch (error) {
                echo "ERROR : NON-FATAL : processBuildStatus: Unexpected error: ${error}"
            }
        }

        utils.stageWithTimeout('publish packages to Azure', 30, 'MINUTES', '', true, 3) {    // Typically takes less than a minute, but provide ample time in situations where logs may be quite large
            def publishRootDir = ''
            def publishBuildFilePaths = "${XADir}/xamarin.android-oss*.zip,${XADir}/bin/Build*/Xamarin.Android.Sdk-OSS*,${XADir}/build-status*,${XADir}/bin/Build${env.BuildFlavor}/xa-build-status*"
            if (isCommercial) {
                publishRootDir = packageDir
                publishBuildFilePaths = "xamarin.android*.pkg,Xamarin.Android*.vsix,build-status*,xa-build-status*,*updateinfo"
            }

            if (!isPr) {
                if (!isCommercial) {
                    publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/bin/${env.BuildFlavor}/bundle-*"
                }
            }

            dir(publishRootDir) {
                echo "publishBuildFilePaths: ${publishBuildFilePaths}"
                def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, storageVirtualPath, publishBuildFilePaths)
                if (commandStatus != 0) {
                    error "publish packages to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
                }
            }

            if (isCommercial) {
                utils.stageWithTimeout('report artifacts', 30, 'MINUTES', 'BuildTasks', false) {
                    withCredentials([string(credentialsId: "${env.GitHubAuthTokenCredentialId}", variable: 'GITHUB_AUTH_TOKEN'), usernamePassword(credentialsId: "${env.UserNamePasswordCredentialId}", passwordVariable: 'STORAGE_PASSWORD', usernameVariable: 'STORAGE_ACCOUNT')]) {
                        // Default search directory for Jenkins build artifacts is '${env.WORKSPACE}/package'
                        sh "mono tools/BuildTasks/build-tasks.exe artifacts -s ${env.WORKSPACE}/${XADir} -a ${env.STORAGE_ACCOUNT} -c ${env.STORAGE_PASSWORD} -u ${env.ContainerName}/${storageVirtualPath} -t ${env.GITHUB_AUTH_TOKEN}"
                    }
                }

                utils.stageWithTimeout('sign artifacts', 30, 'MINUTES', '', false) {
                    if (isPr || skipSigning) {
                        echo "Skipping 'sign artifacts' stage. Artifact signing is only performed for commercial CI builds. SkipSigning: ${skipSigning}"
                        return
                    }

                    // 'jenkins-internal artifacts' is the GitHub status context (name) used by build-tasks.exe
                    httpRequest httpMode: 'POST', ignoreSslErrors: true, responseHandle: 'NONE', url: "http://code-sign.guest.corp.microsoft.com:8080/job/sign-from-github-esrp/buildWithParameters?SIGN_TYPE=Real&REPO=${gitRepo}&COMMIT=${commit}&GITHUB_CONTEXT=jenkins-internal%20artifacts&FILES_TO_SIGN=%2E*%2Evsix"
                }
            }
        }

        utils.stageWithTimeout('run all tests', 360, 'MINUTES', XADir, false) {   // Typically takes 6hr
            if (skipTest) {
                echo "Skipping 'run all tests' stage. Clear the SkipTest variable setting to build and run tests"
                return
            }

            echo "running tests"

            def skipNunitTests = false

            if (isPr) {
                def hasPrLabelRunTestsRelease = utils.hasPrLabel(gitRepo, env.ghprbPullId, 'run-tests-release')
                skipNunitTests = hasPrLabelFullMonoIntegrationBuild || hasPrLabelRunTestsRelease
                echo "Run all tests: Labels on the PR: 'full-mono-integration-build' (${hasPrLabelFullMonoIntegrationBuild}) and/or 'run-tests-release' (${hasPrLabelRunTestsRelease})"
            }

            commandStatus = sh (script: "make run-all-tests CONFIGURATION=${env.BuildFlavor} V=1" + (skipNunitTests ? " SKIP_NUNIT_TESTS=1" : ""), returnStatus: true)
            if (commandStatus != 0) {
                error "run-all-tests FAILED, status: ${commandStatus}"     // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('publish test error logs to Azure', 30, 'MINUTES', '', false, 3) {  // Typically takes less than a minute, but provide ample time in situations where logs may be quite large
            if (skipTest) {
                echo "Skipping 'publish test error logs' stage. Clear the SkipTest variable setting to build and run tests"
                return
            }

            echo "packaging test error logs"

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

            def publishTestFilePaths = "${XADir}/bin/Test${env.BuildFlavor}/xa-test-results*,${XADir}/test-errors.zip"

            echo "publishTestFilePaths: ${publishTestFilePaths}"
            def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, storageVirtualPath, publishTestFilePaths)
            if (commandStatus != 0) {
                error "publish test error logs to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('Plot build & test metrics', 30, 'SECONDS', XADir, false, 3) {    // Typically takes less than a second
            if (isPr || skipTest) {
                echo "Skipping 'plot metrics' stage for PR build. IsPr: ${isPr} / SkipTest: ${skipTest}"
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
