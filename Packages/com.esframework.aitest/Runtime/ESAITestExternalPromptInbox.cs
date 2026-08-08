using System;
using System.IO;
using UnityEngine;

namespace ESFramework.ESAITest
{
    [Serializable]
    public sealed class ESAITestExternalPromptEnvelopeDto
    {
        public int protocolVersion;
        public string promptId;
        public string message;
        public string priority;
        public string source;
        public float timeToLiveSeconds;
        public long createdUtcTicks;
    }

    [DisallowMultipleComponent]
    public sealed class ESAITestExternalPromptInbox : MonoBehaviour
    {
        private const float FastPollSeconds = 0.1f;
        private const float IdlePollSeconds = 0.5f;
        private const int MaxFilesPerPoll = 8;
        private string inboxPath;
        private float nextPollTime;

        private void Awake()
        {
            inboxPath = Path.Combine(Application.persistentDataPath, "ESAITest", "prompt-inbox");
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPollTime)
                return;

            bool processedAny = PollInbox();
            nextPollTime = Time.unscaledTime + (processedAny ? FastPollSeconds : IdlePollSeconds);
        }

        private bool PollInbox()
        {
            try
            {
                if (!Directory.Exists(inboxPath))
                    return false;

                string[] files = Directory.GetFiles(inboxPath, "*.json", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                    return false;
                Array.Sort(files, StringComparer.Ordinal);

                int count = Mathf.Min(files.Length, MaxFilesPerPoll);
                for (int i = 0; i < count; i++)
                    ProcessFile(files[i]);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESAITest] External prompt inbox poll failed: " + exception.Message, this);
                return false;
            }
        }

        private void ProcessFile(string path)
        {
            string disposition = "rejected";
            try
            {
                ESAITestExternalPromptEnvelopeDto envelope = JsonUtility.FromJson<ESAITestExternalPromptEnvelopeDto>(File.ReadAllText(path));
                Validate(envelope);

                long now = DateTime.UtcNow.Ticks;
                long expires = envelope.createdUtcTicks + TimeSpan.FromSeconds(envelope.timeToLiveSeconds).Ticks;
                if (expires <= now)
                {
                    disposition = "expired";
                    return;
                }

                Enum.TryParse(envelope.priority, true, out ESAITestAIPromptPriority priority);
                ESAITestAIPrompt.Publish(
                    envelope.message,
                    priority,
                    "codex.external/" + envelope.source,
                    (float)TimeSpan.FromTicks(expires - now).TotalSeconds);
                disposition = "consumed";
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESAITest] External prompt rejected: " + Path.GetFileName(path) + " | " + exception.Message, this);
            }
            finally
            {
                MoveToDisposition(path, disposition);
            }
        }

        private static void Validate(ESAITestExternalPromptEnvelopeDto envelope)
        {
            if (envelope == null || envelope.protocolVersion != 1)
                throw new InvalidDataException("Unsupported prompt envelope protocol.");
            if (string.IsNullOrWhiteSpace(envelope.promptId))
                throw new InvalidDataException("promptId is required.");
            if (string.IsNullOrWhiteSpace(envelope.message) || envelope.message.Length > 4096)
                throw new InvalidDataException("message must contain 1..4096 characters.");
            if (string.IsNullOrWhiteSpace(envelope.source) || envelope.source.Length > 128)
                throw new InvalidDataException("source must contain 1..128 characters.");
            if (!Enum.TryParse(envelope.priority, true, out ESAITestAIPromptPriority priority)
                || !Enum.IsDefined(typeof(ESAITestAIPromptPriority), priority))
                throw new InvalidDataException("priority must be P0..P4.");
            if (envelope.timeToLiveSeconds < 1f || envelope.timeToLiveSeconds > 3600f)
                throw new InvalidDataException("timeToLiveSeconds must be 1..3600.");
            if (envelope.createdUtcTicks <= 0)
                throw new InvalidDataException("createdUtcTicks is invalid.");
        }

        private static void MoveToDisposition(string source, string disposition)
        {
            if (!File.Exists(source))
                return;
            string destination = Path.ChangeExtension(source, "." + disposition);
            if (File.Exists(destination))
                destination += "." + DateTime.UtcNow.Ticks;
            File.Move(source, destination);
        }
    }
}
