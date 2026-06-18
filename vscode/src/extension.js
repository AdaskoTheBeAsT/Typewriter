"use strict";

const childProcess = require("child_process");
const fs = require("fs");
const path = require("path");
const vscode = require("vscode");

let diagnostics;
let languageClient;
let outputChannel;

async function activate(context) {
    outputChannel = vscode.window.createOutputChannel("Typewriter");
    diagnostics = vscode.languages.createDiagnosticCollection("typewriter");

    context.subscriptions.push(outputChannel, diagnostics);
    context.subscriptions.push(
        vscode.commands.registerCommand("typewriter.generate", uri => executeCurrentTemplate(context, "generate", undefined, toFileUri(uri))),
        vscode.commands.registerCommand("typewriter.generateAll", uri => executeGenerateAll(context, toFileUri(uri))),
        vscode.commands.registerCommand("typewriter.validate", uri => executeCurrentTemplate(context, "validate", undefined, toFileUri(uri))),
        vscode.commands.registerCommand("typewriter.restartServer", () => restartLanguageServer(context)),
        createFallbackCompletionProvider(),
        vscode.workspace.onDidSaveTextDocument(document => handleDocumentSaved(context, document)));
    context.subscriptions.push({
        dispose: () => {
            void stopLanguageServer();
        },
    });

    await startLanguageServer(context);
}

async function deactivate() {
    if (diagnostics) {
        diagnostics.clear();
    }

    await stopLanguageServer();
}

async function handleDocumentSaved(context, document) {
    if (!isTypewriterDocument(document)) {
        return;
    }

    const configuration = getConfiguration(document.uri);
    if (configuration.get("generateOnSave", true)) {
        await executeCurrentTemplate(context, "generate", document);
        return;
    }

    if (configuration.get("validateOnSave", true)) {
        if (languageClient) {
            return;
        }

        await executeCurrentTemplate(context, "validate", document);
    }
}

async function restartLanguageServer(context) {
    await stopLanguageServer();
    await startLanguageServer(context, { notify: true });
}

async function startLanguageServer(context, options = {}) {
    const configuration = getConfiguration();
    if (!configuration.get("languageServer.enabled", true)) {
        return;
    }

    const workspacePath = resolveWorkspacePath();
    if (!workspacePath) {
        return;
    }

    const workingDirectory = getWorkingDirectory(workspacePath);
    const invocation = resolveLanguageServerInvocation(context, workingDirectory, configuration);
    try {
        const { LanguageClient } = require("vscode-languageclient/node");
        languageClient = new LanguageClient(
            "typewriterLanguageServer",
            "Typewriter Language Server",
            {
                command: invocation.command,
                args: invocation.args,
                options: {
                    cwd: workingDirectory,
                    windowsHide: true,
                },
            },
            {
                documentSelector: [
                    {
                        scheme: "file",
                        language: "typewriter",
                    },
                ],
                initializationOptions: buildLanguageServerInitializationOptions(workspacePath, configuration),
                outputChannel,
            });
        outputChannel.appendLine(formatCommand(invocation.command, invocation.args));
        await languageClient.start();
        if (options.notify) {
            vscode.window.setStatusBarMessage("Typewriter language server restarted.", 3000);
        }
    } catch (error) {
        languageClient = undefined;
        outputChannel.appendLine(`Typewriter language server failed to start: ${error.message ?? error}`);
        if (options.notify) {
            vscode.window.showWarningMessage("Typewriter language server failed to restart. See the Typewriter output channel.");
        }
    }
}

async function stopLanguageServer() {
    if (!languageClient) {
        return;
    }

    const client = languageClient;
    languageClient = undefined;
    await client.stop();
}

async function executeCurrentTemplate(context, command, document, resourceUri) {
    const templatePath = await resolveCurrentTemplatePath(document, resourceUri);
    if (!templatePath) {
        return;
    }

    await executeCliCommand(context, command, templatePath, { resourceUri });
}

async function executeGenerateAll(context, resourceUri) {
    await executeCliCommand(context, "generate", undefined, { allTemplates: true, resourceUri });
}

async function executeCliCommand(context, command, templatePath, options = {}) {
    const workspacePath = resolveWorkspacePath(templatePath, options.resourceUri);
    if (!workspacePath) {
        vscode.window.showErrorMessage("Typewriter requires an open workspace or configured typewriter.workspacePath.");
        return;
    }

    const workingDirectory = getWorkingDirectory(workspacePath);
    const configuration = getConfiguration(templatePath ? vscode.Uri.file(templatePath) : options.resourceUri);
    const args = buildTypewriterArguments(command, workspacePath, templatePath, configuration, options);
    const invocation = resolveCliInvocation(context, workingDirectory, configuration);

    await vscode.window.withProgress(
        {
            location: vscode.ProgressLocation.Notification,
            title: command === "validate" ? "Typewriter: validating template" : "Typewriter: generating files",
            cancellable: false,
        },
        async () => {
            outputChannel.appendLine(formatCommand(invocation.command, invocation.args.concat(args)));
            const result = await runProcess(invocation.command, invocation.args.concat(args), workingDirectory);
            writeProcessOutput(result);

            const payload = parseCliPayload(result.stdout);
            if (payload) {
                publishDiagnostics(payload, workingDirectory);
                writeResultSummary(payload);
            } else {
                diagnostics.clear();
            }

            if (result.exitCode === 0 && payload?.success !== false) {
                const generated = Array.isArray(payload?.generatedFiles) ? payload.generatedFiles.length : 0;
                vscode.window.setStatusBarMessage(
                    generated > 0 ? `Typewriter generated ${generated} file(s).` : "Typewriter completed.",
                    3000);
                return;
            }

            const message = payload?.success === false
                ? "Typewriter completed with diagnostics."
                : "Typewriter CLI failed. See the Typewriter output channel.";
            vscode.window.showWarningMessage(message);
            outputChannel.show(true);
        });
}

function buildTypewriterArguments(command, workspacePath, templatePath, configuration, options) {
    const args = [
        command,
        "--workspace",
        workspacePath,
        "--output",
        "json",
    ];

    const projectPath = resolveOptionalPath(configuration.get("projectPath"), getPathBase(workspacePath));
    if (projectPath) {
        args.push("--project", projectPath);
    }

    if (templatePath && !options.allTemplates) {
        args.push("--template", templatePath);
    }

    const framework = configuration.get("framework");
    if (typeof framework === "string" && framework.trim()) {
        args.push("--framework", framework.trim());
    }

    if (options.allTemplates && configuration.get("allProjects", false)) {
        args.push("--all-projects");
    }

    return args;
}

function resolveCliInvocation(context, workspacePath, configuration) {
    const configuredCliPath = configuration.get("cliPath", "").trim();
    const configuredArguments = configuration.get("cliArguments", []);
    if (configuredCliPath) {
        return {
            command: configuredCliPath,
            args: Array.isArray(configuredArguments) ? configuredArguments.map(String) : [],
        };
    }

    const localCliProject = findLocalCliProject(context.extensionPath) || findLocalCliProject(workspacePath);
    if (localCliProject) {
        const localCliDll = findBuiltCliDll(localCliProject);
        if (localCliDll) {
            return {
                command: "dotnet",
                args: [localCliDll],
            };
        }
    }

    const packagedCli = findPackagedCliInvocation(context.extensionPath);
    if (packagedCli) {
        return packagedCli;
    }

    return {
        command: "typewriter",
        args: [],
    };
}

function resolveLanguageServerInvocation(context, workspacePath, configuration) {
    const configuredServerPath = configuration.get("languageServer.path", "").trim();
    const configuredArguments = configuration.get("languageServer.arguments", []);
    if (configuredServerPath) {
        return {
            command: configuredServerPath,
            args: Array.isArray(configuredArguments) ? configuredArguments.map(String) : [],
        };
    }

    const localServerProject = findLocalLanguageServerProject(context.extensionPath)
        || findLocalLanguageServerProject(workspacePath);
    if (localServerProject) {
        const localServerDll = findBuiltLanguageServerDll(localServerProject);
        if (localServerDll) {
            return {
                command: "dotnet",
                args: [localServerDll],
            };
        }
    }

    const packagedServer = findPackagedLanguageServerInvocation(context.extensionPath);
    if (packagedServer) {
        return packagedServer;
    }

    return {
        command: "typewriter-lsp",
        args: [],
    };
}

function findLocalCliProject(startPath) {
    const candidates = [
        path.resolve(startPath, "..", "src", "Typewriter.Cli", "Typewriter.Cli.csproj"),
        path.resolve(startPath, "src", "Typewriter.Cli", "Typewriter.Cli.csproj"),
    ];

    return candidates.find(candidate => fs.existsSync(candidate));
}

function findLocalLanguageServerProject(startPath) {
    const candidates = [
        path.resolve(startPath, "..", "src", "Typewriter.LanguageServer", "Typewriter.LanguageServer.csproj"),
        path.resolve(startPath, "src", "Typewriter.LanguageServer", "Typewriter.LanguageServer.csproj"),
    ];

    return candidates.find(candidate => fs.existsSync(candidate));
}

function findPackagedCliInvocation(extensionPath) {
    return findPackagedToolInvocation(extensionPath, "typewriter-cli", "Typewriter.Cli.dll");
}

function findPackagedLanguageServerInvocation(extensionPath) {
    return findPackagedToolInvocation(extensionPath, "typewriter-lsp", "Typewriter.LanguageServer.dll");
}

function findPackagedToolInvocation(extensionPath, toolDirectoryName, assemblyFileName) {
    const assemblyPath = path.join(extensionPath, "tools", toolDirectoryName, assemblyFileName);
    return fs.existsSync(assemblyPath)
        ? {
            command: "dotnet",
            args: [assemblyPath],
        }
        : undefined;
}

function findBuiltCliDll(projectPath) {
    return findBuiltToolDll(projectPath, "Typewriter.Cli.dll");
}

function findBuiltLanguageServerDll(projectPath) {
    return findBuiltToolDll(projectPath, "Typewriter.LanguageServer.dll");
}

function findBuiltToolDll(projectPath, assemblyFileName) {
    const projectDirectory = path.dirname(projectPath);
    const outputRoot = path.join(projectDirectory, "bin");
    if (!fs.existsSync(outputRoot)) {
        return undefined;
    }

    const candidates = [];
    for (const configuration of safeReadDirectory(outputRoot)) {
        const configurationPath = path.join(outputRoot, configuration);
        if (!safeIsDirectory(configurationPath)) {
            continue;
        }

        for (const framework of safeReadDirectory(configurationPath)) {
            const candidate = path.join(configurationPath, framework, assemblyFileName);
            if (fs.existsSync(candidate)) {
                candidates.push(candidate);
            }
        }
    }

    return candidates
        .sort((left, right) => safeModifiedTime(right) - safeModifiedTime(left))[0];
}

function buildLanguageServerInitializationOptions(workspacePath, configuration) {
    const basePath = getPathBase(workspacePath);
    const framework = configuration.get("framework");
    return {
        workspacePath,
        projectPath: resolveOptionalPath(configuration.get("projectPath"), basePath),
        framework: typeof framework === "string" && framework.trim() ? framework.trim() : undefined,
        allProjects: configuration.get("allProjects", false),
    };
}

function resolveWorkspacePath(templatePath, resourceUri) {
    const uri = resourceUri ?? (templatePath ? vscode.Uri.file(templatePath) : vscode.window.activeTextEditor?.document.uri);
    const workspaceFolder = uri ? vscode.workspace.getWorkspaceFolder(uri) : undefined;
    const fallbackFolder = workspaceFolder ?? vscode.workspace.workspaceFolders?.[0];
    const basePath = fallbackFolder?.uri.fsPath
        ?? (templatePath ? path.dirname(templatePath) : undefined)
        ?? (resourceUri ? path.dirname(resourceUri.fsPath) : undefined);
    const configuration = getConfiguration(uri);
    const configuredWorkspace = configuration.get("workspacePath");

    if (configuredWorkspace) {
        return resolveOptionalPath(configuredWorkspace, basePath ?? process.cwd());
    }

    return basePath;
}

async function resolveCurrentTemplatePath(document, resourceUri) {
    if (isTypewriterUri(resourceUri)) {
        return resourceUri.fsPath;
    }

    const targetDocument = document ?? vscode.window.activeTextEditor?.document;
    if (targetDocument && isTypewriterDocument(targetDocument)) {
        return targetDocument.uri.fsPath;
    }

    const configuration = getConfiguration(targetDocument?.uri);
    const workspacePath = resolveWorkspacePath();
    const configuredTemplate = resolveOptionalPath(
        configuration.get("templatePath"),
        workspacePath ? getPathBase(workspacePath) : process.cwd());
    if (configuredTemplate) {
        return configuredTemplate;
    }

    const selection = await vscode.window.showOpenDialog({
        canSelectFiles: true,
        canSelectFolders: false,
        canSelectMany: false,
        filters: {
            "Typewriter templates": ["tst"],
        },
        title: "Select Typewriter template",
    });
    return selection?.[0]?.fsPath;
}

function isTypewriterDocument(document) {
    return document.languageId === "typewriter"
        || document.uri.fsPath.toLowerCase().endsWith(".tst");
}

function isTypewriterUri(uri) {
    return uri?.scheme === "file"
        && uri.fsPath.toLowerCase().endsWith(".tst");
}

function toFileUri(value) {
    return value?.scheme === "file" ? value : undefined;
}

function createFallbackCompletionProvider() {
    return vscode.languages.registerCompletionItemProvider(
        {
            language: "typewriter",
        },
        {
            provideCompletionItems(document, position) {
                if (languageClient) {
                    return undefined;
                }

                return createFallbackCompletionItems(document, position);
            },
        },
        "$",
        ".",
        "[",
        "(");
}

function createFallbackCompletionItems(document, position) {
    const text = document.getText();
    const offset = document.offsetAt(position);
    const kind = getEmbeddedCompletionKind(text, offset);

    if (kind === "csharp") {
        return createCSharpFallbackCompletions(text);
    }

    if (kind === "template") {
        return createTemplateFallbackCompletions(false);
    }

    return createTypeScriptFallbackCompletions().concat(createTemplateFallbackCompletions(true));
}

function getEmbeddedCompletionKind(text, offset) {
    if (isInsideCSharpHelperBlock(text, offset)) {
        return "csharp";
    }

    if ((offset > 0 && text[offset - 1] === "$")
        || (offset < text.length && text[offset] === "$")
        || isInsideTemplateToken(text, offset)) {
        return "template";
    }

    return "typescript";
}

function isInsideTemplateToken(text, offset) {
    let start = Math.max(0, Math.min(offset, text.length) - 1);
    while (start >= 0 && /[A-Za-z0-9_]/.test(text[start])) {
        start--;
    }

    return start >= 0 && text[start] === "$";
}

function isInsideCSharpHelperBlock(text, offset) {
    let index = 0;
    while (index < text.length) {
        const start = text.indexOf("${", index);
        if (start < 0) {
            return false;
        }

        const end = findBalancedEnd(text, start + 1, "{", "}");
        if (end < 0) {
            return false;
        }

        if (offset >= start && offset <= end + 1
            && isCSharpHelperBlock(text.slice(start + 2, end), isStandaloneBlockStart(text, start))) {
            return true;
        }

        index = end + 1;
    }

    return false;
}

function isStandaloneBlockStart(text, dollarIndex) {
    for (let index = dollarIndex - 1; index >= 0 && text[index] !== "\r" && text[index] !== "\n"; index--) {
        if (!/\s/.test(text[index])) {
            return false;
        }
    }

    for (let index = dollarIndex + 2; index < text.length && text[index] !== "\r" && text[index] !== "\n"; index++) {
        if (!/\s/.test(text[index])) {
            return false;
        }
    }

    return true;
}

function isCSharpHelperBlock(block, allowPartial) {
    const firstLine = block.trimStart().split(/\r?\n/, 1)[0] ?? "";
    if (!firstLine) {
        return false;
    }

    const starts = [
        "using ",
        "#r ",
        "#reference ",
        "Template",
        "public ",
        "private ",
        "internal ",
        "protected ",
        "static ",
        "const ",
        "bool ",
        "string ",
        "char ",
        "int ",
        "long ",
        "void ",
        "List<",
        "IEnumerable<",
    ];
    if (starts.some(prefix => firstLine.startsWith(prefix))) {
        return true;
    }

    return allowPartial && ["boo", "cha", "con", "int", "lon", "pri", "pro", "pub", "sta", "str", "usi", "voi"]
        .some(prefix => firstLine.startsWith(prefix));
}

function findBalancedEnd(text, openIndex, open, close) {
    let depth = 0;
    for (let index = openIndex; index < text.length; index++) {
        if (text[index] === open) {
            depth++;
        } else if (text[index] === close) {
            depth--;
            if (depth === 0) {
                return index;
            }
        }
    }

    return -1;
}

function createTemplateFallbackCompletions(includeDollar) {
    const snippets = [
        ["Types", "Types[$0]", "All public/internal C# types available to this template."],
        ["Classes", "Classes[$0]", "C# classes."],
        ["Records", "Records[$0]", "C# records."],
        ["Interfaces", "Interfaces[$0]", "C# interfaces."],
        ["Enums", "Enums[$0]", "C# enums."],
        ["Properties", "Properties[$0]", "Properties on the current type."],
        ["Methods", "Methods[$0]", "Methods on the current type."],
        ["Parameters", "Parameters[$0]", "Parameters on the current method."],
        ["Constants", "Constants[$0]", "Constants on the current type."],
        ["Name", "Name", "Current item name."],
        ["name", "name", "Current item name with camel-case formatting."],
        ["Type", "Type", "TypeScript rendering of the current item's type."],
        ["Default", "Default", "Default TypeScript value for the current type reference."],
    ];

    return snippets.map(([label, insertText, detail]) => {
        const item = new vscode.CompletionItem(
            includeDollar ? `$${label}` : label,
            vscode.CompletionItemKind.Snippet);
        item.detail = "Typewriter template expression";
        item.documentation = detail;
        item.insertText = new vscode.SnippetString(`${includeDollar ? "$" : ""}${insertText}`);
        return item;
    });
}

function createCSharpFallbackCompletions(text) {
    const keywords = [
        "bool",
        "break",
        "case",
        "class",
        "const",
        "continue",
        "else",
        "false",
        "foreach",
        "if",
        "int",
        "long",
        "new",
        "null",
        "private",
        "public",
        "return",
        "static",
        "string",
        "switch",
        "true",
        "typeof",
        "using",
        "var",
        "void",
    ];
    const codeModelTypes = [
        "Attribute",
        "AttributeArgument",
        "Class",
        "Constant",
        "Delegate",
        "Enum",
        "EnumValue",
        "Field",
        "File",
        "Interface",
        "Method",
        "Parameter",
        "Property",
        "Record",
        "Settings",
        "Type",
    ];
    const helpers = Array.from(text.matchAll(/(?:public\s+|private\s+|internal\s+|protected\s+|static\s+)*[A-Za-z_][A-Za-z0-9_<>,.\s?]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(/g))
        .map(match => match[1])
        .filter(name => !["Template", "if", "for", "foreach", "while", "switch"].includes(name));

    return keywords
        .map(keyword => createSimpleCompletion(keyword, vscode.CompletionItemKind.Keyword, "C# keyword"))
        .concat(codeModelTypes.map(type => createSimpleCompletion(type, vscode.CompletionItemKind.Class, "Typewriter.CodeModel type")))
        .concat(helpers.map(helper => createSimpleCompletion(helper, vscode.CompletionItemKind.Function, "Template helper")));
}

function createTypeScriptFallbackCompletions() {
    return [
        "as",
        "async",
        "await",
        "boolean",
        "class",
        "const",
        "enum",
        "export",
        "extends",
        "false",
        "from",
        "function",
        "if",
        "implements",
        "import",
        "interface",
        "let",
        "new",
        "null",
        "number",
        "readonly",
        "return",
        "string",
        "true",
        "type",
        "undefined",
    ].map(keyword => createSimpleCompletion(keyword, vscode.CompletionItemKind.Keyword, "TypeScript keyword"));
}

function createSimpleCompletion(label, kind, detail) {
    const item = new vscode.CompletionItem(label, kind);
    item.detail = detail;
    return item;
}

function resolveOptionalPath(value, basePath) {
    if (typeof value !== "string" || !value.trim()) {
        return undefined;
    }

    const trimmed = value.trim();
    return path.isAbsolute(trimmed)
        ? trimmed
        : path.resolve(basePath, trimmed);
}

function getWorkingDirectory(workspacePath) {
    const basePath = getPathBase(workspacePath);
    return fs.existsSync(basePath) ? basePath : process.cwd();
}

function getPathBase(value) {
    if (!value) {
        return process.cwd();
    }

    if (fs.existsSync(value)) {
        return fs.statSync(value).isDirectory()
            ? value
            : path.dirname(value);
    }

    return path.extname(value)
        ? path.dirname(value)
        : value;
}

function safeReadDirectory(directory) {
    try {
        return fs.readdirSync(directory);
    } catch {
        return [];
    }
}

function safeIsDirectory(candidate) {
    try {
        return fs.statSync(candidate).isDirectory();
    } catch {
        return false;
    }
}

function safeModifiedTime(candidate) {
    try {
        return fs.statSync(candidate).mtimeMs;
    } catch {
        return 0;
    }
}

function getConfiguration(scope) {
    return vscode.workspace.getConfiguration("typewriter", scope);
}

function runProcess(command, args, cwd) {
    return new Promise((resolve, reject) => {
        const child = childProcess.spawn(
            command,
            args,
            {
                cwd,
                shell: false,
                windowsHide: true,
            });

        let stdout = "";
        let stderr = "";

        child.stdout.on("data", chunk => {
            stdout += chunk.toString();
        });
        child.stderr.on("data", chunk => {
            stderr += chunk.toString();
        });
        child.on("error", error => {
            reject(error);
        });
        child.on("close", exitCode => {
            resolve({
                exitCode: exitCode ?? 1,
                stdout,
                stderr,
            });
        });
    });
}

function parseCliPayload(stdout) {
    const start = stdout.indexOf("{");
    const end = stdout.lastIndexOf("}");
    if (start < 0 || end < start) {
        return undefined;
    }

    try {
        return JSON.parse(stdout.slice(start, end + 1));
    } catch {
        return undefined;
    }
}

function publishDiagnostics(payload, workspacePath) {
    diagnostics.clear();
    if (!Array.isArray(payload.diagnostics)) {
        return;
    }

    const groupedDiagnostics = new Map();
    for (const diagnostic of payload.diagnostics) {
        const filePath = resolveDiagnosticPath(diagnostic.file, workspacePath);
        if (!filePath) {
            outputChannel.appendLine(formatDiagnosticMessage(diagnostic));
            continue;
        }

        const uri = vscode.Uri.file(filePath);
        const key = uri.toString();
        const existing = groupedDiagnostics.get(key) ?? { uri, items: [] };
        existing.items.push(createDiagnostic(diagnostic));
        groupedDiagnostics.set(key, existing);
    }

    for (const entry of groupedDiagnostics.values()) {
        diagnostics.set(entry.uri, entry.items);
    }
}

function createDiagnostic(diagnostic) {
    const line = Math.max((Number(diagnostic.line) || 1) - 1, 0);
    const column = Math.max((Number(diagnostic.column) || 1) - 1, 0);
    const range = new vscode.Range(line, column, line, column + 1);
    const item = new vscode.Diagnostic(
        range,
        diagnostic.message ?? "Typewriter diagnostic",
        mapDiagnosticSeverity(diagnostic.severity));
    item.source = "Typewriter";
    if (diagnostic.code) {
        item.code = diagnostic.code;
    }

    return item;
}

function mapDiagnosticSeverity(severity) {
    switch (String(severity ?? "").toLowerCase()) {
        case "error":
            return vscode.DiagnosticSeverity.Error;
        case "warning":
            return vscode.DiagnosticSeverity.Warning;
        case "information":
        case "info":
            return vscode.DiagnosticSeverity.Information;
        default:
            return vscode.DiagnosticSeverity.Hint;
    }
}

function resolveDiagnosticPath(filePath, workspacePath) {
    if (typeof filePath !== "string" || !filePath.trim()) {
        return undefined;
    }

    return path.isAbsolute(filePath)
        ? filePath
        : path.resolve(workspacePath, filePath);
}

function writeProcessOutput(result) {
    if (result.stdout.trim()) {
        outputChannel.appendLine(result.stdout.trimEnd());
    }

    if (result.stderr.trim()) {
        outputChannel.appendLine(result.stderr.trimEnd());
    }
}

function writeResultSummary(payload) {
    if (Array.isArray(payload.generatedFiles)) {
        for (const file of payload.generatedFiles) {
            const status = file.changed ? "updated" : "unchanged";
            outputChannel.appendLine(`${status}: ${file.path}`);
        }
    }

    if (Array.isArray(payload.diagnostics) && payload.diagnostics.length > 0) {
        outputChannel.appendLine(`${payload.diagnostics.length} diagnostic(s).`);
    }
}

function formatDiagnosticMessage(diagnostic) {
    const severity = diagnostic.severity ?? "diagnostic";
    const code = diagnostic.code ? ` ${diagnostic.code}` : "";
    return `${severity}${code}: ${diagnostic.message ?? ""}`;
}

function formatCommand(command, args) {
    return `> ${[command].concat(args).map(quoteArgument).join(" ")}`;
}

function quoteArgument(value) {
    const text = String(value);
    return /[\s"]/.test(text)
        ? `"${text.replace(/"/g, "\\\"")}"`
        : text;
}

module.exports = {
    activate,
    deactivate,
};
