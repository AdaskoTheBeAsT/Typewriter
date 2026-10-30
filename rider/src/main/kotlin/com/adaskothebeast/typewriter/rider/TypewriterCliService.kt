package com.adaskothebeast.typewriter.rider

import com.adaskothebeast.typewriter.rider.TypewriterLanguageServerClient.PersistentGenerationRequest
import com.google.gson.JsonParseException
import com.google.gson.JsonParser
import com.intellij.execution.ExecutionException
import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.execution.process.CapturingProcessHandler
import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.util.Alarm
import com.intellij.util.execution.ParametersListUtil
import java.io.IOException
import java.nio.charset.StandardCharsets
import java.nio.file.FileVisitResult
import java.nio.file.Files
import java.nio.file.InvalidPathException
import java.nio.file.Path
import java.nio.file.Paths
import java.nio.file.SimpleFileVisitor
import java.nio.file.attribute.BasicFileAttributes

@Service(Service.Level.PROJECT)
class TypewriterCliService(private val project: Project) {
    private val saveLock = Any()
    private val saveAlarm = Alarm(Alarm.ThreadToUse.POOLED_THREAD, project)
    private var pendingSaveRequest: SaveRequest? = null
    private var saveRunning = false
    private var lastSaveAt = 0L

    fun generateCurrentTemplate(file: VirtualFile?) {
        executeCurrentTemplate("generate", "Generate current template", file)
    }

    fun validateCurrentTemplate(file: VirtualFile?) {
        executeCurrentTemplate("validate", "Validate current template", file)
    }

    fun generateAllTemplates() {
        execute("generate", "Generate all Typewriter templates", templatePath = null, allTemplates = true)
    }

    fun handleSavedFile(file: VirtualFile) {
        val request = createSaveRequest(file) ?: return
        scheduleSaveRequest(request)
    }

    fun isIgnoredInputPath(filePath: String): Boolean {
        if (filePath.isBlank()) {
            return true
        }

        val fullPath = try {
            Paths.get(filePath).toAbsolutePath().normalize()
        } catch (_: InvalidPathException) {
            return true
        }

        val rootDirectory = resolveInputRootDirectory(fullPath)
        val excludePatterns = readConfiguredExcludePatterns(fullPath.toString())
        return isPathExcluded(rootDirectory, fullPath, excludePatterns)
    }

    private fun createSaveRequest(file: VirtualFile): SaveRequest? {
        val settings = project.service<TypewriterSettingsState>().settings
        if (!settings.generateOnSave && !settings.validateOnSave) {
            return null
        }

        if (isIgnoredInputPath(file.path)) {
            return null
        }

        val extension = normalizeExtension(file.extension ?: return null).lowercase()
        if (extension !in readConfiguredInputExtensions(file.path)) {
            return null
        }

        if (extension == ".tst") {
            return when {
                settings.generateOnSave -> SaveRequest("generate", "Generate saved Typewriter template", file.path, allTemplates = false, projectPath = null, projectScoped = false)
                settings.validateOnSave -> SaveRequest("validate", "Validate saved Typewriter template", file.path, allTemplates = false, projectPath = null, projectScoped = false)
                else -> null
            }
        }

        val projectPath = findNearestProjectPathForInput(file)
        return when {
            settings.generateOnSave -> SaveRequest("generate", "Generate Typewriter templates after save", templatePath = null, allTemplates = true, projectPath = projectPath, projectScoped = true)
            settings.validateOnSave -> SaveRequest("validate", "Validate Typewriter templates after save", templatePath = null, allTemplates = true, projectPath = projectPath, projectScoped = true)
            else -> null
        }
    }

    private fun scheduleSaveRequest(request: SaveRequest) {
        synchronized(saveLock) {
            pendingSaveRequest = SaveRequest.merge(pendingSaveRequest, request)
            lastSaveAt = System.currentTimeMillis()
        }

        saveAlarm.cancelAllRequests()
        saveAlarm.addRequest({ processSaveRequests() }, SaveDebounceMillis)
    }

    private fun processSaveRequests() {
        synchronized(saveLock) {
            if (saveRunning) {
                return
            }

            saveRunning = true
        }

        try {
            while (true) {
                waitForSaveQuietPeriod()
                val request = synchronized(saveLock) {
                    val current = pendingSaveRequest
                    pendingSaveRequest = null
                    current
                } ?: return

                runCli(request.command, request.title, request.templatePath, request.allTemplates, request.projectPath, request.projectScoped)
            }
        } finally {
            synchronized(saveLock) {
                saveRunning = false
                if (pendingSaveRequest != null) {
                    saveAlarm.cancelAllRequests()
                    saveAlarm.addRequest({ processSaveRequests() }, SaveDebounceMillis)
                }
            }
        }
    }

    private fun waitForSaveQuietPeriod() {
        while (true) {
            val remaining = synchronized(saveLock) {
                SaveDebounceMillis - (System.currentTimeMillis() - lastSaveAt)
            }
            if (remaining <= 0L) {
                return
            }

            Thread.sleep(remaining)
        }
    }

    private fun executeCurrentTemplate(command: String, title: String, file: VirtualFile?) {
        val settings = project.service<TypewriterSettingsState>().settings
        val workspacePath = resolveWorkspacePath(file?.path, settings.workspacePath)
        val configuredTemplatePath = workspacePath?.let {
            resolveOptionalPath(settings.templatePath, getPathBase(it).toString())
        }
        val templatePath = file?.path ?: configuredTemplatePath
        if (templatePath.isNullOrBlank()) {
            notify("Typewriter", "Open a .tst file or configure Typewriter template path.", NotificationType.WARNING)
            return
        }

        execute(command, title, templatePath, allTemplates = false)
    }

    private fun execute(
        command: String,
        title: String,
        templatePath: String?,
        allTemplates: Boolean,
        projectPathOverride: String? = null,
        projectScoped: Boolean = false,
    ) {
        object : Task.Backgroundable(project, "Typewriter: $title", false) {
            override fun run(indicator: ProgressIndicator) {
                indicator.text = "Running Typewriter CLI"
                runCli(command, title, templatePath, allTemplates, projectPathOverride, projectScoped)
            }
        }.queue()
    }

    private fun runCli(
        command: String,
        title: String,
        templatePath: String?,
        allTemplates: Boolean,
        projectPathOverride: String? = null,
        projectScoped: Boolean = false,
    ) {
        val settings = project.service<TypewriterSettingsState>().settings
        val workspacePath = resolveWorkspacePath(templatePath ?: projectPathOverride, settings.workspacePath)
        if (workspacePath.isNullOrBlank()) {
            notify("Typewriter", "Typewriter requires an open project or configured workspace path.", NotificationType.ERROR)
            return
        }

        try {
            val workingDirectory = getPathBase(workspacePath).toString()
            val projectPath = resolveOptionalPath(settings.projectPath, getPathBase(workspacePath).toString()) ?: projectPathOverride
            val templateSearchPath = if (projectScoped) resolveProjectTemplateSearchPath(projectPath) else null
            if (allTemplates && templatePath.isNullOrBlank() && templateSearchPath == null && shouldSkipWithoutTemplates(projectPath, workspacePath)) {
                if (!projectScoped) {
                    notify("Typewriter", "No .tst templates were found in the current solution or workspace.", NotificationType.INFORMATION)
                }
                return
            }

            val persistentResult = project.service<TypewriterLanguageServerClient>().tryGenerate(
                request = PersistentGenerationRequest(
                    command = command,
                    workspacePath = workspacePath,
                    projectPath = projectPath,
                    templatePath = if (allTemplates) null else templatePath,
                    templateSearchPath = templateSearchPath,
                    framework = settings.framework.ifBlank { null },
                    allProjects = allTemplates && settings.allProjects,
                ),
                workingDirectory = workingDirectory,
            )
            if (persistentResult != null) {
                val success = persistentResult.get("success")?.asBoolean == true
                val message = summarizePersistentResult(persistentResult)
                notify(
                    title,
                    message.ifBlank { "Typewriter completed." },
                    if (success) NotificationType.INFORMATION else NotificationType.WARNING,
                )
                return
            }

            val invocation = resolveCliInvocation(settings, workingDirectory)
            val arguments = invocation.arguments.toMutableList()
            arguments += command
            arguments += "--workspace"
            arguments += workspacePath
            arguments += "--output"
            arguments += "text"

            if (!projectPath.isNullOrBlank()) {
                arguments += "--project"
                arguments += projectPath
            }

            if (!templatePath.isNullOrBlank() && !allTemplates) {
                arguments += "--template"
                arguments += templatePath
            }

            if (!templateSearchPath.isNullOrBlank()) {
                arguments += "--template-search-path"
                arguments += templateSearchPath
            }

            if (settings.framework.isNotBlank()) {
                arguments += "--framework"
                arguments += settings.framework
            }

            if (allTemplates && settings.allProjects) {
                arguments += "--all-projects"
            }

            val commandLine = GeneralCommandLine(invocation.command)
                .withParameters(arguments)
                .withWorkDirectory(workingDirectory)
                .withCharset(StandardCharsets.UTF_8)
            val output = CapturingProcessHandler(commandLine).runProcess(ProcessTimeoutMillis)
            val message = summarize(output.stdout, output.stderr, output.isTimeout)
            when {
                output.isTimeout -> notify(title, message, NotificationType.ERROR)
                output.exitCode == 0 -> notify(title, message.ifBlank { "Typewriter completed." }, NotificationType.INFORMATION)
                else -> notify(title, message.ifBlank { "Typewriter CLI failed with exit code ${output.exitCode}." }, NotificationType.WARNING)
            }
        } catch (ex: ExecutionException) {
            notify(title, ex.message ?: "Unable to start Typewriter CLI.", NotificationType.ERROR)
        } catch (ex: RuntimeException) {
            notify(title, ex.message ?: "Typewriter CLI failed.", NotificationType.ERROR)
        }
    }

    private fun resolveCliInvocation(settings: TypewriterSettingsState.Settings, workingDirectory: String): CliInvocation {
        if (settings.cliPath.isNotBlank()) {
            return CliInvocation(settings.cliPath, parseArguments(settings.cliArguments))
        }

        val localCliProject = findLocalCliProject(project.basePath) ?: findLocalCliProject(workingDirectory)
        if (localCliProject != null) {
            return CliInvocation("dotnet", listOf("run", "--project", localCliProject, "--"))
        }

        val packagedCliInvocation = findPackagedCliInvocation()
        if (packagedCliInvocation != null) {
            return packagedCliInvocation
        }

        return CliInvocation("typewriter", emptyList())
    }

    private fun parseArguments(value: String): List<String> =
        if (value.isBlank()) {
            emptyList()
        } else {
            ParametersListUtil.parse(value)
        }

    private fun findLocalCliProject(startPath: String?): String? {
        if (startPath.isNullOrBlank()) {
            return null
        }

        val start = Paths.get(startPath)
        val candidates = listOf(
            start.resolve("src/Typewriter.Cli/Typewriter.Cli.csproj"),
            start.resolve("../src/Typewriter.Cli/Typewriter.Cli.csproj"),
        )

        return candidates
            .map { it.normalize() }
            .firstOrNull { Files.exists(it) }
            ?.toString()
    }

    private fun findPackagedCliInvocation(): CliInvocation? {
        val plugin = PluginManagerCore.getPlugin(PluginId.getId(PluginIdValue)) ?: return null
        val cliAssembly = plugin.pluginPath
            .resolve("tools")
            .resolve("typewriter-cli")
            .resolve("Typewriter.Cli.dll")

        return if (Files.isRegularFile(cliAssembly)) {
            CliInvocation("dotnet", listOf(cliAssembly.toString()))
        } else {
            null
        }
    }

    private fun resolveWorkspacePath(templatePath: String?, configuredWorkspacePath: String): String? {
        if (configuredWorkspacePath.isNotBlank()) {
            val base = project.basePath ?: templatePath?.let { Paths.get(it).parent?.toString() } ?: "."
            return resolveOptionalPath(configuredWorkspacePath, base)
        }

        return project.basePath ?: templatePath?.let { Paths.get(it).parent?.toString() }
    }

    private fun resolveOptionalPath(value: String, basePath: String): String? {
        if (value.isBlank()) {
            return null
        }

        val path = Paths.get(value)
        return if (path.isAbsolute) {
            path.normalize().toString()
        } else {
            Paths.get(basePath).resolve(path).normalize().toString()
        }
    }

    private fun getPathBase(value: String): Path {
        val path = Paths.get(value)
        return when {
            Files.isDirectory(path) -> path
            path.parent != null -> path.parent
            else -> path
        }
    }

    private fun findNearestProjectPathForInput(file: VirtualFile): String? {
        if (file.extension.equals("csproj", ignoreCase = true)) {
            return file.path
        }

        var directory = Paths.get(file.path).parent
        while (directory != null) {
            val projectFile = directory.toFile()
                .listFiles { candidate -> candidate.isFile && candidate.name.endsWith(".csproj", ignoreCase = true) }
                ?.sortedBy { it.name.lowercase() }
                ?.firstOrNull()
            if (projectFile != null) {
                return projectFile.toPath().toString()
            }

            directory = directory.parent
        }

        return null
    }

    private fun resolveProjectTemplateSearchPath(projectPath: String?): String? {
        if (projectPath.isNullOrBlank()) {
            return null
        }

        val projectDirectory = try {
            Paths.get(projectPath).parent ?: return null
        } catch (_: InvalidPathException) {
            return null
        }

        return if (containsTemplateFile(projectDirectory)) projectDirectory.toString() else null
    }

    private fun shouldSkipWithoutTemplates(projectPath: String?, workspacePath: String?): Boolean {
        val checkedScopes = mutableSetOf<String>()
        for (candidatePath in listOf(projectPath, workspacePath)) {
            val (scopeKey, containsTemplates) = tryGetScopeAndContainsTemplates(candidatePath) ?: continue
            if (!checkedScopes.add(scopeKey.lowercase())) {
                continue
            }

            if (containsTemplates) {
                return false
            }
        }

        return checkedScopes.isNotEmpty()
    }

    private fun tryGetScopeAndContainsTemplates(candidatePath: String?): Pair<String, Boolean>? {
        val solutionPath = tryResolveSolutionFile(candidatePath)
        if (solutionPath != null) {
            return solutionPath.toString() to containsSolutionTemplateFile(solutionPath)
        }

        val searchDirectory = tryGetSearchDirectory(candidatePath) ?: return null
        return searchDirectory.toString() to containsTemplateFile(searchDirectory)
    }

    private fun tryGetSearchDirectory(candidatePath: String?): Path? {
        if (candidatePath.isNullOrBlank()) {
            return null
        }

        val fullPath = try {
            Paths.get(candidatePath).toAbsolutePath().normalize()
        } catch (_: InvalidPathException) {
            return null
        }

        val directory = if (Files.isDirectory(fullPath)) fullPath else fullPath.parent
        return directory?.takeIf { Files.isDirectory(it) }
    }

    private fun tryResolveSolutionFile(candidatePath: String?): Path? {
        if (candidatePath.isNullOrBlank()) {
            return null
        }

        val fullPath = try {
            Paths.get(candidatePath).toAbsolutePath().normalize()
        } catch (_: InvalidPathException) {
            return null
        }

        if (isSolutionFileName(fullPath.fileName?.toString().orEmpty()) && Files.isRegularFile(fullPath)) {
            return fullPath
        }

        if (!Files.isDirectory(fullPath)) {
            return null
        }

        val solutions = fullPath.toFile()
            .listFiles { file -> file.isFile && isSolutionFileName(file.name) }
            ?: return null
        return if (solutions.size == 1) solutions[0].toPath().toAbsolutePath().normalize() else null
    }

    private fun isSolutionFileName(fileName: String): Boolean =
        fileName.endsWith(".sln", ignoreCase = true) || fileName.endsWith(".slnx", ignoreCase = true)

    private fun containsSolutionTemplateFile(solutionPath: Path): Boolean {
        val solutionDirectory = solutionPath.parent ?: return false
        if (!Files.isDirectory(solutionDirectory)) {
            return false
        }

        val excludePatterns = readConfiguredExcludePatterns(solutionDirectory.toString())
        val topLevelTemplates = solutionDirectory.toFile()
            .listFiles { file ->
                file.isFile &&
                    file.name.endsWith(".tst", ignoreCase = true) &&
                    !isPathExcluded(solutionDirectory, file.toPath(), excludePatterns)
            }
        if (!topLevelTemplates.isNullOrEmpty()) {
            return true
        }

        for (referencedPath in getSolutionReferencedPaths(solutionPath)) {
            if (!Files.exists(referencedPath)) {
                continue
            }

            if (Files.isDirectory(referencedPath)) {
                if (!isExcludedDirectory(solutionDirectory, referencedPath, excludePatterns) &&
                    containsTemplateFile(referencedPath, solutionDirectory, excludePatterns)
                ) {
                    return true
                }
                continue
            }

            val fileName = referencedPath.fileName?.toString().orEmpty()
            if (fileName.endsWith(".tst", ignoreCase = true) &&
                !isPathExcluded(solutionDirectory, referencedPath, excludePatterns)
            ) {
                return true
            }

            if (fileName.endsWith(".csproj", ignoreCase = true)) {
                val projectDirectory = referencedPath.parent
                if (projectDirectory != null &&
                    Files.isDirectory(projectDirectory) &&
                    !isExcludedDirectory(solutionDirectory, projectDirectory, excludePatterns) &&
                    containsTemplateFile(projectDirectory, solutionDirectory, excludePatterns)
                ) {
                    return true
                }
            }
        }

        return false
    }

    private fun getSolutionReferencedPaths(solutionPath: Path): List<Path> {
        val solutionDirectory = solutionPath.parent ?: return emptyList()
        val results = linkedSetOf<Path>()
        try {
            val content = Files.readString(solutionPath, StandardCharsets.UTF_8)
            if (solutionPath.fileName.toString().endsWith(".slnx", ignoreCase = true)) {
                for (match in SlnxPathRegex.findAll(content)) {
                    val candidate = match.groupValues.getOrNull(1)?.trim().orEmpty()
                    if (candidate.isNotBlank()) {
                        results.add(solutionDirectory.resolve(candidate.replace('\\', '/')).normalize())
                    }
                }
            } else {
                var inSolutionItems = false
                for (rawLine in content.lineSequence()) {
                    val line = rawLine.trim()
                    when {
                        line.startsWith("Project(") -> {
                            val parts = line.split('"')
                            if (parts.size > 5 && parts[5].isNotBlank()) {
                                results.add(solutionDirectory.resolve(parts[5].trim().replace('\\', '/')).normalize())
                            }
                        }
                        line.startsWith("ProjectSection(SolutionItems)") -> inSolutionItems = true
                        inSolutionItems && line.startsWith("EndProjectSection") -> inSolutionItems = false
                        inSolutionItems -> {
                            val candidate = line.substringBefore('=').trim().trim('"')
                            if (candidate.isNotBlank()) {
                                results.add(solutionDirectory.resolve(candidate.replace('\\', '/')).normalize())
                            }
                        }
                    }
                }
            }
        } catch (_: IOException) {
        } catch (_: InvalidPathException) {
        }

        return results.toList()
    }

    private fun containsTemplateFile(
        root: Path,
        excludeRoot: Path = root,
        excludePatterns: List<String> = readConfiguredExcludePatterns(excludeRoot.toString()),
    ): Boolean {
        var found = false
        return try {
            Files.walkFileTree(root, object : SimpleFileVisitor<Path>() {
                override fun preVisitDirectory(dir: Path, attrs: BasicFileAttributes): FileVisitResult {
                    if (dir != root && isExcludedDirectory(excludeRoot, dir, excludePatterns)) {
                        return FileVisitResult.SKIP_SUBTREE
                    }

                    return FileVisitResult.CONTINUE
                }

                override fun visitFile(file: Path, attrs: BasicFileAttributes): FileVisitResult {
                    if (file.fileName.toString().endsWith(".tst", ignoreCase = true) &&
                        !isPathExcluded(excludeRoot, file, excludePatterns)
                    ) {
                        found = true
                        return FileVisitResult.TERMINATE
                    }

                    return FileVisitResult.CONTINUE
                }

                override fun visitFileFailed(file: Path, exc: IOException): FileVisitResult =
                    FileVisitResult.CONTINUE
            })

            found
        } catch (_: IOException) {
            false
        } catch (_: SecurityException) {
            false
        }
    }

    private fun resolveInputRootDirectory(fullPath: Path): Path {
        val basePath = project.basePath
        if (!basePath.isNullOrBlank()) {
            try {
                val baseDir = Paths.get(basePath).toAbsolutePath().normalize()
                if (fullPath.startsWith(baseDir)) {
                    return baseDir
                }
            } catch (_: InvalidPathException) {
            }
        }

        var current: Path? = if (Files.isDirectory(fullPath)) fullPath else fullPath.parent
        var nearestProjectDirectory: Path? = null
        var nearestWorkspaceDirectory: Path? = null
        while (current != null) {
            val directoryFile = current.toFile()
            val files = directoryFile.listFiles()
            if (files != null) {
                if (nearestProjectDirectory == null && files.any { it.isFile && it.name.endsWith(".csproj", ignoreCase = true) }) {
                    nearestProjectDirectory = current
                }

                val hasConfig = ConfigurationFileNames.any { Files.exists(current.resolve(it)) }
                val hasSolution = files.any { it.isFile && isSolutionFileName(it.name) }
                val hasGit = Files.exists(current.resolve(".git"))
                if (hasConfig || hasSolution || hasGit) {
                    nearestWorkspaceDirectory = current
                    if (hasSolution || hasGit) {
                        break
                    }
                }
            }

            current = current.parent
        }

        return nearestWorkspaceDirectory
            ?: nearestProjectDirectory
            ?: fullPath.root
            ?: fullPath.parent
            ?: fullPath
    }

    private fun isExcludedDirectory(
        rootDirectory: Path,
        directory: Path,
        excludePatterns: List<String>,
    ): Boolean {
        val name = directory.fileName?.toString()?.lowercase().orEmpty()
        if (name in AlwaysIgnoredDirectories) {
            return true
        }

        return isPathExcluded(rootDirectory, directory.resolve("_"), excludePatterns)
    }

    private fun isPathExcluded(
        rootDirectory: Path,
        targetPath: Path,
        excludePatterns: List<String>,
    ): Boolean {
        val relativePath = tryGetRelativePath(rootDirectory, targetPath) ?: return false
        if (relativePath == ".") {
            return false
        }

        val segments = relativePath.split('/').filter { it.isNotEmpty() }
        if (segments.any { it.lowercase() in AlwaysIgnoredDirectories }) {
            return true
        }

        for (pattern in excludePatterns) {
            val normalizedPattern = normalizeExcludePattern(pattern)
            if (normalizedPattern.isNotEmpty() && isGlobMatch(relativePath, normalizedPattern)) {
                return true
            }
        }

        return false
    }

    private fun tryGetRelativePath(rootDirectory: Path, targetPath: Path): String? {
        val fullRoot = rootDirectory.toAbsolutePath().normalize()
        val fullTarget = targetPath.toAbsolutePath().normalize()
        if (fullTarget == fullRoot) {
            return "."
        }

        if (!fullTarget.startsWith(fullRoot)) {
            return null
        }

        return fullRoot.relativize(fullTarget).toString().replace('\\', '/')
    }

    private fun normalizeExcludePattern(pattern: String): String {
        val trimmed = pattern.trim()
        if (trimmed.isEmpty()) {
            return ""
        }

        val endsWithSeparator = trimmed.endsWith("/") || trimmed.endsWith("\\")
        val normalized = trimmed.replace('\\', '/').trimEnd('/')
        if (normalized.isEmpty()) {
            return ""
        }

        if ('/' !in normalized) {
            return if (isDirectorySegmentPattern(normalized)) "**/$normalized/**" else normalized
        }

        if (endsWithSeparator) {
            return if (normalized.endsWith("/**")) normalized else "$normalized/**"
        }

        val lastSegment = normalized.substringAfterLast('/')
        return if (isDirectorySegmentPattern(lastSegment)) "$normalized/**" else normalized
    }

    private fun isDirectorySegmentPattern(segment: String): Boolean =
        segment.isNotEmpty() &&
            '*' !in segment &&
            '?' !in segment &&
            segment.indexOf('.', startIndex = 1) < 0

    private fun isGlobMatch(relativePath: String, normalizedPattern: String): Boolean {
        val regexBuilder = StringBuilder("^")
        var index = 0
        while (index < normalizedPattern.length) {
            val current = normalizedPattern[index]
            if (current == '*') {
                val isDoubleStar = index + 1 < normalizedPattern.length && normalizedPattern[index + 1] == '*'
                if (isDoubleStar) {
                    val hasSlashAfter = index + 2 < normalizedPattern.length && normalizedPattern[index + 2] == '/'
                    if (hasSlashAfter) {
                        regexBuilder.append("(?:.*/)?")
                        index += 3
                        continue
                    }

                    regexBuilder.append(".*")
                    index += 2
                    continue
                }

                regexBuilder.append("[^/]*")
                index += 1
                continue
            }

            if (current == '?') {
                regexBuilder.append("[^/]")
                index += 1
                continue
            }

            regexBuilder.append(Regex.escape(current.toString()))
            index += 1
        }

        regexBuilder.append("$")
        return Regex(regexBuilder.toString(), RegexOption.IGNORE_CASE).matches(relativePath)
    }

    private fun readConfiguredExcludePatterns(filePath: String): List<String> {
        var patterns = DefaultExcludePatterns
        for (configurationPath in findConfigurationFiles(filePath)) {
            val configuredPatterns = readExcludePatterns(configurationPath)
            if (configuredPatterns != null) {
                patterns = configuredPatterns
            }
        }

        val envExclude = System.getenv("TYPEWRITER_EXCLUDE")
        if (!envExclude.isNullOrBlank()) {
            val envPatterns = envExclude
                .split(',', ';', ' ')
                .map { it.trim() }
                .filter { it.isNotEmpty() }
            if (envPatterns.isNotEmpty()) {
                patterns = envPatterns
            }
        }

        return patterns
    }

    private fun readExcludePatterns(configurationPath: Path): List<String>? {
        return try {
            val root = Files.newBufferedReader(configurationPath).use(JsonParser::parseReader)
            if (!root.isJsonObject) {
                return null
            }

            val excludeArray = root.asJsonObject
                .entrySet()
                .firstOrNull { it.key.equals("exclude", ignoreCase = true) }
                ?.value
                ?.takeIf { it.isJsonArray }
                ?: return null
            excludeArray.asJsonArray
                .asSequence()
                .filter { it.isJsonPrimitive && it.asJsonPrimitive.isString }
                .map { it.asString.trim() }
                .filter { it.isNotEmpty() }
                .toList()
        } catch (_: IOException) {
            null
        } catch (_: JsonParseException) {
            null
        }
    }

    private fun readConfiguredInputExtensions(filePath: String): Set<String> {
        var extensions = DefaultInputExtensions
        for (configurationPath in findConfigurationFiles(filePath)) {
            val configuredExtensions = readInputExtensions(configurationPath)
            if (configuredExtensions != null) {
                extensions = configuredExtensions
            }
        }

        return extensions.map { it.lowercase() }.toSet()
    }

    private fun findConfigurationFiles(filePath: String): List<Path> {
        var directory: Path? = Paths.get(filePath)
        if (directory != null && !Files.isDirectory(directory)) {
            directory = directory?.parent
        }

        val directories = mutableListOf<Path>()
        while (directory != null) {
            directories.add(directory)
            directory = directory.parent
        }

        return directories
            .asReversed()
            .flatMap { candidateDirectory -> ConfigurationFileNames.map { candidateDirectory.resolve(it) } }
            .filter { Files.exists(it) }
    }

    private fun readInputExtensions(configurationPath: Path): Set<String>? {
        return try {
            val root = Files.newBufferedReader(configurationPath).use(JsonParser::parseReader)
            if (!root.isJsonObject) {
                return null
            }

            val inputExtensions = root.asJsonObject
                .entrySet()
                .firstOrNull { it.key.equals("inputExtensions", ignoreCase = true) }
                ?.value
                ?.takeIf { it.isJsonArray }
                ?: return null
            val extensions = inputExtensions.asJsonArray
                .asSequence()
                .filter { it.isJsonPrimitive && it.asJsonPrimitive.isString }
                .map { normalizeExtension(it.asString) }
                .filter { it.isNotBlank() }
                .toSet()

            extensions.ifEmpty { DefaultInputExtensions }
        } catch (_: IOException) {
            null
        } catch (_: JsonParseException) {
            null
        }
    }

    private fun normalizeExtension(extension: String): String {
        val trimmed = extension.trim()
        return when {
            trimmed.isBlank() -> ""
            trimmed.startsWith(".") -> trimmed
            else -> ".$trimmed"
        }
    }

    private fun summarize(stdout: String, stderr: String, timeout: Boolean): String {
        val prefix = if (timeout) "Typewriter CLI timed out.\n" else ""
        val text = listOf(stdout.trim(), stderr.trim()).filter { it.isNotBlank() }.joinToString("\n")
        val summary = prefix + text
        return if (summary.length <= MaxNotificationLength) {
            summary
        } else {
            summary.take(MaxNotificationLength) + "..."
        }
    }

    private fun summarizePersistentResult(result: com.google.gson.JsonObject): String {
        val lines = mutableListOf<String>()
        result.getAsJsonArray("generatedFiles")?.forEach { element ->
            val file = element.asJsonObject
            val status = if (file.get("changed")?.asBoolean == true) "updated" else "unchanged"
            lines += "$status: ${file.get("path")?.asString.orEmpty()}"
        }
        result.getAsJsonArray("diagnostics")?.forEach { element ->
            val diagnostic = element.asJsonObject
            val severity = diagnostic.get("severity")?.asString ?: "diagnostic"
            val code = diagnostic.get("code")?.takeUnless { it.isJsonNull }?.asString?.let { " $it" }.orEmpty()
            lines += "$severity$code: ${diagnostic.get("message")?.asString.orEmpty()}"
        }

        if (lines.isEmpty()) {
            val duration = result.get("durationMs")?.asLong
            return if (duration == null) "Typewriter completed." else "Typewriter completed in $duration ms."
        }

        val summary = lines.joinToString("\n")
        return if (summary.length <= MaxNotificationLength) summary else summary.take(MaxNotificationLength) + "..."
    }

    private fun notify(title: String, message: String, type: NotificationType) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup(NotificationGroupId)
            .createNotification(title, message, type)
            .notify(project)
    }

    private data class CliInvocation(val command: String, val arguments: List<String>)
    private data class SaveRequest(
        val command: String,
        val title: String,
        val templatePath: String?,
        val allTemplates: Boolean,
        val projectPath: String?,
        val projectScoped: Boolean,
    ) {
        companion object {
            fun merge(existing: SaveRequest?, incoming: SaveRequest): SaveRequest =
                if (existing?.allTemplates == true && !incoming.allTemplates) {
                    existing
                } else {
                    incoming
                }
        }
    }

    private companion object {
        const val PluginIdValue = "com.adaskothebeast.typewriter"
        const val NotificationGroupId = "Typewriter"
        const val ProcessTimeoutMillis = 300_000
        const val MaxNotificationLength = 1_000
        const val SaveDebounceMillis = 300
        val ConfigurationFileNames = listOf("typewriter.json", "typewriter.config.json", ".typewriterrc.json")
        val DefaultInputExtensions = setOf(".cs", ".csproj", ".json", ".props", ".sln", ".slnx", ".targets", ".tst")
        val DefaultExcludePatterns = listOf("**/bin/**", "**/obj/**", "**/node_modules/**")
        val AlwaysIgnoredDirectories = setOf(".git", ".gradle", ".idea", ".vs", ".vscode")
        val SlnxPathRegex = Regex("""<\s*(?:Project|File)\b[^>]*\bPath\s*=\s*["']([^"']+)["']""", RegexOption.IGNORE_CASE)
    }
}
