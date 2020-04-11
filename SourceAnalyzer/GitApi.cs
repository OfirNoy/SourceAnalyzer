using GraphBuilder.Models;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace GraphBuilder
{
    static class GitApi
    {
        private static string MakeRelative(string filePath, string referencePath)
        {
            var fileUri = new Uri(filePath);
            var referenceUri = new Uri(referencePath);
            return referenceUri.MakeRelativeUri(fileUri).ToString();
        }
        
        public static Graph ParseProjectFiles(string repoUrl, string repoPath)
        {
            var currentDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(repoPath);

            var nodes = new Dictionary<string, SourceNode>();
            var links = new List<Link>();

            using (var repo = new Repository(Directory.GetCurrentDirectory()))
            {
                var projects = repo.Index.Where(e => e.Path.ToLower().EndsWith(".csproj"));
                
                foreach (var entry in projects)
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(repo.Lookup<Blob>(entry.Id).GetContentText());
                    var packageRefs = doc.GetElementsByTagName("PackageReference");
                    var projectRefs = doc.GetElementsByTagName("ProjectReference");
                    var references = doc.GetElementsByTagName("Reference");                    
                    var targetType = doc.GetElementsByTagName("OutputType").Cast<XmlNode>().FirstOrDefault()?.InnerXml.ToLower();
                    var targetName = doc.GetElementsByTagName("AssemblyName").Cast<XmlNode>().FirstOrDefault()?.InnerXml;
                    
                    var projectPath = Path.GetFullPath(entry.Path);
                    var projectName = MakeRelative(projectPath, repoPath);
                    SourceNode sourceNode;
                    if (nodes.ContainsKey(projectName))
                    {
                        sourceNode = nodes[projectName];
                    }
                    else
                    {
                        sourceNode = new SourceNode(projectName, "Project");
                        nodes.Add(projectName, sourceNode);
                    }

                    SourceNode CreateSourceNode(string name, string type)
                    {
                        if (!nodes.ContainsKey(name))
                        {
                            nodes.Add(name, new SourceNode(name, type));
                        }

                        return nodes[name];
                    }

                    foreach (XmlNode node in packageRefs)
                    {
                        var name = node.Attributes["Include"]?.Value;
                        if (!string.IsNullOrEmpty(name))
                        {
                            var package = CreateSourceNode(name, "NuGet");
                            links.Add(new Link(package.id, sourceNode.id));
                        }
                    }

                    foreach (XmlNode node in projectRefs)
                    {
                        var name = MakeRelative(Path.Combine(Path.GetDirectoryName(projectPath), node.Attributes["Include"].Value), repoPath);
                        var proj = CreateSourceNode(name, "Project");
                        links.Add(new Link(proj.id, sourceNode.id));                        
                    }
                    
                    foreach (XmlNode node in references)
                    {
                        var name = node.Attributes["Include"].Value;
                        var hint = node.ChildNodes.Cast<XmlNode>().Where(c => c.Name == "HintPath");
                        var type = string.Empty;
                        var hasHint = hint.Count() > 0;

                        if (hasHint && name.Contains(", Version"))
                        {
                            type = "NuGet";
                        }
                        else if(hasHint)
                        {
                            type = "File";
                            name = hint.First().InnerXml;
                        }

                        if (!string.IsNullOrEmpty(type))
                        {
                            var sn = CreateSourceNode(name, type);
                            links.Add(new Link(sn.id, sourceNode.id));
                        }
                    }

                    // Add output                    
                    if(string.IsNullOrEmpty(targetType))
                    {
                        targetType = "library";
                    }

                    if (string.IsNullOrEmpty(targetName))
                    {
                        targetName = Path.GetFileNameWithoutExtension(projectName) + ".dll";
                        targetType = "library";
                    }
                    else
                    {
                        targetName = targetType.ToLower() == "library" ? targetName + ".dll" : targetName + "." + targetType;
                    }
                    
                    targetName = Path.Combine(Path.GetDirectoryName(projectName), targetName);
                    var targetNode = new SourceNode(targetName, targetType);                    
                    links.Add(new Link(sourceNode.id, targetNode.id));
                    nodes.Add(targetName, targetNode);
                }
            }
            Directory.SetCurrentDirectory(currentDir);
            return new Graph(repoUrl, nodes.Values.ToList(), links) { message = $"Nodes: {nodes.Count}, Links: {links.Count}" };
        }

        public static void Clone(string source, string target, Action<TransferProgress> progress)
        {
            TransferProgressHandler progressHandler = (tp) =>
            {
                progress(tp);
                return true;
            };
            Repository.Clone(source, target, new CloneOptions { OnTransferProgress = progressHandler });
        }

        public static void Fetch(string source)
        {
            using (var repo = new Repository(source))
            {
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, null, "");
            }
        }
    }

}
