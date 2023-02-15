using System;
using Typewriter.CodeModel;

namespace Typewriter.Configuration
{
    /// <summary>
    /// Provides settings for Typewriter Templates
    /// </summary>
    public abstract class Settings
    {
        /// <summary>
        /// Includes files in the specified project when rendering the template.
        /// </summary>
        public abstract Settings IncludeProject(string projectName);

        /// <summary>
        /// Single File Mode - Template will be parsed in one file. Ignor
        /// Note: SingleFileMode ignores the filename factory(!)
        /// </summary>
        /// <param name="singleFilename">The single filename.</param>
        /// <returns></returns>
        public abstract Settings SingleFileMode(string singleFilename);

        /// <summary>
        /// Includes files in the current project when rendering the template.
        /// </summary>
        public abstract Settings IncludeCurrentProject();

        /// <summary>
        /// Includes files in all referenced projects when rendering the template.
        /// </summary>
        public abstract Settings IncludeReferencedProjects();

        /// <summary>
        /// Includes files in all projects in the solution when rendering the template.
        /// Note: Including all projects can have a large impact on performance in large solutions.
        /// </summary>
        public abstract Settings IncludeAllProjects();

        /// <summary>
        /// Gets or sets the file extension for output files.
        /// </summary>
        public string OutputExtension { get; set; } = ".ts";

        /// <summary>
        /// Gets or sets a filename factory for the template.
        /// The factory is called for each rendered file to determine the output filename (including extension).
        /// Example: file => file.Classes.First().FullName + ".ts";
        /// </summary>
        public Func<File, string> OutputFilenameFactory { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public PartialRenderingMode PartialRenderingMode { get; set; } = PartialRenderingMode.Partial;

        /// <summary>
        /// Gets full solution name from DTE.
        /// </summary>
        public abstract string SolutionFullName { get; }

        /// <summary>
        /// Gets or sets output directory to which generated files needs to be saved.
        /// </summary>
        public string OutputDirectory { get; set; } = null;

        /// <summary>
        /// Gets or sets value indicating if generated files should not be added to project.
        /// </summary>
        public bool SkipAddingGeneratedFilesToProject { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is single file mode.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is single file mode; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsSingleFileMode { get; }

        /// <summary>
        /// Gets the name of the single file.
        /// </summary>
        /// <value>
        /// The name of the single file.
        /// </value>
        public abstract string SingleFileName { get; }

    }
}
