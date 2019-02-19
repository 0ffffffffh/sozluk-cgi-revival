using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace boot
{
    class Program
    {

        static string appExePath;
        static string appWorkDir;

        static int stricmp(string s1,string s2)
        {
            return String.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
        }

        static bool Run(string process, string args)
        {

            if (!File.Exists(process))
            {
                Console.WriteLine("{0} not found. Process creation failed.", process);
                return false;
            }

            ProcessStartInfo psi = new ProcessStartInfo(process, args)
            {
                UseShellExecute = true
            };

            return Process.Start(psi) != null;
        }

        static void CheckAdminRights()
        {
            WindowsPrincipal wp = new WindowsPrincipal(WindowsIdentity.GetCurrent());

            if (!wp.IsInRole(WindowsBuiltInRole.Administrator))
            {
                
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    Verb = "runas",
                    FileName = appExePath,
                    WorkingDirectory = appWorkDir
                };

                Process.Start(psi);
                Environment.Exit(0);
            }

        }

        static string BackendExePath
        {
            get
            {
                return appWorkDir + "\\backend\\sozluk_backend.exe";
            }
        }

        static string HttpdExePath
        {
            get
            {
                return appWorkDir + "\\httpd\\bin\\httpd.exe";
            }
        }

        static void Log(string format, params object[] args)
        {
            Console.WriteLine(string.Format(format, args));
        }

        static void Main(string[] args)
        {
            Process apache=null, backend=null, sbmon=null;
            List<Process> memcachedInsts = null;

            appExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            appWorkDir = Path.GetDirectoryName(appExePath);

            CheckAdminRights();

            var procList = Process.GetProcesses();

            foreach (var proc in procList)
            {
                if (stricmp(proc.ProcessName, "httpd") == 0)
                {
                    Log("Httpd still running");
                    apache = proc;
                }
                else if (stricmp(proc.ProcessName, "sozluk_backend") == 0)
                {
                    Log("Backend process still running");
                    backend = proc;
                }
                else if (stricmp(proc.ProcessName, "sbmon") == 0)
                {
                    Log("Sbmon still running");
                    sbmon = proc;
                }
                else if (stricmp(proc.ProcessName, "memcached") == 0)
                {
                    Log("A memcached instance found");
                    if (memcachedInsts == null)
                        memcachedInsts = new List<Process>();

                    memcachedInsts.Add(proc);
                }
            }

            if (apache == null)
            {
                Log("starting httpd");
                Run(HttpdExePath,string.Empty);
            }

            if (backend==null)
            {
                if (sbmon != null)
                {
                    Log("there is no backend but sbmon. Sbmon killing");
                    sbmon.Kill();
                }

                if (memcachedInsts!=null)
                {
                    Log("There some memcached instances. Killing em");

                    foreach (var mcp in memcachedInsts)
                    {
                        mcp.Kill();
                    }
                }


                Log("Starting up backend");
                Run(BackendExePath, string.Empty);
            }

            Console.ReadKey();

        }
    }
}
