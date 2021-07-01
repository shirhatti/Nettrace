using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nettrace.Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(logging =>
            {
                logging.AddConsole();
            });
            using var stream = File.OpenRead("trace.nettrace");
            var parser = new Parser(loggerFactory.CreateLogger<Parser>());
            await parser.ProcessAsync(stream);
            await Task.Delay(5000);
        }
    }
}
