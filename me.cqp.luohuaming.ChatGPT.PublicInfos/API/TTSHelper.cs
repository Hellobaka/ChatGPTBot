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
            MainSave.CQLog?.Debug("TTS-Text", text);
            text = text.Replace("\r", "。").Replace("\n", "。");
            string command = $"edge-tts --text \"{text}\" --write-media \"{outFilePath}\" --voice {voice}";

            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd.exe",
                Arguments = $"/c chcp 65001 && {command}",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using Process process = Process.Start(startInfo);
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            bool success = !output.Contains("Traceback") && !error.Contains("Traceback");
            if (!success)
            {
                MainSave.CQLog?.Debug("TTS", output);
                MainSave.CQLog?.Debug("TTS", error);
            }
            return success && File.Exists(outFilePath);
        }

        public static void CheckTTS()
        {
            if (AppConfig.EnableTTS is false)
            {
                return;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd.exe",
                Arguments = "/c chcp 65001 && python --version",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using Process process = Process.Start(startInfo);
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            bool success = output.Contains("Python 3.") || error.Contains("Python 3.");

            if (!success)
            {
                MainSave.CQLog?.Debug("TTS_Output", output);
                MainSave.CQLog?.Debug("TTS_Error", error);
                MainSave.CQLog?.Error("TTS", "未检测到python环境");
            }

            TTSHelper.Enabled = success;
        }
    }
}
