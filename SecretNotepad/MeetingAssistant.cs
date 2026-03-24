using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SecretNotepad
{
    public class MeetingAssistant
    {
        private WasapiLoopbackCapture capture;
        private MemoryStream rawAudioStream;
        private WaveFileWriter waveWriter;
        private DateTime lastAudioTime;
        private bool isRecording = false;
        private Form1 mainForm;

        // A lock object to prevent thread-collision crashes
        private readonly object audioLock = new object();

        // TODO: Insert your Gemini API Key here
        private readonly string geminiApiKey = "AIzaSyBUTG8cWBFMfRcRMN5GPbynOsQek3E3";
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        private void WriteLog(string message)
        {
            try
            {
                // Writes to a file named debug_log.txt in your bin/Debug folder
                File.AppendAllText("debug_log.txt", $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
            catch { } // Ignore logging errors so it doesn't crash the app
        }

        public MeetingAssistant(Form1 form)
        {
            mainForm = form;
            capture = new WasapiLoopbackCapture();
            capture.DataAvailable += OnAudioDataAvailable;
        }

        public void StartListening()
        {
            ResetAudioStream();
            capture.StartRecording();
            lastAudioTime = DateTime.Now;
            isRecording = true;
        }

        private void ResetAudioStream()
        {
            lock (audioLock)
            {
                // FIX: Only dispose waveWriter. It will safely dispose the stream for us.
                waveWriter?.Dispose();

                rawAudioStream = new MemoryStream();
                waveWriter = new WaveFileWriter(rawAudioStream, capture.WaveFormat);
            }
        }

        private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                lock (audioLock)
                {
                    if (!isRecording) return;

                    // 1. Calculate Volume (Wasapi natively uses 32-bit floats)
                    float maxVolume = 0;
                    for (int index = 0; index < e.BytesRecorded; index += 4)
                    {
                        float sample = BitConverter.ToSingle(e.Buffer, index);
                        if (sample < 0) sample = -sample;
                        if (sample > maxVolume) maxVolume = sample;
                    }

                    // 2. Track speaking vs silence (Adjusted threshold for float precision)
                    if (maxVolume > 0.005f)
                    {
                        waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        lastAudioTime = DateTime.Now;
                    }

                    // 3. Trigger Gemini if 2.5 seconds of silence passed AND we have audio
                    if (rawAudioStream.Length > 10000 && (DateTime.Now - lastAudioTime).TotalSeconds > 2.5)
                    {
                        isRecording = false; // Pause listening while processing

                        // Run the API call on a separate task so we don't block the audio thread
                        Task.Run(() => ProcessAudioWithGemini());
                    }
                }
            }
            catch (Exception ex)
            {
                mainForm.AppendAIResponse($"[Capture Error: {ex.Message}]");
            }
        }

        private async Task ProcessAudioWithGemini()
        {
            try
            {
                WriteLog("--- NEW QUESTION TRIGGERED ---");
                mainForm.AppendAIResponse("...");

                byte[] optimizedAudioBytes;
                long originalSize;

                lock (audioLock)
                {
                    waveWriter.Flush();
                    originalSize = rawAudioStream.Length;
                    rawAudioStream.Position = 0; // Rewind the stream so we can read it

                    var targetFormat = new WaveFormat(16000, 16, 1);

                    // THE FIX: We use WaveFileReader so it correctly parses the WAV header we created
                    using (var reader = new WaveFileReader(rawAudioStream))
                    using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                    using (var outStream = new MemoryStream())
                    {
                        resampler.ResamplerQuality = 60;
                        WaveFileWriter.WriteWavFileToStream(outStream, resampler);
                        optimizedAudioBytes = outStream.ToArray();
                    }
                }

                WriteLog($"Audio compressed. Original: {originalSize} bytes -> Optimized: {optimizedAudioBytes.Length} bytes.");

                string base64Audio = Convert.ToBase64String(optimizedAudioBytes);
                WriteLog("Sending optimized payload to Gemini API...");

                string answer = await AskGemini(base64Audio);

                WriteLog($"Gemini processing complete. Answer length: {answer?.Length}");

                if (!string.IsNullOrWhiteSpace(answer))
                {
                    mainForm.AppendAIResponse(answer);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"CRITICAL ERROR in ProcessAudio: {ex.Message}\n{ex.StackTrace}");
                mainForm.AppendAIResponse($"[System Error: Check Logs]");
            }
            finally
            {
                ResetAudioStream();
                isRecording = true;
                WriteLog("Stream reset. Listening for next question...");
            }
        }

        // This will store the memory of the current meeting
        private string meetingContext = "";
        public void ClearSession()
        {
            meetingContext = "";
            WriteLog("--- SESSION MEMORY CLEARED ---");
        }

        private async Task<string> AskGemini(string base64Audio)
        {
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={geminiApiKey}";

            // THE FINAL PROMPT: Context-aware, keyword-spotting, and highly technical.
            string dynamicPrompt = $@"You are a discreet technical meeting assistant. 
Here is the context of what has been discussed in the meeting so far:
{meetingContext}

Listen to the attached audio and follow these rules:
1. If a direct technical question is asked, answer it crisply using bullet points.
2. If an important technical concept, keyword, or architecture is mentioned (e.g., C++ concepts, AWS Glue pipelines, networking, etc.), provide a brief 1-sentence definition or relevant context.
3. If the audio is just casual filler, greetings, or non-technical chatter, reply EXACTLY with: '[No question detected]'.";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = dynamicPrompt },
                            new { inline_data = new { mime_type = "audio/wav", data = base64Audio } }
                        }
                    }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            WriteLog("Executing HTTP POST...");
            HttpResponseMessage response = await httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode) return $"API Failed: {response.StatusCode}";

            try
            {
                string responseString = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(responseString))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return "";

                    var textResponse = candidates[0].GetProperty("content")
                                                    .GetProperty("parts")[0]
                                                    .GetProperty("text").GetString();

                    // Hide the response if it's just casual chatter
                    if (textResponse != null && textResponse.Contains("[No question detected]")) return "";

                    // UPDATE MEMORY: Save this answer so the AI remembers the context of the meeting
                    if (!string.IsNullOrWhiteSpace(textResponse))
                    {
                        meetingContext += $"- {textResponse}\n";

                        // Keep memory from overflowing during long meetings
                        if (meetingContext.Length > 2000)
                        {
                            meetingContext = meetingContext.Substring(meetingContext.Length - 2000);
                        }
                    }

                    return textResponse?.Trim();
                }
            }
            catch (Exception ex)
            {
                WriteLog($"JSON PARSING ERROR: {ex.Message}");
                return "[Error reading AI response format]";
            }
        }
    }
}