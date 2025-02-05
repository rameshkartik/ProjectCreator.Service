using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Frameworks;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NullLogger = NuGet.Common.NullLogger;
using ILogger = NuGet.Common.ILogger;
using System.Reflection;

namespace ProjectCreator.Service
{
    public class ProjectCreation : IProjectCreator
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly ProjectCreationConfig _config;

        private ActivityLogger? _logger;

        private Dictionary<string, IEnumerable<IPackageSearchMetadata>> PackageMetaData = new();

        private Dictionary<string, string> ProjErrors = new();

        private Dictionary<string, string> PackageErrors = new();

        private List<string> UpdatedProjects = new();

        private readonly string BreakLine = "========================================================================";

        private string sInputFileLocation = string.Empty;

        private string sOutputFileLocation = string.Empty;

        private List<string> Logs = new List<string>();

        JObject jParent = new JObject();
        public ProjectCreation(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _config = _serviceProvider.GetRequiredService<IOptionsSnapshot<ProjectCreationConfig>>().Value;
            InitializeLoggerVariables();
        }
       
       
        public async Task<string> UpgradeProjectsAndApplyNugetPackages()
        {
            SetProjectPath();
            var projectDependencyLevelOrderList = _config.ProjectDependency.Level.ToList().OrderBy(x => x.LevelOrder).ToList();
            PushUpgradeOperationSummaryCountLog(projectDependencyLevelOrderList.Count);
            LogConsole($"Number of projects to be upgraded {projectDependencyLevelOrderList.Count}");
            int noUpdated = 0;
            var alreadyUpdated = 0;
            int iCounter = 1;
            foreach (var  projectDepLevel in projectDependencyLevelOrderList)
            {
                var c = File.ReadAllText(projectDepLevel.CSProjPath);
                if (c.Contains(projectDepLevel.TargetFramework))
                {
                    alreadyUpdated++;
                    continue;
                }
                try
                {
                    string status = await UpgradeProject(projectDepLevel);
                    if (status == "Project Upgraded")
                    {
                        LogConsole($"Project {iCounter} Upgraded");
                        noUpdated++; iCounter++;
                        UpdatedProjects.Add(projectDepLevel.ProjectName);
                    }
                }
                catch (Exception ex)
                {
                    ProjErrors.Add(projectDepLevel.ProjectName, $"Couldn't upgrade project. Check for spaces in the project path");
                }

            }
           await PushUpgradeOperationLogs(noUpdated, alreadyUpdated);
            PrintLogs();
            return "Projects Upgraded";
        }



        private async Task<string> ReadPackagesFromConvertedProject()
        {
            var tcs = new TaskCompletionSource<string>();
            var convertedprojectList = _config.ProjectDependency.Level.Where(x => x.UpgradeStatus == "Converted").ToList();
            int projectswithNugetUpdated = 0;
            Logs.Add($"\n{BreakLine}\nPackages Update Summary\n{BreakLine}");
            try
            {
                int iCounter = 1;
                foreach (var convertedProject in convertedprojectList)
                {
                    var currentPackagesDictionary = GetPackagesList(convertedProject.CSProjPath);
                    Logs.Add(PushLogJustified("\nProject", convertedProject.ProjectName));
                    Logs.Add(PushLogJustified("No of Nuget Packages before Migration", $"{currentPackagesDictionary.Count}"));
                    int packageUpdateCount = 0;
                    foreach (var currentPackage in currentPackagesDictionary)
                    {
                        try
                        {
                            var latestPackages = await GetLatestPackageVersion(currentPackage.Key);
                            var latestVersion = latestPackages.OrderByDescending(v => v).FirstOrDefault();
                            if (latestVersion != null)
                            {
                                currentPackagesDictionary[currentPackage.Key] = latestVersion.ToString();
                                packageUpdateCount++;
                            }
                            else
                            {
                                if (!PackageErrors.ContainsKey(currentPackage.Key))
                                {
                                    PackageErrors.Add($"{currentPackage.Key}", $"The package is not available, current version is {currentPackage.Value}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!PackageErrors.ContainsKey(currentPackage.Key))
                            {
                                PackageErrors.Add($"{currentPackage.Key}", $"Unexpected Error");
                            }
                        }
                    }
                    LogConsole($"Nuget dependencies for project {iCounter} Updated");
                    iCounter++;
                    Logs.Add(PushLogJustified("No of Nuget Packages upgraded", $"{packageUpdateCount}"));
                    projectswithNugetUpdated++;

                    UpdatePackages(currentPackagesDictionary, convertedProject.CSProjPath);
                }
                
                Logs.Add(PushLogJustified("\nNo of Projects for which Nuget Packages upgraded", $"{projectswithNugetUpdated}"));
                tcs.SetResult("Packages Upgraded");
                await tcs.Task;
                return await tcs.Task;
            }
            catch(Exception ex)
            {
                return "Not Updated";
            }
        }

        private void UpdatePackages(Dictionary<string, string> updatedPackages, string path)
        {
            try
            {
                XDocument doc = XDocument.Load(path);
                XNamespace ns = doc.Root.GetDefaultNamespace();
                var referenceElements = doc.Descendants(ns + "PackageReference");
                foreach (var Packagereference in referenceElements)
                {
                    var includeAttribute = Packagereference.Attribute("Include");
                    var versionAttribute = Packagereference.Attribute("Version");
                    if (includeAttribute != null && versionAttribute != null)
                    {
                        var packageName = includeAttribute.Value;
                        if (updatedPackages.ContainsKey(packageName))
                        {
                            versionAttribute.Value = updatedPackages[packageName];
                        }
                    }
                }
                doc.Save(path);
            }
            catch (Exception ex)
            {
                //  _logger.LogInformation($"Exception occured: {ex}");
                _logger.LogInformation("Error in updating package info");
            }
        }

        private async Task<IEnumerable<NuGetVersion>> GetLatestPackageVersion(string packageName, string targetFramework = "net8.0")
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;
            SourceCacheContext cache = new SourceCacheContext();
            //SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            
            List<NuGetVersion> compatibleVersions = new List<NuGetVersion>();

            //FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();
            //var framework = NuGetFramework.ParseFolder(targetFramework);
            try
            {
                IEnumerable<IPackageSearchMetadata> versions = await resource.GetMetadataAsync(
                packageName,
                includePrerelease: true,
                includeUnlisted: false,
                cache,
                logger,
                cancellationToken);

                foreach (var version in versions)
                {
                    //var supportsTargetFramework = version.DependencySets
                    //.Any(group => group.TargetFramework != null && group.TargetFramework == framework);

                    //I am adding every version in compatible list right now because the TargetFramework property
                    //of any version return the lowest framework version it supports, so net8.0 is compatible with every version of package.
                    compatibleVersions.Add(version.Identity.Version);
                }
                return compatibleVersions;
            }
            catch (Exception ex)
            {
                return new List<NuGetVersion>();
            }
        }


        private Dictionary<string, string> GetPackagesList(string path)
        {
            var content = File.ReadAllText(path);
            XDocument doc = XDocument.Parse(content);
            var dictionary = new Dictionary<string, string>();

            foreach (var element in doc.Descendants("PackageReference"))
            {
                var key = element.Attribute("Include")?.Value;
                var value = element.Attribute("Version")?.Value;

                if (key != null && value != null)
                {
                    dictionary[key] = value;
                }
            }
            return dictionary;
        }

        private void SetProjectPath()
        {
            var directoryPath = _config.ProjectDependency.SolutionPath;
            if (!Directory.Exists(directoryPath)) 
            {
                Console.WriteLine($"The directory '{directoryPath}' does not exist.");
            }
            try
            {
                string[] txtFiles = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
                var d = new Dictionary<string, string>();
                foreach (string projPath in txtFiles)
                {
                    var splitResult = projPath.Split('\\');
                    var projectName = splitResult[splitResult.Length - 2];
                    d.Add(projectName, projPath);

                    var filteredProject =_config.ProjectDependency.Level.Where(p => p.ProjectName == projectName).FirstOrDefault();
                    if (filteredProject != null)
                    {
                        filteredProject.CSProjPath = projPath;
                        filteredProject.UpgradeStatus = "Converted";
                    }
                }

            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");

            }
        }

        private async Task<string> UpgradeProject(Level projectDependencyLevel)
        {
            try
            {
                var command = _config.UpgradeConfiguration.Command.Replace("%FilePath%", projectDependencyLevel.CSProjPath);
                command = command.Replace("%TargetFramework%", projectDependencyLevel.TargetFramework);
                var tcs = new TaskCompletionSource<string>();
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = _config.UpgradeConfiguration.ExeName;
                    process.StartInfo.Arguments = $"/c {command}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.RedirectStandardError = false;
                    process.EnableRaisingEvents = true;
                    process.Exited += (sender, e) =>
                    {
                        tcs.SetResult("Project Upgraded");
                        process.Dispose();
                    };
                    process.Start();
                    var delayTask = Task.Delay(40000);
                    var completedTask = await Task.WhenAny(tcs.Task, delayTask);

                    if (completedTask == delayTask)
                    {
                        return "Timed out";
                    }
                    await tcs.Task;
                    return await tcs.Task;
                }

            }
            catch (Exception ex)
            {
                ProjErrors.Add(projectDependencyLevel.ProjectName, ex.ToString());
                return "Not Upgraded";
            }
        }

        private Dictionary<string, string> GetCsProjFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"The directory '{directoryPath}' does not exist.");
                return new Dictionary<string, string>();
            }
            try
            {
                string[] txtFiles = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
                var d = new Dictionary<string, string>();
                foreach (string txtFile in txtFiles)
                {
                    var s = txtFile.Split('\\');
                    d.Add(s[s.Length - 2], txtFile);
                }
                return d;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new Dictionary<string, string>();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }


        private void InitializeLoggerVariables()
        {
            //var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //string logFileLocation = Path.Combine(assemblyLocation, "Logs.txt");
            //var d = logFileLocation.Split("\\");
            //d.Skip(0).Take(d.Length - 3).ToArray();
            //logFileLocation = logFileLocation + "\\..\\..\\..\\Logs.txt";

            string currentDirectory = Directory.GetCurrentDirectory();
            var logFileLocation = currentDirectory + "\\Logs.txt";
            //File.Create(logFileLocation);
            _logger = new(logFileLocation);
        }

        private string PushLogJustified(string log1,string log2)
        {
            var spaces = 46 - log1.Length;
            log1 = log1 + string.Concat(Enumerable.Repeat(" ", spaces));
            log2 = log1 + $":{log2}";
            return log2;
        }

        private void PushLogMinimal(string log)
        {
            _logger.LogMinimal(log);
        }

        private async Task PushUpgradeOperationLogs(int noUpdated, int alreadyUpdated)
        {
            Logs.Add(PushLogJustified("No of Projects migrated", $"{noUpdated}"));
            //PushLogJustified("No of Projects up to targetFramework", $"{alreadyUpdated}");
            //listing name of projects
            if (UpdatedProjects.Count > 0)
            {
                Logs.Add("\nProjects Upgraded");
                foreach (var pname in UpdatedProjects)
                {
                    Logs.Add(pname);
                }
            }
            //listing project errors
            if (ProjErrors.Count > 0)
            {
                Logs.Add($"\nProject Errors");
                foreach (var b in ProjErrors)
                {
                    Logs.Add($"{b.Key} : {b.Value}");
                }
            }
            await ReadPackagesFromConvertedProject();
            //listing package errors
            if (PackageErrors.Count > 0)
            {
                Logs.Add("\n Package Errors");
                foreach (var b in PackageErrors)
                {
                    Logs.Add(PushLogJustified(b.Key, b.Value));
                }
            }
        }

        private void PushUpgradeOperationSummaryCountLog(int ProjectsToBeMigratedCount)
        {
            Logs.Add("\n" + BreakLine + $"\nProjects Upgrade Operation Summary \n" + BreakLine);
            Logs.Add(PushLogJustified("No of Projects to be migrated", $"{ProjectsToBeMigratedCount}"));
            Logs.Add("");
        }

        private void PrintLogs()
        {
            foreach(var log in Logs)
            {
                _logger?.PrintData(log);
            }
        }
        private void LogConsole(string s)
        {
            Console.WriteLine(s);
        }
    }

}
