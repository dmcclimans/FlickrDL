using System;
using System.Threading;
using System.Diagnostics;

namespace FlickrDL
{
	public class ExecuteCommand
	{
        // StdOut result of command
		public string StdOut { get; set; }  = null;
        // StdErr result of command
        public string StdErr { get; set; } = null;
        // Exit code of command
        public int ExitCode { get; set; } = 0;

        // Execute a command by calling the command processor on the string cmd.
        // Returns stdOut concatenated with stdErr.
		public string Execute(string cmd)
		{
			string program = Environment.ExpandEnvironmentVariables("\"%COMSPEC%\"");
			string args = "/c " + cmd;
			this.psi = new ProcessStartInfo(program, args);
			this.psi.CreateNoWindow = true;
            this.psi.UseShellExecute = false;
			this.psi.RedirectStandardOutput = true;
			this.psi.RedirectStandardError = true;
			Thread thread_ReadStandardError = new Thread(new ThreadStart(Thread_ReadStandardError));
			Thread thread_ReadStandardOut = new Thread(new ThreadStart(Thread_ReadStandardOut));

			activeProcess = Process.Start(psi);
			if (psi.RedirectStandardError)
			{
				thread_ReadStandardError.Start();
			}
			if (psi.RedirectStandardOutput)
			{
				thread_ReadStandardOut.Start();
			}
			activeProcess.WaitForExit();
            ExitCode = activeProcess.ExitCode;

            if (psi.RedirectStandardError)
            {
                thread_ReadStandardError.Join();
            }
            if (psi.RedirectStandardOutput)
            {
                thread_ReadStandardOut.Join();
            }

			string output = StdOut + StdErr;

			return output;
		}

        private ProcessStartInfo psi = null;
        private Process activeProcess = null;

        private void Thread_ReadStandardError()
        {
            if (activeProcess != null)
            {
                StdErr = activeProcess.StandardError.ReadToEnd();
            }
        }

        private void Thread_ReadStandardOut()
        {
            if (activeProcess != null)
            {
                StdOut = activeProcess.StandardOutput.ReadToEnd();
            }
        }

    }
}
