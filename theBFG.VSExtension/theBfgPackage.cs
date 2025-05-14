using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace BfgPortalExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideAutoLoad(UIContextGuids.NoSolution)]
    [ProvideMenuResource("Menus.ctxt", 1)]
    public sealed class BfgPortalPackage : Package
    {
        private static BfgPortalPackage _instance;
        private static readonly object _lock = new object();
        private static Process _bfgProcess;
        private static WpfPanel _portalPanel;

        public static BfgPortalPackage Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new BfgPortalPackage();
                        }
                    }
                }
                return _instance;
            }
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Register command for shortcut key
            var mcs = GetService(typeof(IMenuCommandService)) as IMenuCommandService;
            if (null != mcs)
            {
                CommandID cmdId = new CommandID(new Guid("BfgPortalExtensionGuid"), 0x100);
                MenuCommand menuItem = new MenuCommand(ExecuteBfgPortal, cmdId);
                mcs.AddCommand(menuItem);

                // Add menu item to Test Explorer context menu
                cmdId = new CommandID(new Guid("13B08420-6F1D-4B8E-93CE-6A3FDD869841"), 0x1000);
                menuItem = new MenuCommand(ExecuteSelectedTest, cmdId);
                mcs.AddCommand(menuItem);
            }
        }

        private void ExecuteBfgPortal(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await LaunchBfgPortal(string.Empty);
            });
        }

        private void ExecuteSelectedTest(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var dte = (DTE2)GetService(typeof(DTE));
                if (dte == null)
                    return;

                var selectedTests = dte.ToolWindows.TestExplorer.SelectedItems;
                if (selectedTests.Count == 0)
                    return;

                var test = selectedTests[0] as UITestElement;
                if (test == null)
                    return;

                var testAssembly = test.Source;
                var testClassName = test.FullName.Split('.').Reverse().Take(2).Reverse().JoinString(".");
                var testParams = $"{testAssembly}:{testClassName}";

                await LaunchBfgPortal(testParams);
            });
        }

        private async Task LaunchBfgPortal(string testParams)
        {
            if (_bfgProcess != null)
            {
                IVsUIShell shell = (IVsUIShell)GetService(typeof(SVsUIShell));
                if (shell != null)
                {
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(
                        shell.ShowMessageBox(
                            0,
                            "BFG Portal",
                            "BFG portal is already running.",
                            OLEMSGICON.OLEMSGICON_INFO,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_DEFAULT1
                        )
                    );
                }
                return;
            }

            try
            {
                _bfgProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "thebfg",
                        Arguments = $"fire {testParams}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                _bfgProcess.OutputDataReceived += (sender, data) =>
                    System.Diagnostics.Debug.WriteLine($"BFG Output: {data.Data}");
                _bfgProcess.ErrorDataReceived += (sender, data) =>
                    System.Diagnostics.Debug.WriteLine($"BFG Error: {data.Data}");

                _bfgProcess.Start();
                _bfgProcess.BeginOutputReadLine();
                _bfgProcess.BeginErrorReadLine();

                await WaitForServer(888);

                _portalPanel = new WpfPanel("BFG Portal", "http://localhost:888");
                _portalPanel.Closed += (sender, e) =>
                {
                    if (_bfgProcess != null)
                    {
                        _bfgProcess.Kill();
                        _bfgProcess = null;
                    }
                };
            }
            catch (Exception ex)
            {
                IVsUIShell shell = (IVsUIShell)GetService(typeof(SVsUIShell));
                if (shell != null)
                {
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(
                        shell.ShowMessageBox(
                            0,
                            "BFG Portal",
                            $"Failed to start BFG portal: {ex.Message}",
                            OLEMSGICON.OLEMSGICON_CRITICAL,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_DEFAULT1
                        )
                    );
                }
            }
        }

        private async Task WaitForServer(int port, int retries = 0)
        {
            if (retries >= 10)
                throw new TimeoutException("Failed to connect to server");

            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("localhost", port);
                return;
            }
            catch
            {
                await Task.Delay(500);
                await WaitForServer(port, retries + 1);
            }
        }
    }
}