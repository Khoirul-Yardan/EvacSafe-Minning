using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SafeMining
{
    // JsonUtility is intentionally permissive. Check the flat v1 wire schema first so
    // duplicate keys, wrong primitive types, missing fields, and trailing text cannot pass.
    public static class MiningEdgeJson
    {
        const string JsonString = "\"(?:[^\"\\\\\\x00-\\x1F]|\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4}))*\"";
        static readonly Regex Field = new Regex("\\G\\s*(?<key>" + JsonString + ")\\s*:\\s*(?<value>" + JsonString +
            "|-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)\\s*(?<end>[,}])", RegexOptions.CultureInvariant);
        static readonly HashSet<string> Strings = new HashSet<string> { "sessionId", "layoutId", "eventId", "deviceId", "source" };
        static readonly HashSet<string> Integers = new HashSet<string> { "schemaVersion", "sequence", "level" };
        public static EdgeStatusMessage Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 8192) return null;
            json = json.Trim(); if (json.Length < 2 || json[0] != '{') return null;
            var seen = new HashSet<string>(); int offset = 1;
            while (offset < json.Length)
            {
                var match = Field.Match(json, offset); if (!match.Success) return null;
                string key = match.Groups["key"].Value; key = key.Substring(1, key.Length - 2);
                string value = match.Groups["value"].Value;
                if (!seen.Add(key)) return null;
                if (Strings.Contains(key)) { if (value[0] != '"') return null; }
                else
                {
                    if (!Integers.Contains(key) && key != "simulationTimeS" && key != "vibrationNormalized") return null;
                    if (value[0] == '"') return null;
                    if (Integers.Contains(key) && !long.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture, out _)) return null;
                    if ((key == "schemaVersion" || key == "level") && !int.TryParse(value,
                        System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out _)) return null;
                }
                offset = match.Index + match.Length;
                if (match.Groups["end"].Value == "}")
                {
                    if (offset != json.Length || seen.Count != 10) return null;
                    try { var message = new EdgeStatusMessage(); JsonUtility.FromJsonOverwrite(json, message); return message; }
                    catch (ArgumentException) { return null; }
                }
            }
            return null;
        }
    }
}
