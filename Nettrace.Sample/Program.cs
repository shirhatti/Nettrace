using Microsoft.Extensions.Logging;

using Nettrace;

using System.Diagnostics;
using System.IO;

var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var stopwatch = new Stopwatch();
using var stream = File.OpenRead("serp.nettrace");
var logger = loggerFactory.CreateLogger<Parser>();
using var parser = new Parser();
stopwatch.Start();
await parser.ProcessAsync(stream);
stopwatch.Stop();
logger.LogWarning("Elapsed time: {elapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
loggerFactory.Dispose();
