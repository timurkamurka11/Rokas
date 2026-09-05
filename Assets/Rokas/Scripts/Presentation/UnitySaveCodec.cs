using System;
using Rokas.Core;
using UnityEngine;

namespace Rokas.Presentation
{
    public sealed class UnitySaveCodec : ISaveCodec
    {
        public string Serialize(SaveData data) { return JsonUtility.ToJson(data, true); }

        public bool TryDeserialize(string text, out SaveData data, out string error)
        {
            data = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith("{"))
            {
                error = "Save is not a JSON object.";
                return false;
            }
            try
            {
                data = JsonUtility.FromJson<SaveData>(text);
                if (data == null) { error = "Empty save object."; return false; }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
