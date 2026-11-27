package com.adaskothebeast.typewriter.rider

import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.nio.file.Files

class TypewriterProjectPathsTest {
    private val workspace = Files.createTempDirectory("typewriter-rider-test").toFile()

    @After
    fun cleanUp() {
        workspace.deleteRecursively()
    }

    @Test
    fun `uses the saved template's nearest project even if it is not in the workspace solution`() {
        writeFile("First/First.csproj")
        writeFile("Second/Second.csproj")
        workspace.resolve("Workspace.slnx").writeText(
            """
            <Solution>
              <Project Path="First/First.csproj" />
              <Project Path="Second/Second.csproj" />
            </Solution>
            """.trimIndent(),
        )
        writeFile("Samples/Samples.csproj")
        val projectPath = writeFile("Samples/Api/Api.csproj")
        val templatePath = writeFile("Samples/Api/Templates/Models.tst")

        assertEquals(projectPath, resolveGenerationProjectPath(null, null, templatePath))
    }

    @Test
    fun `prefers a configured project to the template's project`() {
        val configuredProjectPath = writeFile("Contracts/Contracts.csproj")
        writeFile("Api/Api.csproj")
        val templatePath = writeFile("Api/Models.tst")

        assertEquals(configuredProjectPath, resolveGenerationProjectPath(configuredProjectPath, null, templatePath))
    }

    @Test
    fun `defers to workspace selection when the nearest directory contains multiple projects`() {
        writeFile("Root.csproj")
        writeFile("Api/Api.csproj")
        writeFile("Api/Other.csproj")
        val templatePath = writeFile("Api/Models.tst")

        assertNull(resolveGenerationProjectPath(null, null, templatePath))
    }

    @Test
    fun `does not pick a sibling project for a shared template`() {
        writeFile("Api/Api.csproj")
        writeFile("Contracts/Contracts.csproj")
        val templatePath = writeFile("Shared/Models.tst")

        assertNull(resolveGenerationProjectPath(null, null, templatePath))
    }

    @Test
    fun `preserves the saved source's project and leaves all-template commands unscoped otherwise`() {
        val projectPath = writeFile("Api/Api.csproj")
        val inputPath = writeFile("Api/Models/Customer.cs")

        assertEquals(projectPath, resolveGenerationProjectPath(null, findNearestProjectPathForInput(inputPath), null))
        assertEquals(projectPath, findNearestProjectPathForInput(projectPath))
        assertNull(resolveGenerationProjectPath(null, null, null))
    }

    private fun writeFile(relativePath: String): String = workspace.resolve(relativePath).apply {
        parentFile.mkdirs()
        writeText("")
    }.path
}
