package com.adaskothebeast.typewriter.rider

import java.nio.file.Paths

internal fun resolveGenerationProjectPath(
    configuredProjectPath: String?,
    projectPathOverride: String?,
    templatePath: String?,
): String? = configuredProjectPath
    ?: projectPathOverride
    ?: templatePath?.let { findNearestProjectPathForInput(it, requireUnique = true) }

internal fun findNearestProjectPathForInput(inputPath: String, requireUnique: Boolean = false): String? {
    if (inputPath.endsWith(".csproj", ignoreCase = true)) {
        return inputPath
    }

    var directory = Paths.get(inputPath).parent
    while (directory != null) {
        val projectFiles = directory.toFile()
            .listFiles { candidate -> candidate.isFile && candidate.name.endsWith(".csproj", ignoreCase = true) }
        if (!projectFiles.isNullOrEmpty()) {
            return if (requireUnique) {
                projectFiles.singleOrNull()?.path
            } else {
                projectFiles.minBy { it.name.lowercase() }.path
            }
        }

        directory = directory.parent
    }

    return null
}
