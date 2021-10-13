using Microsoft.Extensions.Logging;

using Nettrace;

using System;
using System.Diagnostics;
using System.IO;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var logger = loggerFactory.CreateLogger<Parser>();

var stopwatch = new Stopwatch();
var process = Process.GetCurrentProcess();

//using var stream = File.OpenRead("trace_1.nettrace");
using var stream = File.OpenRead("serp.nettrace");

using var blockProcessor = new CopyBlockProcessor(@"temp.nettrace");
//using var blockProcessor = new RolloverBlockProcessor(Directory.GetCurrentDirectory());

var parser = new Parser(logger, blockProcessor);

stopwatch.Start();
var startCpuUsage = process.TotalProcessorTime;
await parser.ProcessAsync(stream);
stopwatch.Stop();

var endCpuUsage = process.TotalProcessorTime;
var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
var cpuUsage = cpuUsedMs / (Environment.ProcessorCount * stopwatch.ElapsedMilliseconds);

logger.LogInformation("Elapsed time: {elapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
logger.LogInformation("Consumed CPU: {cpuUsage}", cpuUsage);
