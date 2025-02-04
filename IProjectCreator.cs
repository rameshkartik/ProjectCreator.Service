using CoreWCF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectCreator.Service
{
    [ServiceContract]
    public interface IProjectCreator
    {
        [OperationContract]
        public Task<string> UpgradeProjectsAndApplyNugetPackages();


    }
}
