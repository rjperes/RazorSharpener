using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RazorSharpener
{
    public class RazorRenderer(ILoggerFactory loggerFactory)
    {
        private static readonly IServiceProvider _serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        private readonly ILoggerFactory _loggerFactory = loggerFactory;

        public RazorRenderer() : this(_serviceProvider.GetRequiredService<ILoggerFactory>())
        {        
        }

        public async Task<string> Render<TComponent>(Dictionary<string, object?>? dictionary = null) where TComponent : IComponent
        {
            return await Render(typeof(TComponent), dictionary);
        }

        public async Task<string> Render(Type componentType, Dictionary<string, object?>? dictionary = null)
        {
            ArgumentNullException.ThrowIfNull(componentType);

            if (!typeof(IComponent).IsAssignableFrom(componentType))
            {
                throw new ArgumentException("Invalid component type.", nameof(componentType));
            }

            await using var htmlRenderer = new HtmlRenderer(_serviceProvider, _loggerFactory);

            var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
            {
                var parameters = (dictionary is not null) ? ParameterView.FromDictionary(dictionary) : new ParameterView();
                var output = await htmlRenderer.RenderComponentAsync(componentType, parameters);

                return output.ToHtmlString();
            });

            var logger = _loggerFactory.CreateLogger<RazorRenderer>();
            logger.LogInformation("Rendered HTML is:\n{HTML}", html);

            return html;
        }
    }
}
