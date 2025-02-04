using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ProjectCreator.Service
{
    public class ProjectCreationConfig
    {
        public string ProjectType { get; set; } = string.Empty;

        public string Command { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public ConfigFilePaths ConfigFilePaths { get; set; }

        public string WCFConfigFilePath { get; set; } = string.Empty;

        public Usings[] Usings { get; set; }


        public ServiceInfo[] ServiceInfo { get; set; }

        public UpgradeConfiguration UpgradeConfiguration { get; set; }

        public ProjectDependency ProjectDependency { get; set; }

        public NugetPackages[] NugetPackages { get; set; }



    }

    public class ConfigFilePaths
    {
        public string WCFConfigFilePath { get; set; } = string.Empty;

        public string WebConfigFilePath { get; set; } = string.Empty;
    }
    public class NugetPackages
    {
        public string PackageName { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;
    }

    public class Usings
    {
        public string UsingNameSpace { get; set; } = string.Empty;
    }

    public class ServiceInfo
    {
        public string ProjectId { get; set; } = string.Empty;

        public bool IsNewServiceProjectToBeCreated { get; set; }

        public string ServiceProjectLocation { get; set; } = string.Empty ;

        public string ProjectReference { get; set; } = string.Empty;

        public string csProjLocation { get;set; } = string.Empty ;

        public string Namespace { get; set; } = string.Empty ;

        public string Type { get;set; } = string.Empty ;

        public string ServiceInterface { get; set; } = string.Empty;
    }

    public class UpgradeConfiguration
    {
        public string Command { get; set; } = string.Empty;

        public string ExeName { get; set; } = string.Empty;
    }

    public class ProjectDependency
    {
        public List<Level> Level { get; set; }
        public string SolutionPath { get; set; }
    }

    public class Level
    {
        public string ProjectName { get; set; }
        public int LevelOrder { get; set; }
        public string CSProjPath { get; set; }
        public string TargetFramework { get; set; }
        public string UpgradeStatus { get; set; }
    }
}
