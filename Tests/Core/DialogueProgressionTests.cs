using System;
using System.IO;
using System.Text.Json;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class DialogueProgressionTests
    {
        public static void RunAll()
        {
            DialogueCompletionIsIdempotentAndPersists();
        }

        private static void DialogueCompletionIsIdempotentAndPersists()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-dialogue-progress-" + Guid.NewGuid().ToString("N"));
            try
            {
                SaveData state = new SaveData();
                dynamic messages = new MessageService(state);
                Equal(false, (bool)messages.IsDialogueCompleted("kaito", "Kaito_Start"),
                    "new conversation must not be completed");
                Equal(true, (bool)messages.MarkDialogueCompleted("kaito", "Kaito_Start"),
                    "first completion marker must persist");
                Equal(false, (bool)messages.MarkDialogueCompleted("kaito", "Kaito_Start"),
                    "repeated completion marker must be idempotent");
                Equal(true, (bool)messages.IsDialogueCompleted("kaito", "Kaito_Start"),
                    "marked dialogue must report completed");

                SaveStore store = new SaveStore(directory, new JsonCodec());
                Equal(SaveWriteStatus.Saved, store.Save(state).Status, "dialogue completion should save");
                dynamic restored = new MessageService(store.Load().Data);
                Equal(true, (bool)restored.IsDialogueCompleted("kaito", "Kaito_Start"),
                    "dialogue completion must survive save/load");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
        }

        private sealed class JsonCodec : ISaveCodec
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
            public string Serialize(SaveData data) { return JsonSerializer.Serialize(data, options); }
            public bool TryDeserialize(string text, out SaveData data, out string error)
            {
                try
                {
                    data = JsonSerializer.Deserialize<SaveData>(text, options);
                    error = data == null ? "Save contained null." : string.Empty;
                    return data != null;
                }
                catch (Exception exception)
                {
                    data = null;
                    error = exception.Message;
                    return false;
                }
            }
        }
    }
}
