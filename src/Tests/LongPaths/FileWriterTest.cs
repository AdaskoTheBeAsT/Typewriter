using System;
using System.IO;
using AwesomeAssertions;
using Xunit;

namespace Typewriter.Tests.LongPaths
{
    public class FileWriterTest
    {
        [Fact]
        public void ShouldCreateFileInDirectoryForLongPath()
        {
            var path = @"C:\Dev\SomeInnerPath\Github\Project\src\innerpath\another-subpath\A.Very.Long.Project.Name.Like.Dotnet.People.Like\packages\some-random-scope\some-package-name\src\lib\events\a-very-very-very-very-very-very-very-very-very-very-very-very-very-very-long-file-name.ts";
            var modifiedPath = path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
            ? $@"\\?\UNC\{path.Substring(2, path.Length - 2)}"
                : $@"\\?\{path}";
            Action action = () =>
            {
                var dir = Path.GetDirectoryName(modifiedPath);
                if (!Directory.Exists(dir) && dir != null)
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(modifiedPath, "ok");
            };

            action.Should().NotThrow();
        }

        [Fact]
        public void ShouldCreateFileInDirectoryForNonNormalizedLongPath()
        {
            var path = @"C:\Dev\..\Dev\SomeInnerPath\Github\Project\src\innerpath\another-subpath\A.Very.Long.Project.Name.Like.Dotnet.People.Like\packages\some-random-scope\some-package-name\src\lib\events\a-very-very-very-very-very-very-very-very-very-very-very-very-very-very-long-file-name.ts";
            var normalizedPath = Path.GetFullPath(path);
            var longPath = normalizedPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
                ? $@"\\?\UNC\{normalizedPath.Substring(2, normalizedPath.Length - 2)}"
                : $@"\\?\{normalizedPath}";
            Action action = () =>
            {
                var dir = Path.GetDirectoryName(longPath);
                if (!Directory.Exists(dir) && dir != null)
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(longPath, "ok");
            };

            action.Should().NotThrow();
        }

        [Fact]
        public void ShouldSaveOnDesktop()
        {
            var path = "C:\\Users\\Adam_\\Desktop\\DoctorsOfficeGit\\AngularTest\\src\\models\\a.tst";
            var normalizedPath = Path.GetFullPath(path);
            var longPath = normalizedPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
                ? $@"\\?\UNC\{normalizedPath.Substring(2, normalizedPath.Length - 2)}"
                : $@"\\?\{normalizedPath}";
            Action action = () =>
            {
                var dir = Path.GetDirectoryName(longPath);
                if (!Directory.Exists(dir) && dir != null)
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(longPath, "ok");
            };

            action.Should().NotThrow();
        }
    }
}