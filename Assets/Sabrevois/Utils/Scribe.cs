using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Sabrevois.Utils
{
    public class Scribe : ILogHandler
    {
        private readonly ILogHandler _log = Debug.unityLogger.logHandler;

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return;
#endif
            var stackTrace = new StackTrace();
            var frame = stackTrace.GetFrame(3);
            Type type = frame.GetMethod().DeclaringType;
            string className = type == null ? "Unknown" : GetPrettyName(type);
            string color = GetUniqueColor(className);
            string output = $"<b><color={color}>[{className}]</color></b> {string.Format(format, args)}";

            if (!output.EndsWith(".") && !output.EndsWith("!") && !output.EndsWith("?") && !output.EndsWith("\n"))
                output += ".";

            _log.LogFormat(logType, context, output);
        }

        public void LogException(Exception exception, Object context)
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return;
#endif
            _log.LogException(exception, context);
        }

        private static string GetPrettyName(Type type)
        {
            var name = type.Name;

            if (type.IsGenericType)
            {
                var genericArguments = type.GetGenericArguments();
                var unmangledName = name[..name.IndexOf('`')];
                return $"{unmangledName}<{string.Join(", ", genericArguments.Select(GetPrettyName))}>";
            }

            int start = name.IndexOf('<');
            if (start != -1)
            {
                int end = name.IndexOf('>');
                return name[(start + 1)..end];
            }

            return name;
        }

        private readonly Dictionary<string, string> _colorsCache = new();

        private string GetUniqueColor(string str)
        {
            if (_colorsCache.TryGetValue(str, out var value))
                return value;

            int hash = str.GetHashCode(); // unique for every input

            byte r = (byte)((hash >> 16) & 0xFF);
            byte g = (byte)((hash >> 8) & 0xFF);
            byte b = (byte)(hash & 0xFF);

            // ensure the color is well-saturated
            r = (byte)(r | 0x40);
            g = (byte)(g | 0x40);
            b = (byte)(b | 0x40);

            // prevent the color from being too dark
            if (r < 0x80 && g < 0x80 && b < 0x80)
            {
                r = (byte)(r | 0x80);
                g = (byte)(g | 0x80);
                b = (byte)(b | 0x80);
            }

            // convert to hex
            return _colorsCache[str] = $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
