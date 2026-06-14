plugins {
    kotlin("jvm") version "2.2.21"
    id("org.jetbrains.intellij.platform") version "2.16.0"
}

group = providers.gradleProperty("pluginGroup").get()
version = providers.gradleProperty("pluginVersion").get()

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        rider(providers.gradleProperty("platformVersion").get()) {
            useInstaller = false
        }
        bundledModule("intellij.rider")
    }
}

kotlin {
    jvmToolchain(21)
}

intellijPlatform {
    buildSearchableOptions = false
    instrumentCode = false

    pluginConfiguration {
        id = "com.adaskothebeast.typewriter"
        name = providers.gradleProperty("pluginName")
        version = providers.gradleProperty("pluginVersion")
        description = """
            <p>JetBrains Rider adapter for Typewriter .tst templates.</p>
            <p>Provides file recognition, syntax highlighting, CLI-backed generation and validation actions, and save-time generation.</p>
        """.trimIndent()

        ideaVersion {
            sinceBuild = providers.gradleProperty("pluginSinceBuild")
            untilBuild = provider { null }
        }

        vendor {
            name = "AdaskoTheBeAsT"
            url = "https://github.com/AdaskoTheBeAsT"
        }
    }

    pluginVerification {
        ides {
            current()
        }
    }
}
