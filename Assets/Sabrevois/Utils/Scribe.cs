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
        private readonly ILogHandler _original;

        [ThreadStatic]
        private static bool _isLogging;

        public Scribe(ILogHandler original)
        {
            _original = original ?? Debug.unityLogger.logHandler;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return;
#endif

            // Recursive guard: if we're already inside a Scribe.LogFormat call
            // (e.g. nested instances from domain reloads), pass through to original
            // without adding another prefix.
            if (_isLogging)
            {
                _original.LogFormat(logType, context, format, args);
                return;
            }

            _isLogging = true;
            try
            {
                var stackTrace = new StackTrace();
                string className = FindCallerClass(stackTrace);

                string color = GetUniqueColor(className);
                string message = args != null && args.Length > 0
                    ? string.Format(format, args)
                    : format;
                string output = $"<b><color={color}>[{className}]</color></b> {message}";

                if (!output.EndsWith(".") && !output.EndsWith("!") && !output.EndsWith("?") && !output.EndsWith("\n"))
                    output += ".";

                _original.LogFormat(logType, context, EscapeBraces(output));
            }
            finally
            {
                _isLogging = false;
            }
        }

        public void LogException(Exception exception, Object context)
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return;
#endif
            _original.LogException(exception, context);
        }

        /// <summary>
        /// Walks the stack trace, skipping Scribe and Debug frames, to find the actual
        /// calling class. This is robust against nested Scribe instances from domain reloads.
        /// </summary>
        private static string FindCallerClass(StackTrace stackTrace)
        {
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var method = stackTrace.GetFrame(i)?.GetMethod();
                var declaringType = method?.DeclaringType;

                if (declaringType == null)
                    continue;

                // Skip frames from the logging infrastructure
                if (declaringType == typeof(Scribe))
                    continue;
                if (declaringType == typeof(Debug))
                    continue;
                if (declaringType == typeof(Logger))
                    continue;
                if (declaringType.FullName?.StartsWith("UnityEngine.DebugLogHandler") == true)
                    continue;
                if (declaringType.FullName?.StartsWith("UnityEngine.Logger") == true)
                    continue;

                return GetPrettyName(declaringType);
            }

            return "Unknown";
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

        private static string EscapeBraces(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("{", "{{").Replace("}", "}}");
        }

        private readonly Dictionary<string, string> _colorsCache = new();

        private string GetUniqueColor(string str)
        {
            if (_colorsCache.TryGetValue(str, out var value))
                return value;

            int hash = str.GetHashCode();

            byte r = (byte)((hash >> 16) & 0xFF);
            byte g = (byte)((hash >> 8) & 0xFF);
            byte b = (byte)(hash & 0xFF);

            r = (byte)(r | 0x40);
            g = (byte)(g | 0x40);
            b = (byte)(b | 0x40);

            if (r < 0x80 && g < 0x80 && b < 0x80)
            {
                r = (byte)(r | 0x80);
                g = (byte)(g | 0x80);
                b = (byte)(b | 0x80);
            }

            return _colorsCache[str] = $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
