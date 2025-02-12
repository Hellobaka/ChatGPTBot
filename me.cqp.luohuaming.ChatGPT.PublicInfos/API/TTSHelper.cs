using System.Diagnostics;
using System.IO;
using System.Text;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class TTSHelper
    {
        public static bool Enabled { get; set; }

        public static bool TTS(string text, string outFilePath, string voice)
        {
            if (!Enabled)
            {
                return false;
            }
            MainSave.CQLog.Debug("TTS-Text", text);
            text = text.Replace("\r", "。").Replace("\n", "。");
            string command = $"edge-tts --text \"{text}\" --write-media \"{outFilePath}\" --voice {voice}";

            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd.exe",
                Arguments = "/c chcp 65001",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using Process process = Process.Start(startInfo);
            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
            process.StandardInput.Close();
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            bool success = !output.Contains("Traceback") && !error.Contains("Traceback");
            if (!success)
            {
                MainSave.CQLog.Debug("TTS", output);
                MainSave.CQLog.Debug("TTS", error);
            }
            return success && File.Exists(outFilePath);
        }
    }
}
