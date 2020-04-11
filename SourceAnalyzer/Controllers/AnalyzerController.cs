using GraphBuilder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace GraphBuilder.Controllers
{
    public class AnalyzerController : ControllerBase
    {
        private static ConcurrentDictionary<string, Graph> _cache = new ConcurrentDictionary<string, Graph>();
        private readonly ILogger<AnalyzerController> _logger;
        private static ConcurrentDictionary<string, Task> _processing = new ConcurrentDictionary<string, Task>();

        public AnalyzerController(ILogger<AnalyzerController> logger)
        {
            _logger = logger;
        }
 
        public IActionResult Graph(string repoUrl)
        {
            try
            {
                repoUrl = HttpUtility.UrlDecode(repoUrl);

                if (_cache.TryGetValue(repoUrl, out var graph))
                {
                    if (DateTime.Now - graph.Timestamp < TimeSpan.FromMinutes(10))
                    {
                        return Ok(graph);
                    }
                    _cache.TryRemove(repoUrl, out var removed);
                }

                var rootPath = Environment.GetEnvironmentVariable("SGB_ROOT_PATH");
                var repoName = Path.GetFileNameWithoutExtension(repoUrl);
                var repoPath = Path.Combine(rootPath, repoName);

                var task = new Task(() =>
                {
                    try
                    {
                        if (!Directory.Exists(repoPath))
                        {
                            GitApi.Clone(repoUrl, repoPath);
                        }

                        GitApi.Fetch(repoPath);

                        var graph = GitApi.ParseProjectFiles(repoUrl, repoPath);

                        _cache.TryAdd(repoUrl, graph);
                        _processing.TryRemove(repoUrl, out var removed);
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Background task encountered a critical error");
                    }
                });

                if (_processing.TryAdd(repoUrl, task))
                {
                    task.Start();
                }
                return StatusCode(202, new Graph(repoUrl));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to generate source graph");
                return StatusCode(500, "Something went wrong, you should talk to support about this");
            }
        }
    }
}
