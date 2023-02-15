using Microsoft.IO;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Should;
using System.Linq;
using Typewriter.Configuration;
using Typewriter.Generation;
using Typewriter.Tests.Render.WebApiController;
using Typewriter.Tests.TestInfrastructure;
using Xunit;

namespace Typewriter.Tests.Render
{
    [Trait("Render", "Roslyn"), Collection(nameof(RoslynFixture))]
    public class RoslynRenderTests : RenderTests
    {
        public RoslynRenderTests(RoslynFixture fixture, GlobalServiceProvider sp) : base(fixture, sp)
        {
        }
    }

    public abstract class RenderTests : TestInfrastructure.TestBase
    {
        protected RenderTests(ITestFixture fixture, GlobalServiceProvider sp) : base(fixture, sp)
        {
        }

        private void Assert<TClass>()
        {
            var type = typeof(TClass);
            var nsParts = type.FullName.Remove(0, 11).Split('.');

            var path = string.Join(@"\", nsParts);
            var tt = GetProjectItem("Tests\\Render\\WebApiController\\SingleFile.tstemplate");
            var t2 = GetProjectItem("Tests\\Render\\WebApiController\\SingleFile.tstemplate");
            var template = new Template(GetProjectItem(path + ".tstemplate"));
            var file = GetFile(path + ".cs");
            var result = GetFileContents(path + ".result");

            var output = template.Render(file, out var success);
            
            success.ShouldBeTrue();
            output.ShouldEqual(result);
        }

        [Fact]
        public void Expect_SingleFileMode_To_Render_Correctly()
        {
            var ts = Path.Combine("Tests", "Render", "WebApiController", "SingleFile.tstemplate");
            var projectItem = GetProjectItem(ts);
           
            var pathModels = Path.Combine("Tests","Render","WebApiController", "SingleFileModels");
           
            pathModels = Path.Combine(SolutionDirectory, pathModels);

            string[] models = Directory.GetFiles(pathModels, "*.cs");

            var files = models.Select(x => GetFile(Path.Combine(pathModels, Path.GetFileName(x)))).ToArray();
           
            var template = new Template(projectItem);

            template.Settings.IsSingleFileMode.ShouldBeTrue();

            string tpl = template.Render(files, out var success);

            string resultFile = Path.Combine("Tests", "Render", "WebApiController", Path.GetFileNameWithoutExtension(ts) + ".result");
            string content = GetFileContents(resultFile);
            success.ShouldBeTrue();

            tpl.ShouldEqual(content);

        }
        //[Fact]
        //public void Expect_webapi_controller_to_angular_service_to_render_correctly()
        //{
        //    Assert<WebApiController.WebApiController>();
        //}

        //[Fact]
        //public void Expect_routed_webapi_controller_to_render_correctly()
        //{
        //    Assert<BooksController>();
        //}
    }
}
