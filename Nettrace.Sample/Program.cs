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
using var stream = File.OpenRead("trace.nettrace");

//using var blockProcessor = new DecompressBlockProcessor("temp.nettrace");
using var blockProcessor = new CopyBlockProcessor(@"temp.nettrace");
//using var blockProcessor = new RolloverBlockProcessor(Directory.GetCurrentDirectory());
//using var blockProcessor = new CompressBlockProcessor("temp.nettrace");

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


////using Microsoft.Diagnostics.NETCore.Client;
////using System.Diagnostics.Tracing;
////using System.Collections.Generic;
////using Microsoft.Diagnostics.Tracing;
////using System;
////using System.Collections;

////const long DiagnosticSourceKeywords_Messages = 0x1;
////const long DiagnosticSourceKeywords_Events = 0x2;

////const string DiagnosticFilterString = "\"" +
////  "Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Start@Activity1Start:-" +
////    "Request.Scheme" +
////    ";Request.Host" +
////    ";Request.PathBase" +
////    ";Request.QueryString" +
////    ";Request.Path" +
////    ";Request.Method" +
////    ";ActivityStartTime=*Activity.StartTimeUtc.Ticks" +
////    ";ActivityParentId=*Activity.ParentId" +
////    ";ActivityId=*Activity.Id" +
////    ";ActivitySpanId=*Activity.SpanId" +
////    ";ActivityTraceId=*Activity.TraceId" +
////    ";ActivityParentSpanId=*Activity.ParentSpanId" +
////    ";ActivityIdFormat=*Activity.IdFormat" +
////  "\r\n" +
////"Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop@Activity1Stop:-" +
////    "Response.StatusCode" +
////    ";ActivityDuration=*Activity.Duration.Ticks" +
////    ";ActivityId=*Activity.Id" +
////"\r\n" +
////"HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut@Event:-" +
////"\r\n" +
////"HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Start@Activity2Start:-" +
////    "Request.RequestUri" +
////    ";Request.Method" +
////    ";Request.RequestUri.Host" +
////    ";Request.RequestUri.Port" +
////    ";ActivityStartTime=*Activity.StartTimeUtc.Ticks" +
////    ";ActivityId=*Activity.Id" +
////    ";ActivitySpanId=*Activity.SpanId" +
////    ";ActivityTraceId=*Activity.TraceId" +
////    ";ActivityParentSpanId=*Activity.ParentSpanId" +
////    ";ActivityIdFormat=*Activity.IdFormat" +
////    ";ActivityId=*Activity.Id" +
//// "\r\n" +
////"HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Stop@Activity2Stop:-" +
////    ";ActivityDuration=*Activity.Duration.Ticks" +
////    ";ActivityId=*Activity.Id" +
////"\r\n" +

////"\"";

////const string DiagnosticFilterString = "Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Start@Activity1Start:-TraceIdentifier;Request.Method;Request.Host;Request.Path;Request.QueryString\r\n" +
////                                    "Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop@Activity1Stop:-TraceIdentifier;Response.StatusCode";

////const int pid = 37316;

//var provider = new EventPipeProvider(
//        name: "Microsoft-Diagnostics-DiagnosticSource",
//        eventLevel: EventLevel.Verbose,
//        keywords: DiagnosticSourceKeywords_Messages |
//                  DiagnosticSourceKeywords_Events,
//        arguments: new Dictionary<string, string> {
//            {
//                "FilterAndPayloadSpecs", DiagnosticFilterString
//            }
//        }
//    );

//var reader = new DiagnosticsClient(pid);
//var session = reader.StartEventPipeSession(provider, requestRundown: false);
//using var parser = new Parser(new CopyBlockProcessor("out.nettrace"));
//await parser.ProcessAsync(session.EventStream);
//Console.ReadLine();
//session.Dispose();