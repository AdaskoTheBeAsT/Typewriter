using System.Collections.Generic;
using EnvDTE;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Typewriter.Configuration;

namespace Typewriter.CodeModel.Configuration
{
    public class SettingsImpl : Settings
    {
        private readonly ProjectItem _projectItem;

        private bool _isSingleFileMode = false;
        private string _singleFileName = null;

        public SettingsImpl(ProjectItem projectItem)
        {
            _projectItem = projectItem;
        }

        private List<string> _includedProjects;
        
        public override Settings IncludeProject(string projectName)
        {
            if (_includedProjects == null)
                _includedProjects = new List<string>();

            ProjectHelpers.AddProject(_projectItem, _includedProjects, projectName);
            return this;
        }

        public override Settings SingleFileMode(string singleFilename)
        {
            this._isSingleFileMode= true;
            this._singleFileName= singleFilename;

            return this;
        }

        public override Settings IncludeReferencedProjects()
        {
            if (_includedProjects == null)
                _includedProjects = new List<string>();

            ProjectHelpers.AddReferencedProjects(_includedProjects, _projectItem);
            return this;
        }

        public override Settings IncludeCurrentProject()
        {
            if (_includedProjects == null)
                _includedProjects = new List<string>();

            ProjectHelpers.AddCurrentProject(_includedProjects, _projectItem);
            return this;
        }

        public override Settings IncludeAllProjects()
        {
            if (_includedProjects == null)
            {
                _includedProjects = new List<string>();
            }

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ProjectHelpers.AddAllProjects(_projectItem.DTE, _includedProjects);
            });
            return this;
        }

        public ICollection<string> IncludedProjects
        {
            get
            {
                if (_includedProjects == null)
                {
                    IncludeCurrentProject();
                    IncludeReferencedProjects();
                }

                return _includedProjects;
            }
        }

        public override string SolutionFullName
        {
            get
            {
                var fullName = string.Empty;
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    fullName = _projectItem.DTE.Solution.FullName;
                });
                return fullName;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is single file mode.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is single file mode; otherwise, <c>false</c>.
        /// </value>
        public override bool IsSingleFileMode => this._isSingleFileMode;

        /// <summary>
        /// Gets the name of the single file.
        /// </summary>
        /// <value>
        /// The name of the single file.
        /// </value>
        public override string SingleFileName => this._singleFileName;
    }
}
