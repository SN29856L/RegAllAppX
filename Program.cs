using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace RegAllAppX
{
    static class Program
    {
        internal static StreamWriter LogFile;
        internal static bool doingForceRegister = false;
        internal static bool loggingEnabled = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            doingForceRegister = 
                args.Contains<string>("/force", StringComparer.InvariantCultureIgnoreCase)
              | args.Contains<string>("-force", StringComparer.InvariantCultureIgnoreCase);

            string LogFileName = Path.Combine(Environment.CurrentDirectory, @"Logs\" + Environment.UserName + "-RegAllAppx.log");

            if (doingForceRegister)
            {
                LogFileName = Path.Combine(Environment.CurrentDirectory, @"Logs\" + Environment.UserName + "-RegAllAppx-Force.log");
            }

            try
            {
                LogFile = File.CreateText(LogFileName);
                LogFile.AutoFlush = true;
                loggingEnabled = true;
            }
            catch
            {
            }

            LogThis(string.Format("Logging started: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString()));
            LogThis();
            LogThis("[Register all installed AppX packages]");

            int returnValue = 0;

            try
            {
                PackageManager packageManager = new PackageManager();

                foreach (Package item in packageManager.FindPackagesForUser(string.Empty))
                {
                    try
                    {
                        string displayName = "Unknown";
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(item.DisplayName))
                                displayName = item.DisplayName;
                            else
                            if (!string.IsNullOrWhiteSpace(item.Id.Name))
                                displayName = item.Id.Name;

                            LogThis("[" + DateTime.Now.ToLongTimeString() + "]" + " Registering: " + displayName + " [" + item.InstalledLocation.Path + "]");
                        }
                        catch (FileNotFoundException)
                        {
                            LogThis("[" + DateTime.Now.ToLongTimeString() + "]" + " Registering: " + displayName + " [Error: File not found]");
                        }

                        DeploymentOptions deploymentOptions = DeploymentOptions.None;

                        if (doingForceRegister)
                            deploymentOptions = DeploymentOptions.ForceApplicationShutdown | DeploymentOptions.ForceTargetApplicationShutdown;

                        IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> deploymentOperation =
                            packageManager.RegisterPackageAsync(
                                new Uri(item.InstalledLocation.Path + @"\appxmanifest.xml"),
                                null,
                                deploymentOptions);

                        ManualResetEvent opCompletedEvent = new ManualResetEvent(false);

                        deploymentOperation.Completed = (depProgress, status) => { opCompletedEvent.Set(); };

                        opCompletedEvent.WaitOne();

                        if (deploymentOperation.Status == AsyncStatus.Error)
                        {
                            DeploymentResult deploymentResult = deploymentOperation.GetResults();
                            LogThis(string.Format("Error code: {0}", deploymentOperation.ErrorCode));
                            LogThis(string.Format("Error text: {0}", deploymentResult.ErrorText));
                            returnValue = deploymentOperation.ErrorCode.HResult;
                        }
                        else if (deploymentOperation.Status == AsyncStatus.Canceled)
                        {
                            LogThis("--> Installation cancelled");
                            returnValue = 1602;
                        }
                        else if (deploymentOperation.Status == AsyncStatus.Completed)
                        {
                            LogThis("--> Installation succeeded");
                            returnValue = 0;
                        }
                        else
                        {
                            returnValue = 1;
                            LogThis("--> Installation status unknown");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogThis("--> ERROR : " + ex.Message);
                        returnValue = ex.HResult;
                    }
                }
            }
            catch(Exception ex)
            {
                LogThis("ERROR : "+ex.Message);
                returnValue = ex.HResult;
            }

            LogThis("All done.");
            LogThis();
            LogThis(string.Format("Logging stopped: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString()));

            Environment.Exit(returnValue);
        }

        private static void LogThis(string logentry = "")
        {
#if DEBUG
            Debug.WriteLine(logentry);
#endif
            if (loggingEnabled)
            {
                LogFile.WriteLine(logentry);
                LogFile.Flush();
            }
        }
    }
}
