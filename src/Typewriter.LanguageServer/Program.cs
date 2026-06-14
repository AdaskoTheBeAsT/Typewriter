using Typewriter.LanguageServer;

await LanguageServerProgram.RunAsync(args: args, cancellationToken: CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
