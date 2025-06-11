namespace RazorSharpener.Console
{
    static class Program
    {
        static async Task Main()
        {
            var compiler = new RazorCompiler();

            var dictionary = new Dictionary<string, object?>
            {
                ["Message"] = "Hello, World"
            };

            var renderer = new RazorRenderer();
            //var html = await renderer.Render<RenderMessage>(dictionary);
            
            var asm = compiler.Compile("RenderMessage.razor");
            var componentType = asm.GetTypes().First();
            var html = await renderer.Render(componentType, dictionary);

            System.Console.WriteLine(html);
        }
    }
}
