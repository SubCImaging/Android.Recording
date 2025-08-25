// <copyright file="Shell.cs" company="SubC Imaging">
// Copyright (c) SubC Imaging. All rights reserved.
// </copyright>

namespace SubC.Rayfin.Helpers
{
    using Java.IO;
    using Java.Lang;

    /// <summary>
    /// Responsible for providing shell access.
    /// </summary>
    public class Shell
    {
        /// <summary>
        /// Runs a shell command on the Rayfin.
        /// </summary>
        /// <param name="command">The command you wish to execute.</param>
        /// <param name="timeout">The maximum time the command is allowed to run before timing out.</param>
        /// <param name="su">Whether or not to run as super user.</param>
        /// <returns>Anything that comes from stdout.</returns>
        public string Execute(string command, int timeout = 0, bool su = true)
        {
            try
            {
                // Run the command
                var log = new System.Text.StringBuilder();
                Java.Lang.Process process;
                if (su)
                {
                    process = Runtime.GetRuntime().Exec(new[] { "su", "-c", command });
                }
                else
                {
                    process = Runtime.GetRuntime().Exec(new[] { "/system/bin/sh", "-c", command });
                }

                var bufferedReader = new BufferedReader(
                new InputStreamReader(process.InputStream));

                // Grab the results
                if (timeout > 0)
                {
                    process.Wait(timeout);
                    return string.Empty;
                }

                string line;

                while ((line = bufferedReader.ReadLine()) != null)
                {
                    log.AppendLine(line);
                }

                return log.ToString();
            }
            catch (System.Exception ex)
            {
                // return ex.Message;
                return string.Empty;
            }
        }
    }
}
