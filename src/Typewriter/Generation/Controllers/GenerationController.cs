using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Typewriter.CodeModel.Implementation;
using Typewriter.Metadata.Providers;
using Typewriter.VisualStudio;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Typewriter.Generation.Controllers
{
    public class GenerationController
    {
        private readonly DTE _dte;
        private readonly IMetadataProvider _metadataProvider;
        private readonly TemplateController _templateController;
        private readonly IEventQueue _eventQueue;

        public GenerationController(DTE dte, IMetadataProvider metadataProvider, TemplateController templateController, IEventQueue eventQueue)
        {
            _dte = dte;
            _metadataProvider = metadataProvider;
            _templateController = templateController;
            _eventQueue = eventQueue;
        }

        public void OnTemplateChanged(string templatePath, bool force = false)
        {
            Log.Debug("{0} queued {1}", GenerationType.Template, templatePath);

            ErrorList.Clear();

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var projectItem = _dte.Solution.FindProjectItem(templatePath);

                var template = _templateController.GetTemplate(projectItem);

                if (!force && !ExtensionPackage.Instance.RenderOnSave)
                {
                    Log.Debug("Render skipped {0}", templatePath);
                    return;
                }

                var filesToRender = template.GetFilesToRender();
                Log.Debug(" Will Check/Render {0} .cs files in referenced projects", filesToRender.Count);

                // Delay to wait for Roslyn to refresh the current Workspace after a change.
                await Task.Delay(1000).ConfigureAwait(true);
                _eventQueue.Enqueue(() =>
                {
                    var stopwatch = Stopwatch.StartNew();

                    // Intervene here!!! when singlefilemode use the singlefile renderer
                    if (template.Settings.IsSingleFileMode)
                    {
                        // Single File Render
                        var files = filesToRender.Select(path =>
                        {
                            var metadata = _metadataProvider.GetFile(path, template.Settings, null);
                            if (metadata == null)
                            {
                                // the cs-file was found, but the build-action is not set to compile.
                                return null;
                            }

                            var file = new FileImpl(metadata, template.Settings);

                            return file;
                        }).Where(f => f != null).ToArray();

                        template.RenderFile(files);

                        stopwatch.Stop();
                        Log.Debug("{0} processed {1} in {2}ms", GenerationType.Template, templatePath,
                        stopwatch.ElapsedMilliseconds);

                        return;
                    }

                    foreach (var path in filesToRender)
                    {
                        var metadata = _metadataProvider.GetFile(path, template.Settings, null);
                        if (metadata == null)
                        {
                            // the cs-file was found, but the build-action is not set to compile.
                            continue;
                        }

                        var file = new FileImpl(metadata, template.Settings);

                        template.RenderFile(file);

                        if (template.HasCompileException)
                        {
                            break;
                        }
                    }

                    template.SaveProjectFile();

                    stopwatch.Stop();
                    Log.Debug("{0} processed {1} in {2}ms", GenerationType.Template, templatePath,
                        stopwatch.ElapsedMilliseconds);
                });
            });
        }

        public void OnCsFileChanged(string[] paths)
        {
            if (!ExtensionPackage.Instance.TrackSourceFiles)
            {
                Log.Debug("Render skipped {0}", paths?.FirstOrDefault());
                return;
            }

            RenderFile(paths);
        }

        private void RenderFile(string[] paths)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                // Delay to wait for Roslyn to refresh the current Workspace after a change.
                await Task.Delay(1000).ConfigureAwait(true);

                Enqueue(GenerationType.Render, paths, (path, template) => _metadataProvider.GetFile(path, template.Settings, RenderFile), (fileMeta, template) =>
                {
                    if (fileMeta == null)
                    {
                        // the cs-file was found, but the build-action is not set to compile.
                        return;
                    }

                    var file = new FileImpl(fileMeta, template.Settings);

                    if (template.Settings.IsSingleFileMode)
                    {
                        var filesToRender = template.GetFilesToRender();
                        // In this case we need all files
                        if (template.ShouldRenderFile(file.FullName))
                        {
                            var files = filesToRender.Select(path =>
                            {
                                var metadata = _metadataProvider.GetFile(path, template.Settings, null);
                                if (metadata == null)
                                {
                                    // the cs-file was found, but the build-action is not set to compile.
                                    return null;
                                }

                                return new FileImpl(metadata, template.Settings);
                            }).Where(f => f != null).ToArray();

                            template.RenderFile(files);
                        }

                        return;
                    }

                    if (template.ShouldRenderFile(file.FullName))
                    {
                        template.RenderFile(file);
                    }
                });
            });
        }

        public void OnCsFileDeleted(string[] paths)
        {
            if (!ExtensionPackage.Instance.TrackSourceFiles)
            {
                Log.Debug("Delete skipped {0}", paths?.FirstOrDefault());
                return;
            }

            // Delay to wait for Roslyn to refresh the current Workspace after a change.
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await Task.Delay(1000).ConfigureAwait(true);
                Enqueue(GenerationType.Delete, paths, (path, template) =>
                {
                    if (template.Settings.IsSingleFileMode)
                    {
                        //File needs to be deleted manually
                        // Since we can check here if any condition in the single file is met
                        // But we can update the file
                        var filesToRender = template.GetFilesToRender();

                        var files = filesToRender.Select(p =>
                        {
                            var metadata = _metadataProvider.GetFile(p, template.Settings, null);
                            if (metadata == null)
                            {
                                // the cs-file was found, but the build-action is not set to compile.
                                return null;
                            }

                            return new FileImpl(metadata, template.Settings);
                        }).Where(f => f != null).ToArray();

                        template.RenderFile(files);

                        return;
                    }

                    template.DeleteFile(path);
                });
            });
        }

        private void Enqueue(GenerationType type, string[] paths, Action<string, Template> action)
        {
            Enqueue(type, paths, (s, t, i) => s, action);
        }

        public void OnCsFileRenamed(string[] newPaths, string[] oldPaths)
        {
            if (!ExtensionPackage.Instance.TrackSourceFiles)
            {
                Log.Debug("Rename skipped {0}", oldPaths?.FirstOrDefault());
                return;
            }

            // Delay to wait for Roslyn to refresh the current Workspace after a change.
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await Task.Delay(1000).ConfigureAwait(true);
                Enqueue(GenerationType.Rename, newPaths,
                    (path, template, fileIndex) => new
                    {
                        OldPath = oldPaths[fileIndex],
                        NewPath = path,
                        NewFileMeta = _metadataProvider.GetFile(path, template.Settings, null)
                    },
                    (item, template) =>
                    {
                        if (item.NewFileMeta == null)
                        {
                            // the cs-file was found, but the build-action is not set to compile.
                            return;
                        }

                        // Single mode?
                        if (template.Settings.IsSingleFileMode)
                        {
                            // In case of single file mode we need to recheck all files for this template
                            var files = template.GetFilesToRender().Select(path =>
                            {
                                var metadata = _metadataProvider.GetFile(path, template.Settings, null);
                                if (metadata == null)
                                {
                                    // the cs-file was found, but the build-action is not set to compile.
                                    return null;
                                }

                                return new FileImpl(metadata, template.Settings);
                            }).Where(f => f != null).ToArray();

                            template.RenderFile(files);

                            return;
                        }

                        var newFile = new FileImpl(item.NewFileMeta, template.Settings);
                        template.RenameFile(newFile, item.OldPath, item.NewPath);
                    });
            });
        }

        private void Enqueue<T>(GenerationType type, string[] paths, Func<string, Template, T> transform, Action<T, Template> action)
        {
            Enqueue(type, paths, (s, t, i) => transform(s, t), action);
        }

        private void Enqueue<T>(GenerationType type, string[] paths, Func<string, Template, int, T> transform, Action<T, Template> action)
        {
            var templates = _templateController.Templates.Where(m => !m.HasCompileException).ToArray();
            if (!templates.Any())
            {
                return;
            }

            Log.Debug("{0} queued {1}", type, string.Join(", ", paths));

            _eventQueue.Enqueue(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                paths.ForEach((path, i) =>
                {
                    templates.ForEach(template =>
                    {
                        var item = transform(path, template, i);
                        action(item, template);
                    });
                });

                templates.GroupBy(m => m.ProjectFullName).ForEach(template => template.First().SaveProjectFile());

                stopwatch.Stop();
                Log.Debug("{0} processed {1} in {2}ms", type, string.Join(", ", paths), stopwatch.ElapsedMilliseconds);
            });
        }
    }
}