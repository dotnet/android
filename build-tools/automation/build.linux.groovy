// This file is based on the Jenkins scripted pipeline (as opposed to the declarative pipeline) syntax
// https://jenkins.io/doc/book/pipeline/syntax/#scripted-pipeline

def XADir = "xamarin-android"
def buildTarget = 'jenkins'
def chRootPackages = '''
    ant
    autoconf
    automake
    build-essential
    ca-certificates-mono
    clang
    cli-common-dev
    cmake
    curl
    debhelper
    devscripts
    fakeroot
    fsharp
    g++-mingw-w64
    g++-multilib
    gcc-mingw-w64
    gcc-multilib
    gettext
    git
    intltool
    lib32stdc++6
    lib32z1
    libc++-dev
    libgdk-pixbuf2.0-dev
    libncurses-dev
    libsqlite3-dev
    libtinfo-dev:i386
    libtool
    libtool-bin
    libz-mingw-w64-dev
    libzip-dev
    linux-libc-dev:i386
    mono-csharp-shell
    mono-devel
    msbuild
    ninja-build
    nuget
    p7zip-full
    pkg-config
    psmisc
    referenceassemblies-pcl
    ruby
    scons
    sqlite3
    unzip
    vim-common
    wget
    xauth
    xvfb
    xz-utils
    zip
    zlib1g-dev:i386
    zulu-8
'''
def isPr = false                // Default to CI
def isStable = false            // Stable build workflow
def publishPackages = false
def pBuilderBindMounts = null
def utils = null
def hasPrLabelFullMonoIntegrationBuild = false

prLabels = null             // Globally defined "static" list accessible within the hasPrLabel function

def execChRootCommand(chRootName, chRootPackages, pBuilderBindMounts, makeCommand) {
    chroot chrootName: chRootName,
        additionalPackages: chRootPackages,
        bindMounts: pBuilderBindMounts,
        command: """
            export LC_ALL=en_US.UTF-8
            export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games:/usr/local/games
            locale

            ${makeCommand}
            """
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

        utils.stageWithTimeout('init', 30, 'SECONDS', XADir, true) {    // Typically takes less than a second for CI builds
            // Note: PR plugin environment variable settings available here: https://wiki.jenkins.io/display/JENKINS/GitHub+pull+request+builder+plugin
            isPr = env.ghprbActualCommit != null
            isStable = env.IsStable == '1'
            publishPackages = env.PublishPackages == '1'
            def branch = isPr ? env.GIT_BRANCH : scmVars.GIT_BRANCH
            def commit = isPr ? env.ghprbActualCommit : scmVars.GIT_COMMIT

            def buildType = isPr ? 'PR' : 'CI'

            echo "HostName: ${env.NODE_NAME}"
            echo "Git repo: ${env.GitRepo}"     // Defined as an environment variable in the jenkins build definition
            echo "Job: ${env.JOB_BASE_NAME}"
            echo "Workspace: ${env.WORKSPACE}"
            echo "Branch: ${branch}"
            echo "Commit: ${commit}"
            echo "Build type: ${buildType}"
            echo "Stable build workflow: ${isStable}"

            pBuilderBindMounts = "/mnt/scratch"
            echo "pBuilderBindMounts: ${pBuilderBindMounts}"
            echo "chRootPackages: ${chRootPackages}"

            if (env.AdditionalPackages) {
                echo "AdditionalPackages (build configuration): ${env.AdditionalPackages}"
                chRootPackages = "${chRootPackages} ${env.AdditionalPackages}"
            }

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
                }
            }
            
            sh "env"
        }

        utils.stageWithTimeout('build and package', 7, 'HOURS', XADir, true) {    // Typically takes 4-5 hours
            execChRootCommand(env.ChRootName, chRootPackages, pBuilderBindMounts,
                            """
                                export AndroidToolchainDirectory=/mnt/scratch/android-toolchain
                                export AndroidToolchainCacheDirectory=/mnt/scratch/android-archives

                                if [ -z "\$JAVA_HOME" ]; then
                                    if [ -f /etc/profile.d/jdk.sh ]; then
                                        echo 'STAGE: jdk'
                                        source /etc/profile.d/jdk.sh
                                    fi
                                fi

                                echo 'STAGE: build'
                                make prepare ${buildTarget} CONFIGURATION=${env.BuildFlavor} V=1 NO_SUDO=true MSBUILD_ARGS='/p:MonoRequiredMinimumVersion=5.12' PREPARE_CI=1

                                if [[ "${isPr}" != "true" ]]; then
                                    echo 'STAGE: package deb'
                                    make package-deb CONFIGURATION=${env.BuildFlavor} V=1
                                else
                                    echo 'Skipping debian packaging for PR builds'
                                fi

                                if [[ "${isPr}" != "true" && "${isStable}" != "true" ]]; then
                                    echo 'STAGE: build tests'
                                    xvfb-run -a -- make all-tests CONFIGURATION=${env.BuildFlavor} V=1
                                else
                                    echo 'Skipping build tests for PR and stable builds'
                                fi

                                echo 'STAGE: package build status'
                                make package-build-status CONFIGURATION=${env.BuildFlavor}
                            """)
        }

        utils.stageWithTimeout('publish packages to Azure', 30, 'MINUTES', '', true, 3) {    // Typically takes less than a minute, but provide ample time in situations where logs may be quite large
            if (!publishPackages) {
                echo "Skipping package publishing. Set PublishPackages to 1 as a property setting in the build configuration "
                return
            }

            def publishBuildFilePaths = "${XADir}/*xamarin.android*.tar*";
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/bin/${env.BuildFlavor}/bundle-*.zip"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/*.changes"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/*.dsc"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/*.deb"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/build-status*"
            publishBuildFilePaths = "${publishBuildFilePaths},${XADir}/bin/Build${env.BuildFlavor}/xa-build-status*"

            echo "publishBuildFilePaths: ${publishBuildFilePaths}"
            def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, env.StorageVirtualPath, publishBuildFilePaths)
            if (commandStatus != 0) {
                error "publish packages to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('run all tests and package results', 360, 'MINUTES', XADir, false) {
            if (isPr || isStable) {
                echo "Skipping test run for PR and stable builds"
                return
            }

            echo "running tests"

            execChRootCommand(env.ChRootName, chRootPackages, pBuilderBindMounts,
                    """
                        export AndroidToolchainDirectory=/mnt/scratch/android-toolchain
                        export AndroidToolchainCacheDirectory=/mnt/scratch/android-archives

                        echo "STAGE: run all tests"
                        xvfb-run -a -- make run-all-tests CONFIGURATION=${env.BuildFlavor} V=1 || (killall adb && false)
                        killall adb || true

                        echo "STAGE: package test error logs"
                        make -C ${XADir} -k package-test-results CONFIGURATION=${env.BuildFlavor}
                    """)

            def publishTestFilePaths = "${XADir}/bin/Test${env.BuildFlavor}/xa-test-results*,${XADir}/test-errors.zip"

            echo "publishTestFilePaths: ${publishTestFilePaths}"
            def commandStatus = utils.publishPackages(env.StorageCredentialId, env.ContainerName, env.StorageVirtualPath, publishTestFilePaths)
            if (commandStatus != 0) {
                error "publish test error logs to Azure FAILED, status: ${commandStatus}"    // Ensure stage is labeled as 'failed' and red failure indicator is displayed in Jenkins pipeline steps view
            }
        }

        utils.stageWithTimeout('Publish test results', 5, 'MINUTES', XADir, false, 3) {    // Typically takes under 1 minute to publish test results
            if (isPr || isStable) {
                echo "Skipping publishing of test results for PR and stable builds"
                return
            }

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
