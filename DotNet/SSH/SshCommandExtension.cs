using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace RService.Extensions
{
    public static class SshCommandExtension
    {
        public static async Task ExecuteAsync(this SshCommand sshCommand, Action<bool, string> onWrite = null,
            Func<string, bool> errorValidator = null)
        {
            var asyncResult = sshCommand.BeginExecute();
            var stdoutReader = new StreamReader(sshCommand.OutputStream);
            var stderrReader = new StreamReader(sshCommand.ExtendedOutputStream);

            var stderrTask =
                CheckOutputAndReportProgressAsync(sshCommand, asyncResult, stderrReader, onWrite, true, errorValidator);
            var stdoutTask =
                CheckOutputAndReportProgressAsync(sshCommand, asyncResult, stdoutReader, onWrite, false, errorValidator);

            await Task.WhenAll(stderrTask, stdoutTask);

            sshCommand.EndExecute(asyncResult);
        }

        private static async Task CheckOutputAndReportProgressAsync(
            SshCommand sshCommand,
            IAsyncResult asyncResult,
            StreamReader streamReader,
            Action<bool, string> onWrite,
            bool isError,
            Func<string, bool> errorValidator = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            while (!asyncResult.IsCompleted || !streamReader.EndOfStream)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sshCommand.CancelAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();

                string line = await streamReader.ReadLineAsync();

                if (!string.IsNullOrWhiteSpace(line) && onWrite != null)
                {
                    bool isLineError = isError;
                    if (!isLineError && errorValidator != null)
                    {
                        isLineError = errorValidator(line);
                    }

                    onWrite(isLineError, line);
                }

                await Task.Delay(100, cancellationToken);
            }
        }
    }
}
