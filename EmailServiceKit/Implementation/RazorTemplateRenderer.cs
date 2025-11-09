using RazorLight;
using System.Threading.Tasks;

public class RazorTemplateRenderer : ITemplateRenderer
{
    private readonly RazorLightEngine _engine;
    public RazorTemplateRenderer()
    {
        _engine = new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(RazorTemplateRenderer))
            .UseMemoryCachingProvider()
            .Build();
    }

    public Task<string> RenderAsync(string templateKey, object model)
    {
        // templateKey can be template content or resource key
        return _engine.CompileRenderStringAsync(templateKey, templateKey, model);
    }
}
