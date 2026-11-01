using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using RbnSystemPro.Models;

namespace RbnSystemPro.Services
{
    public interface IDeployService
    {
        Task<bool> RunRaphireWinDeployAsync();
    }

    public class DeployService : IDeployService
    {
        private readonly ILoggerService _logger;
        private readonly ITimelineService _timeline;

        public DeployService(ILoggerService logger, ITimelineService timeline)
        {
            _logger = logger;
            _timeline = timeline;
        }

        public async Task<bool> RunRaphireWinDeployAsync()
        {
            try
            {
                await _logger.LogInfoAsync("Starting Raphire WinDeploy script...");
                string script = "& ([scriptblock]::Create((irm \"https://debloat.raphi.re/\")))";
                
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized,
                    Verb = "runas"
                };

                await Task.Run(() => 
                {
                    var process = System.Diagnostics.Process.Start(psi);
                    process?.WaitForExit();
                });

                _timeline.AddEventKey("event.deploy.title", "event.deploy.desc", TimelineEventType.Optimization);
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to execute Raphire WinDeploy", ex);
                return false;
            }
        }
    }
}
