using System.Threading.Tasks;

public interface ITemplateRenderer
{
    Task<string> RenderAsync(string templateKey, object model);
}
