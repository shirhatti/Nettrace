using Microsoft.Extensions.Logging;

using Nettrace;

using System.Diagnostics;
using System.IO;

var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var stopwatch = new Stopwatch();
//using var stream = File.OpenRead("trace_1.nettrace");
using var stream = File.OpenRead("serp.nettrace");
var logger = loggerFactory.CreateLogger<Parser>();
//var blockProcessor = new CopyBlockProcessor(@"temp.nettrace");
var blockProcessor = new RolloverBlockProcessor(Directory.GetCurrentDirectory());
using var parser = new Parser(blockProcessor);
stopwatch.Start();
await parser.ProcessAsync(stream);
stopwatch.Stop();
logger.LogWarning("Elapsed time: {elapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
loggerFactory.Dispose();
