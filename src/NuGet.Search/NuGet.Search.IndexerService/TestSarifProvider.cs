using NuGet.Search.Common.ElasticSearch.Sarif;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.IndexerService
{
    public class TestSarifProvider : ISarifProvider
    {
        public IEnumerable<ResultLog> GetNextBatch()
        {
            ResultLog log = new ResultLog("635908320000000000_MIKEFAN-SERVER0_Ng.exe_");
            log.Version = "1.0.0." + DateTime.Now.Ticks;
            log.RunLogs = new List<RunLog>();

            RunLog runLog = new RunLog();
            runLog.ToolInfo = new ToolInfo();
            runLog.ToolInfo.FullName = "NuGet Crawler";
            runLog.ToolInfo.FileVersion = "1.0.0.0";
            runLog.ToolInfo.Name = "NG";
            runLog.ToolInfo.Version = "1.0.0.0";

            log.RunLogs.Add(runLog);

            return new ResultLog[] { log };
        }
    }
}
